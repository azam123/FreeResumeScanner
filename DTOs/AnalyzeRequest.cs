namespace FreeResumeScanner.DTOs;

public sealed class AnalyzeRequest
{
    public IFormFile? Resume { get; set; }
    public string JobDescription { get; set; } = string.Empty;
}
