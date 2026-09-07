using FreeResumeScanner.Models;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

public sealed class ResumeChunker : IResumeChunker
{
    private const int TargetWords = 160;
    private const int OverlapWords = 30;
    private const int MinimumWords = 35;

    private static readonly HashSet<string> SectionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "professional summary", "profile", "objective", "skills", "technical skills",
        "core skills", "experience", "work experience", "professional experience", "employment",
        "education", "certifications", "projects", "achievements", "awards", "publications",
        "leadership", "languages", "interests", "volunteering"
    };

    public IReadOnlyList<ResumeChunk> Chunk(string resumeText)
    {
        var lines = resumeText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<ResumeChunk>();
        var section = "General";
        var buffer = new List<string>();
        var id = 0;

        void Flush()
        {
            if (buffer.Count == 0)
                return;

            AddWordChunks(chunks, section, string.Join(' ', buffer), ref id);
            buffer.Clear();
        }

        foreach (var line in lines)
        {
            if (IsSectionHeading(line, out var detectedSection))
            {
                Flush();
                section = detectedSection;
                continue;
            }

            buffer.Add(line);
            if (WordCount(buffer) >= TargetWords)
                Flush();
        }

        Flush();
        return chunks;
    }

    private static void AddWordChunks(
        List<ResumeChunk> result,
        string section,
        string text,
        ref int id)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < MinimumWords)
            return;

        var step = Math.Max(1, TargetWords - OverlapWords);

        for (var start = 0; start < words.Length; start += step)
        {
            var count = Math.Min(TargetWords, words.Length - start);
            if (count < MinimumWords)
                break;

            var chunk = string.Join(' ', words.Skip(start).Take(count));
            result.Add(new ResumeChunk(id++, section, chunk));

            if (start + count >= words.Length)
                break;
        }
    }

    private static int WordCount(List<string> lines)
        => lines.Sum(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

    private static bool IsSectionHeading(string line, out string section)
    {
        section = string.Empty;
        var clean = line.Trim().Trim(':', '-', '•', '*', '#').Trim();
        if (clean.Length is < 3 or > 55)
            return false;

        if (SectionNames.Contains(clean))
        {
            section = ToTitle(clean);
            return true;
        }

        if (clean.Contains('@') || clean.Contains("http", StringComparison.OrdinalIgnoreCase))
            return false;

        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 7)
            return false;

        var letters = clean.Count(char.IsLetter);
        var upper = clean.Count(char.IsUpper);

        if (letters >= 3 && upper >= Math.Max(3, (int)(letters * 0.7)) &&
            !clean.EndsWith('.') && !clean.EndsWith(',') &&
            !clean.Contains(" and ", StringComparison.OrdinalIgnoreCase))
        {
            section = ToTitle(clean);
            return true;
        }

        return false;
    }

    private static string ToTitle(string value)
        => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Length == 1
                ? x.ToUpperInvariant()
                : char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
}
