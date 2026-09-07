namespace FreeResumeScanner.Models;

public sealed class RagMetrics
{
    public int Requirements { get; init; }
    public int ResumeChunksCreated { get; init; }
    public int ChunksEmbedded { get; init; }
    public int RetrievedEvidenceItems { get; init; }
    public int UniqueRetrievedChunks { get; init; }
    public int UniqueRetrievedSections { get; init; }
    public double AverageTopSemanticSimilarity { get; init; }
    public double AverageTopHybridScore { get; init; }
    public double HighConfidenceRequirementRate { get; init; }
    public double EvidenceCoverageRate { get; init; }
    public double AverageEvidencePerRequirement { get; init; }
    public double SemanticLexicalAgreement { get; init; }
}
