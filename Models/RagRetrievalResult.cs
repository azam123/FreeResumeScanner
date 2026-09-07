namespace FreeResumeScanner.Models;

public sealed class RagRetrievalResult
{
    public IReadOnlyList<RetrievedEvidence> Evidence { get; init; } = Array.Empty<RetrievedEvidence>();
    public RagMetrics Metrics { get; init; } = new();
}
