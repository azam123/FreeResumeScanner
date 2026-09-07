using System.Text.RegularExpressions;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

public sealed partial class TextPreprocessor : ITextPreprocessor
{
    public string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        text = text.Replace("\0", " ").Replace("\r\n", "\n").Replace('\r', '\n');
        text = MultipleSpacesRegex().Replace(text, " ");
        text = ExcessiveBlankLinesRegex().Replace(text, "\n\n");

        return string.Join('\n', text.Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0));
    }

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveBlankLinesRegex();
}
