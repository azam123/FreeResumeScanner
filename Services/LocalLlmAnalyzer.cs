using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FreeResumeScanner.Models;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

public sealed class LocalLlmAnalyzer : IResumeAnalyzer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ITextPreprocessor _preprocessor;
    private readonly IResumeChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRagRetriever _ragRetriever;
    private readonly ILogger<LocalLlmAnalyzer> _logger;

    public LocalLlmAnalyzer(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ITextPreprocessor preprocessor,
        IResumeChunker chunker,
        IEmbeddingService embeddingService,
        IRagRetriever ragRetriever,
        ILogger<LocalLlmAnalyzer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _preprocessor = preprocessor;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _ragRetriever = ragRetriever;
        _logger = logger;
    }

    public async Task<AnalyzerOutput> AnalyzeAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        var normalizationWatch = Stopwatch.StartNew();
        var normalizedResume = _preprocessor.Normalize(resumeText);
        var normalizedJd = _preprocessor.Normalize(jobDescription);
        normalizationWatch.Stop();

        var chunkWatch = Stopwatch.StartNew();
        var allChunks = _chunker.Chunk(normalizedResume);
        chunkWatch.Stop();

        if (allChunks.Count == 0)
            throw new InvalidOperationException("No usable resume chunks were created.");

        var requirementWatch = Stopwatch.StartNew();
        var requirements = ExtractRequirements(normalizedJd);
        if (requirements.Count == 0)
        {
            requirements.Add(new JobRequirement(
                normalizedJd[..Math.Min(normalizedJd.Length, 1200)],
                false));
        }
        requirementWatch.Stop();

        var maxEmbeddingChunks = GetInt("LocalLLM:MaxEmbeddingChunks", 80);
        var chunksToEmbed = SelectEmbeddingCandidates(allChunks, normalizedJd, maxEmbeddingChunks);

        var embeddingWatch = Stopwatch.StartNew();
        var chunkEmbeddings = await _embeddingService.CreateEmbeddingsAsync(
            chunksToEmbed.Select(x => x.Text).ToArray(), cancellationToken);
        embeddingWatch.Stop();

        var retrievalWatch = Stopwatch.StartNew();
        var rag = await _ragRetriever.RetrieveAsync(
            requirements,
            chunksToEmbed,
            chunkEmbeddings,
            GetInt("LocalLLM:TopChunksPerRequirement", 2),
            GetInt("LocalLLM:MaxRetrievedChunks", 20),
            GetDouble("LocalLLM:HighConfidenceThreshold", 0.65),
            cancellationToken);
        retrievalWatch.Stop();

        var prompt = BuildPrompt(normalizedJd, requirements, rag.Evidence);

        var llmWatch = Stopwatch.StartNew();
        var result = await CallOllamaAsync(prompt, cancellationToken);
        llmWatch.Stop();

        _logger.LogInformation(
            "RAG analysis completed. Chunks={Chunks}, Embedded={Embedded}, Requirements={Requirements}, Evidence={Evidence}, EmbeddingMs={EmbeddingMs}, RetrievalMs={RetrievalMs}, LlmMs={LlmMs}, AvgSimilarity={Similarity:F3}",
            allChunks.Count,
            chunksToEmbed.Count,
            requirements.Count,
            rag.Evidence.Count,
            embeddingWatch.ElapsedMilliseconds,
            retrievalWatch.ElapsedMilliseconds,
            llmWatch.ElapsedMilliseconds,
            rag.Metrics.AverageTopSemanticSimilarity);

        return new AnalyzerOutput
        {
            Result = result,
            NormalizationMilliseconds = normalizationWatch.ElapsedMilliseconds,
            ChunkingMilliseconds = chunkWatch.ElapsedMilliseconds,
            RequirementExtractionMilliseconds = requirementWatch.ElapsedMilliseconds,
            EmbeddingMilliseconds = embeddingWatch.ElapsedMilliseconds,
            RetrievalMilliseconds = retrievalWatch.ElapsedMilliseconds,
            LlmMilliseconds = llmWatch.ElapsedMilliseconds,
            ResumeChunks = allChunks.Count,
            ChunksEmbedded = chunksToEmbed.Count,
            RequirementsRetrieved = rag.Evidence.Count,
            PromptCharacters = prompt.Length,
            Rag = rag.Metrics
        };
    }

    private async Task<AnalysisResult> CallOllamaAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["LocalLLM:BaseUrl"] ?? "http://localhost:11434";
        var model = _configuration["LocalLLM:Model"] ?? "llama3.2:3b";
        var client = _httpClientFactory.CreateClient("OllamaLlm");

        using var response = await client.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/api/generate",
            new
            {
                model,
                prompt,
                stream = false,
                format = "json",
                keep_alive = "10m",
                options = new
                {
                    temperature = GetDouble("LocalLLM:Temperature", 0.05),
                    num_ctx = GetInt("LocalLLM:ContextSize", 8192),
                    num_predict = GetInt("LocalLLM:MaxOutputTokens", 1800),
                    top_p = GetDouble("LocalLLM:TopP", 0.9)
                }
            },
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama API returned {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        var content = json.RootElement.TryGetProperty("response", out var responseElement)
            ? responseElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Ollama returned an empty analysis response.");

        try
        {
            return JsonSerializer.Deserialize<AnalysisResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Ollama returned no analysis object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Ollama returned invalid JSON. Try a stronger model or reduce the prompt size.", ex);
        }
    }

    private static string BuildPrompt(
        string jd,
        IReadOnlyList<JobRequirement> requirements,
        IReadOnlyList<RetrievedEvidence> evidence)
    {
        const int maxJdCharacters = 12000;
        const int maxEvidenceCharacters = 1000;

        var builder = new StringBuilder(26000);
        builder.AppendLine("You are a rigorous resume-to-job-description analysis engine.");
        builder.AppendLine("Analyze any profession and any seniority level.");
        builder.AppendLine("This is a RAG pipeline. Retrieved resume excerpts are evidence candidates, not proof.");
        builder.AppendLine("Only claim experience when the retrieved text supports it.");
        builder.AppendLine("If evidence is incomplete, use Partial. If there is no supporting evidence, use Missing.");
        builder.AppendLine("Never infer an unproven skill from semantic similarity alone.");
        builder.AppendLine("Return ONLY valid JSON. Do not use markdown.");
        builder.AppendLine();
        builder.AppendLine("Return exactly this JSON shape:");
        builder.AppendLine("{\n  \"candidateName\":\"\",\n  \"jobTitle\":\"\",\n  \"seniority\":\"\",\n  \"overallMatch\":0,\n  \"technicalFit\":0,\n  \"skillsMatch\":0,\n  \"experienceMatch\":0,\n  \"responsibilitiesMatch\":0,\n  \"seniorityMatch\":0,\n  \"educationMatch\":0,\n  \"domainMatch\":0,\n  \"leadershipMatch\":0,\n  \"achievementMatch\":0,\n  \"atsScore\":0,\n  \"executiveVerdict\":\"\",\n  \"strongestEvidence\":[],\n  \"criticalGaps\":[],\n  \"existingKeywords\":[],\n  \"missingKeywords\":[],\n  \"recommendations\":[],\n  \"interviewRisks\":[],\n  \"requirements\":[{\"requirement\":\"\",\"evidence\":\"\",\"status\":\"\",\"recommendation\":\"\"}],\n  \"ats\":{\"strengths\":[],\"risks\":[]}\n}");
        builder.AppendLine();
        builder.AppendLine("Scores are integers from 0 to 100.");
        builder.AppendLine("For requirements use only Strong, Partial or Missing.");
        builder.AppendLine("Preserve the requirement meaning and evaluate every requirement listed below.");
        builder.AppendLine();
        builder.AppendLine("JOB DESCRIPTION:");
        builder.AppendLine(jd.Length > maxJdCharacters ? jd[..maxJdCharacters] : jd);
        builder.AppendLine();
        builder.AppendLine("REQUIREMENTS TO EVALUATE:");

        for (var i = 0; i < requirements.Count; i++)
            builder.AppendLine($"R{i + 1}. {(requirements[i].IsMustHave ? "[MUST] " : "[PREFERRED] ")}{requirements[i].Text}");

        builder.AppendLine();
        builder.AppendLine("RETRIEVED RESUME EVIDENCE:");

        foreach (var item in evidence)
        {
            var text = item.Text.Length > maxEvidenceCharacters
                ? item.Text[..maxEvidenceCharacters] + "..."
                : item.Text;

            builder.AppendLine($"[R{item.RequirementId} | Section={item.Section} | Semantic={item.SemanticScore:F3} | Lexical={item.LexicalScore:F3} | Hybrid={item.HybridScore:F3}]");
            builder.AppendLine(text);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<ResumeChunk> SelectEmbeddingCandidates(
        IReadOnlyList<ResumeChunk> chunks,
        string jobDescription,
        int max)
    {
        if (chunks.Count <= max)
            return chunks;

        var jdTokens = Tokenize(jobDescription);
        var ranked = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = LexicalOverlap(jdTokens, Tokenize(chunk.Text))
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var selected = new List<ResumeChunk>(max);
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Guarantee representation from different resume sections first.
        foreach (var item in ranked)
        {
            if (selected.Count >= max)
                break;

            if (sections.Add(item.Chunk.Section))
                selected.Add(item.Chunk);
        }

        foreach (var item in ranked)
        {
            if (selected.Count >= max)
                break;

            if (!selected.Contains(item.Chunk))
                selected.Add(item.Chunk);
        }

        return selected;
    }

    private static double LexicalOverlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0;

        return (double)a.Intersect(b).Count() / Math.Min(a.Count, b.Count);
    }

    private static HashSet<string> Tokenize(string text)
        => text.ToLowerInvariant()
            .Split(new[] { ' ', '\r', '\n', '\t', ',', '.', ':', ';', '/', '(', ')', '[', ']', '|', '-' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);

    private static List<JobRequirement> ExtractRequirements(string jd)
    {
        var lines = jd.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var requirements = new List<JobRequirement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimStart('-', '•', '*', '>', '–', '—', '·').Trim();
            if (line.Length < 12 || line.Length > 500)
                continue;

            var lower = line.ToLowerInvariant();
            var isBullet = raw.TrimStart().Length > 0 && "-•*>–—·".Contains(raw.TrimStart()[0]);
            var looksLikeRequirement = isBullet
                || lower.Contains("required")
                || lower.Contains("must have")
                || lower.Contains("experience with")
                || lower.Contains("years of")
                || lower.Contains("proficient")
                || lower.Contains("knowledge of")
                || lower.Contains("responsible for")
                || lower.Contains("ability to")
                || lower.Contains("qualification")
                || lower.Contains("preferred")
                || lower.Contains("nice to have")
                || lower.Contains("degree in");

            if (looksLikeRequirement && seen.Add(line))
            {
                var mustHave = lower.Contains("required")
                    || lower.Contains("must have")
                    || lower.Contains("mandatory");

                requirements.Add(new JobRequirement(line, mustHave));
            }
        }

        return requirements.Take(30).ToList();
    }

    private int GetInt(string key, int fallback)
        => int.TryParse(_configuration[key], out var value) && value > 0 ? value : fallback;

    private double GetDouble(string key, double fallback)
        => double.TryParse(_configuration[key], out var value) && value > 0 ? value : fallback;
}
