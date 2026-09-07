using System.Text.RegularExpressions;
using FreeResumeScanner.Models;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

/// <summary>
/// Hybrid RAG retriever: semantic similarity + lexical overlap.
/// It uses in-memory vectors because a single resume is small enough that
/// a database/vector service would add unnecessary latency and complexity.
/// </summary>
public sealed partial class RagRetriever : IRagRetriever
{
    private readonly IEmbeddingService _embeddingService;

    public RagRetriever(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public async Task<RagRetrievalResult> RetrieveAsync(
        IReadOnlyList<JobRequirement> requirements,
        IReadOnlyList<ResumeChunk> chunks,
        IReadOnlyList<float[]> chunkEmbeddings,
        int topKPerRequirement,
        int maxEvidence,
        double highConfidenceThreshold,
        CancellationToken cancellationToken)
    {
        if (requirements.Count == 0 || chunks.Count == 0)
            return new RagRetrievalResult();

        if (chunks.Count != chunkEmbeddings.Count)
            throw new InvalidOperationException("Chunk and embedding counts do not match.");

        var queryEmbeddings = await _embeddingService.CreateEmbeddingsAsync(
            requirements.Select(x => x.Text).ToArray(), cancellationToken);

        var candidates = new List<RetrievedEvidence>();
        var topSemanticScores = new List<double>();
        var topHybridScores = new List<double>();
        var highConfidence = 0;
        var covered = 0;
        var agreement = 0;

        for (var requirementIndex = 0; requirementIndex < requirements.Count; requirementIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requirement = requirements[requirementIndex];
            var queryEmbedding = queryEmbeddings[requirementIndex];
            var queryTokens = Tokenize(requirement.Text);

            var ranked = new List<(ResumeChunk Chunk, double Semantic, double Lexical, double Hybrid)>();

            for (var i = 0; i < chunks.Count; i++)
            {
                var semantic = Cosine(queryEmbedding, chunkEmbeddings[i]);
                var lexical = LexicalF1(queryTokens, Tokenize(chunks[i].Text));
                var hybrid = (semantic * 0.75) + (lexical * 0.25);
                ranked.Add((chunks[i], semantic, lexical, hybrid));
            }

            var top = ranked
                .OrderByDescending(x => x.Hybrid)
                .ThenByDescending(x => x.Semantic)
                .Take(Math.Max(1, topKPerRequirement))
                .ToList();

            if (top.Count > 0)
            {
                topSemanticScores.Add(top[0].Semantic);
                topHybridScores.Add(top[0].Hybrid);

                if (top[0].Hybrid >= highConfidenceThreshold)
                    highConfidence++;

                if (top[0].Semantic >= highConfidenceThreshold)
                    covered++;

                if (top[0].Semantic >= 0.55 && top[0].Lexical >= 0.05)
                    agreement++;
            }

            candidates.AddRange(top.Select(x => new RetrievedEvidence(
                requirementIndex + 1,
                requirement.Text,
                x.Chunk.Section,
                x.Chunk.Text,
                x.Semantic,
                x.Lexical,
                x.Hybrid)));
        }

        // Prefer strong evidence while keeping multiple requirements represented.
        var selected = candidates
            .GroupBy(x => x.RequirementId)
            .SelectMany(g => g.OrderByDescending(x => x.HybridScore).Take(Math.Max(1, topKPerRequirement)))
            .OrderByDescending(x => x.HybridScore)
            .Take(Math.Max(1, maxEvidence))
            .ToList();

        var metrics = new RagMetrics
        {
            Requirements = requirements.Count,
            ResumeChunksCreated = chunks.Count,
            RetrievedEvidenceItems = selected.Count,
            UniqueRetrievedChunks = selected
                .Select(x => x.Text)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            UniqueRetrievedSections = selected
                .Select(x => x.Section)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageTopSemanticSimilarity = Average(topSemanticScores),
            AverageTopHybridScore = Average(topHybridScores),
            HighConfidenceRequirementRate = Rate(highConfidence, requirements.Count),
            EvidenceCoverageRate = Rate(covered, requirements.Count),
            AverageEvidencePerRequirement = requirements.Count == 0 ? 0 : (double)selected.Count / requirements.Count,
            SemanticLexicalAgreement = Rate(agreement, requirements.Count)
        };

        return new RagRetrievalResult
        {
            Evidence = selected,
            Metrics = metrics
        };
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0 || normB <= 0)
            return 0;

        return Math.Clamp(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)), -1, 1);
    }

    private static double LexicalF1(HashSet<string> query, HashSet<string> document)
    {
        if (query.Count == 0 || document.Count == 0)
            return 0;

        var intersection = query.Intersect(document).Count();
        if (intersection == 0)
            return 0;

        var precision = (double)intersection / document.Count;
        var recall = (double)intersection / query.Count;
        return 2 * precision * recall / (precision + recall);
    }

    private static HashSet<string> Tokenize(string text)
        => TokenRegex().Matches(text.ToLowerInvariant())
            .Select(x => x.Value)
            .Where(x => x.Length >= 3 && !StopWords.Contains(x))
            .ToHashSet(StringComparer.Ordinal);

    private static double Average(IReadOnlyList<double> values)
        => values.Count == 0 ? 0 : values.Average();

    private static double Rate(int value, int total)
        => total == 0 ? 0 : Math.Round((double)value / total * 100, 1);

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "with", "that", "this", "from", "are", "you", "your",
        "our", "will", "have", "has", "into", "their", "they", "years", "year", "job",
        "role", "work", "working", "using", "use", "ability", "experience"
    };

    [GeneratedRegex("[a-zA-Z0-9+#.]+", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();
}
