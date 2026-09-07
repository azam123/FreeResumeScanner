namespace FreeResumeScanner.Services.Interfaces;

public interface IDocumentExtractor
{
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}
