using System.Text.RegularExpressions;

namespace AasExcelToXml.Core;

internal static class IdShortNormalizer
{
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unnamed";
        }

        var trimmed = raw.Trim();
        var normalized = new string(trimmed.Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '_'
        ).ToArray());

        normalized = Regex.Replace(normalized, "_{2,}", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Unnamed";
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = "_" + normalized;
        }

        return normalized;
    }
}

