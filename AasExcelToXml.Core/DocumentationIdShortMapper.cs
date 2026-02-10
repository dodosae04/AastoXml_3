namespace AasExcelToXml.Core;

internal static class DocumentationIdShortMapper
{
    public static string? ResolveDocumentCollectionIdShort(string? collectionIdShort, int index, string aasIdShort, string pattern)
    {
        if (string.IsNullOrWhiteSpace(collectionIdShort))
        {
            return null;
        }

        return NormalizeIdShort(collectionIdShort);
    }

    private static string NormalizeIdShort(string raw)
    {
        var normalized = new string(raw.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, "_{2,}", "_").Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Unnamed";
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = "_" + normalized;
        }

        return normalized;
    }
}
