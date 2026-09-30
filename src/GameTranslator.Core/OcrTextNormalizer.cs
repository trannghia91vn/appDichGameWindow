using System.Text.RegularExpressions;

namespace GameTranslator.Core;

public static partial class OcrTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedLines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd());

        var normalized = string.Join("\n", normalizedLines).Trim();
        return RepeatedBlankLines().Replace(normalized, "\n\n");
    }

    [GeneratedRegex("\\n(?:[ \\t]*\\n){2,}")]
    private static partial Regex RepeatedBlankLines();
}
