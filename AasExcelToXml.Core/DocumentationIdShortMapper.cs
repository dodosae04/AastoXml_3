using System.Linq;
using System.Text.RegularExpressions;

namespace AasExcelToXml.Core;

internal static class DocumentationIdShortMapper
{
    public static string ResolveDocumentCollectionIdShort(string? collectionIdShort, int index, string aasIdShort, string pattern)
    {
        if (IsAirbalanceAas(aasIdShort))
        {
            return FormatDocumentIdShort(pattern, index);
        }

        if (string.IsNullOrWhiteSpace(collectionIdShort))
        {
            var fallbackSuffix = ResolveDocumentSpecSuffix(aasIdShort);
            return $"Document_spec_{fallbackSuffix}";
        }

        var trimmed = collectionIdShort.Trim();
        if (trimmed.StartsWith("Document", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var suffix = ResolveDocumentSpecSuffix(aasIdShort);
        return $"Document_spec_{suffix}";
    }

    public static string ResolveDocumentSpecSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "doc";
        }

        var trimmed = name.Trim();
        if (Regex.IsMatch(trimmed, @"^Cylinder[_\-\s]*main$", RegexOptions.IgnoreCase))
        {
            return "cymain";
        }

        var cylinderMatch = Regex.Match(trimmed, @"^Cylinder[_\-\s]*(\d+)$", RegexOptions.IgnoreCase);
        if (cylinderMatch.Success)
        {
            return $"cy{cylinderMatch.Groups[1].Value}";
        }

        var parts = trimmed
            .Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            return "doc";
        }

        var initials = string.Concat(parts.Select(part => char.ToLowerInvariant(part[0])));
        return string.IsNullOrWhiteSpace(initials) ? "doc" : initials;
    }

    public static bool IsAirbalanceAas(string? aasIdShort)
    {
        return string.Equals(aasIdShort, "Airbalance_robot", StringComparison.Ordinal);
    }

    private static string FormatDocumentIdShort(string pattern, int index)
    {
        if (pattern.Contains("{N:00}", StringComparison.Ordinal))
        {
            return pattern.Replace("{N:00}", index.ToString("00"));
        }

        if (pattern.Contains("{N}", StringComparison.Ordinal))
        {
            return pattern.Replace("{N}", index.ToString("00"));
        }

        var match = Regex.Match(pattern, @"\d+");
        if (match.Success)
        {
            return Regex.Replace(pattern, @"\d+", index.ToString(match.Value.Length == 2 ? "00" : "0"));
        }

        return $"{pattern}{index:00}";
    }
}
