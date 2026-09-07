using DocumentFormat.OpenXml.Packaging;
using FreeResumeScanner.Services.Interfaces;
using UglyToad.PdfPig;

namespace FreeResumeScanner.Services;

public sealed class DocumentExtractor : IDocumentExtractor
{
    public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(filePath, cancellationToken),
            ".docx" => ExtractDocx(filePath, cancellationToken),
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath, cancellationToken),
            _ => throw new ArgumentException("Supported formats are PDF, DOCX, TXT and MD.")
        };
    }

    private static string ExtractPdf(string path, CancellationToken ct)
    {
        using var document = PdfDocument.Open(path);
        var builder = new System.Text.StringBuilder();
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            builder.AppendLine(page.Text);
        }
        return builder.ToString();
    }

    private static string ExtractDocx(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var document = WordprocessingDocument.Open(path, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }
}
