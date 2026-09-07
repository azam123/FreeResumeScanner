using FreeResumeScanner.Models;

namespace FreeResumeScanner.Services.Interfaces;

public interface IRagRetriever
{
    Task<RagRetrievalResult> RetrieveAsync(
        IReadOnlyList<JobRequirement> requirements,
        IReadOnlyList<ResumeChunk> chunks,
        IReadOnlyList<float[]> chunkEmbeddings,
        int topKPerRequirement,
        int maxEvidence,
        double highConfidenceThreshold,
        CancellationToken cancellationToken);
}
