using FreeResumeScanner.Models;

namespace FreeResumeScanner.Services.Interfaces;

public interface IResumeAnalyzer
{
    Task<AnalyzerOutput> AnalyzeAsync(string resumeText, string jobDescription, CancellationToken cancellationToken);
}
