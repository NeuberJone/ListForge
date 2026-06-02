namespace ListForge.Core;

public static class TextSearchHelper
{
    public static List<(int start, int length)> FindMatches(string? text, string? term, bool matchCase)
    {
        var matches = new List<(int start, int length)>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
            return matches;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = 0;

        while (true)
        {
            var position = text.IndexOf(term, index, comparison);
            if (position < 0)
                break;

            matches.Add((position, term.Length));
            index = position + term.Length;
        }

        return matches;
    }

    public static string ReplaceAt(string text, int start, int length, string replacement) =>
        text[..start] + replacement + text[(start + length)..];

    public static string ReplaceAll(string text, string term, string replacement, bool matchCase)
    {
        if (string.IsNullOrEmpty(term))
            return text;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Replace(term, replacement, comparison);
    }
}
