using System.Diagnostics;
using FreeResumeScanner.DTOs;
using FreeResumeScanner.Models;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

public sealed class AnalysisService : IAnalysisService
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private readonly IDocumentExtractor _extractor;
    private readonly IResumeAnalyzer _analyzer;
    private readonly IMatchingEngine _matcher;
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(
        IDocumentExtractor extractor,
        IResumeAnalyzer analyzer,
        IMatchingEngine matcher,
        ILogger<AnalysisService> logger)
    {
        _extractor = extractor;
        _analyzer = analyzer;
        _matcher = matcher;
        _logger = logger;
    }

    public async Task<AnalyzeResponse> AnalyzeAsync(
        IFormFile resume,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        Validate(resume, jobDescription);

        var startedAt = DateTimeOffset.UtcNow;
        var totalWatch = Stopwatch.StartNew();
        var extension = Path.GetExtension(resume.FileName).ToLowerInvariant();
        var directory = Path.Combine(Path.GetTempPath(), "FreeResumeScanner");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using (var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await resume.CopyToAsync(stream, cancellationToken);
            }

            var extractionWatch = Stopwatch.StartNew();
            var text = await _extractor.ExtractTextAsync(path, cancellationToken);
            extractionWatch.Stop();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Could not extract readable text from the resume.");

            var analyzerOutput = await _analyzer.AnalyzeAsync(
                text,
                jobDescription,
                cancellationToken);

            var result = _matcher.Calculate(analyzerOutput.Result);
            totalWatch.Stop();
            var completedAt = DateTimeOffset.UtcNow;

            var metrics = new ProcessingMetrics
            {
                StartedAt = startedAt,
                CompletedAt = completedAt,
                TotalMilliseconds = totalWatch.ElapsedMilliseconds,
                NormalizationMilliseconds = analyzerOutput.NormalizationMilliseconds,
                ExtractionMilliseconds = extractionWatch.ElapsedMilliseconds,
                ChunkingMilliseconds = analyzerOutput.ChunkingMilliseconds,
                RequirementExtractionMilliseconds = analyzerOutput.RequirementExtractionMilliseconds,
                EmbeddingMilliseconds = analyzerOutput.EmbeddingMilliseconds,
                RetrievalMilliseconds = analyzerOutput.RetrievalMilliseconds,
                LlmMilliseconds = analyzerOutput.LlmMilliseconds,
                ResumeCharacters = text.Length,
                ResumeWords = CountWords(text),
                ResumeChunks = analyzerOutput.ResumeChunks,
                ChunksEmbedded = analyzerOutput.ChunksEmbedded,
                RequirementsExtracted = analyzerOutput.Rag.Requirements,
                RequirementsRetrieved = analyzerOutput.RequirementsRetrieved,
                PromptCharacters = analyzerOutput.PromptCharacters,
                Rag = analyzerOutput.Rag
            };

            _logger.LogInformation(
                "Analysis completed in {TotalMs}ms. Extraction={ExtractionMs}ms, Embedding={EmbeddingMs}ms, Retrieval={RetrievalMs}ms, LLM={LlmMs}ms",
                metrics.TotalMilliseconds,
                metrics.ExtractionMilliseconds,
                metrics.EmbeddingMilliseconds,
                metrics.RetrievalMilliseconds,
                metrics.LlmMilliseconds);

            return new AnalyzeResponse
            {
                CreatedAt = startedAt,
                Processing = metrics,
                Result = result
            };
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete temporary resume file.");
            }
        }
    }

    private static int CountWords(string text)
        => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static void Validate(IFormFile? resume, string jd)
    {
        if (resume is null || resume.Length == 0)
            throw new ArgumentException("Resume file is required.");

        if (resume.Length > MaxFileSize)
            throw new ArgumentException("Resume cannot exceed 10 MB.");

        if (string.IsNullOrWhiteSpace(jd))
            throw new ArgumentException("Job description is required.");

        var extension = Path.GetExtension(resume.FileName).ToLowerInvariant();
        if (!new[] { ".pdf", ".docx", ".txt", ".md" }.Contains(extension))
            throw new ArgumentException("Supported resume formats are PDF, DOCX, TXT and MD.");
    }
}
