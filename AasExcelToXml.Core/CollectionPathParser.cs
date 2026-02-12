using System.Text.RegularExpressions;

namespace AasExcelToXml.Core;

internal static class CollectionPathParser
{
    private static readonly char[] AbsoluteSeparators = new[] { '>', '/', '\\', '|' };

    public static IReadOnlyList<string> Split(string? raw, string? currentPath = null)
    {
        var currentSegments = ParseCanonical(currentPath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return currentSegments;
        }

        var value = raw.TrimEnd();
        var arrowDepth = CountLeadingArrows(value);
        if (arrowDepth > 0)
        {
            var name = value.Substring(arrowDepth).Trim();
            return BuildRelativePath(currentSegments, arrowDepth, name);
        }

        var indentDepth = CountIndentDepth(value);
        if (indentDepth > 0)
        {
            var name = value.TrimStart();
            return BuildRelativePath(currentSegments, indentDepth, name);
        }

        if (ContainsAbsoluteSeparator(value))
        {
            return SplitAbsolute(value);
        }

        return new[] { IdShortNormalizer.Normalize(value) };
    }

    public static string ToKey(IReadOnlyList<string> segments)
    {
        return segments.Count == 0 ? string.Empty : string.Join("/", segments);
    }

    public static IReadOnlyList<string> ParseCanonical(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<string>();
        }

        return key.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(IdShortNormalizer.Normalize)
            .ToList();
    }

    private static IReadOnlyList<string> BuildRelativePath(IReadOnlyList<string> currentSegments, int depth, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return currentSegments;
        }

        var targetDepth = Math.Max(0, Math.Min(currentSegments.Count, depth));
        var segments = currentSegments.Take(targetDepth).ToList();
        segments.Add(IdShortNormalizer.Normalize(name));
        return segments;
    }

    private static IReadOnlyList<string> SplitAbsolute(string value)
    {
        return value
            .Split(AbsoluteSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(IdShortNormalizer.Normalize)
            .ToList();
    }

    private static bool ContainsAbsoluteSeparator(string value)
    {
        return value.IndexOfAny(AbsoluteSeparators) >= 0;
    }

    private static int CountLeadingArrows(string value)
    {
        var count = 0;
        while (count < value.Length && value[count] == '>')
        {
            count++;
        }

        return count;
    }

    private static int CountIndentDepth(string value)
    {
        var match = Regex.Match(value, @"^(?<indent>[\t ]+)");
        if (!match.Success)
        {
            return 0;
        }

        var indent = match.Groups["indent"].Value;
        var spaces = indent.Count(ch => ch == ' ');
        var tabs = indent.Count(ch => ch == '\t');
        return tabs + (spaces / 2);
    }
}

