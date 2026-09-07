using FreeResumeScanner.Models;

namespace FreeResumeScanner.Services.Interfaces;

public interface IMatchingEngine
{
    AnalysisResult Calculate(AnalysisResult analysis);
}
