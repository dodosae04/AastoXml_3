namespace AasExcelToXml.Core;

public static class ValueTypeMapper
{
    public static string ResolveAas3ValueType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return "xs:string";
        }

        var trimmed = rawType.Trim();
        if (trimmed.StartsWith("xs:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("xsd:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var normalized = trimmed.ToLowerInvariant();
        if (normalized.Contains("double") || normalized.Contains("float") || normalized.Contains("decimal"))
        {
            return "xs:double";
        }

        if (normalized.Contains("int"))
        {
            return "xs:int";
        }

        if (normalized.Contains("bool"))
        {
            return "xs:boolean";
        }

        return "xs:string";
    }
}
