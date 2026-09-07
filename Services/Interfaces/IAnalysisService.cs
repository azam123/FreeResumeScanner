using FreeResumeScanner.DTOs;

namespace FreeResumeScanner.Services.Interfaces;

public interface IAnalysisService
{
    Task<AnalyzeResponse> AnalyzeAsync(IFormFile resume, string jobDescription, CancellationToken cancellationToken);
}
