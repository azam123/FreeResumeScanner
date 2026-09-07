using FreeResumeScanner.Models;

namespace FreeResumeScanner.DTOs;

public sealed class AnalyzeResponse
{
    public string AnalysisId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ProcessingMetrics Processing { get; init; } = new();
    public AnalysisResult Result { get; init; } = new();
}
