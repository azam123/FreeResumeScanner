namespace FreeResumeScanner.Models;

public sealed class AnalyzerOutput
{
    public AnalysisResult Result { get; init; } = new();
    public long NormalizationMilliseconds { get; init; }
    public long ChunkingMilliseconds { get; init; }
    public long RequirementExtractionMilliseconds { get; init; }
    public long EmbeddingMilliseconds { get; init; }
    public long RetrievalMilliseconds { get; init; }
    public long LlmMilliseconds { get; init; }
    public int ResumeChunks { get; init; }
    public int ChunksEmbedded { get; init; }
    public int RequirementsRetrieved { get; init; }
    public int PromptCharacters { get; init; }
    public RagMetrics Rag { get; init; } = new();
}
