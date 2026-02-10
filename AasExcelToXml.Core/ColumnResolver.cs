namespace AasExcelToXml.Core;

public sealed class ColumnResolver
{
    private readonly Dictionary<string, string[]> _aliasesByKey;

    public ColumnResolver(ColumnAliasSettings settings)
    {
        _aliasesByKey = settings.Columns.ToDictionary(
            item => item.Key,
            item => item.Aliases.Select(NormalizeHeaderKey).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, int> Resolve(IReadOnlyDictionary<string, int> headerMap)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, aliases) in _aliasesByKey)
        {
            foreach (var alias in aliases)
            {
                if (headerMap.TryGetValue(alias, out var idx))
                {
                    map[key] = idx;
                    break;
                }
            }
        }

        return map;
    }

    public static string NormalizeHeaderKey(string header)
    {
        return (header ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }
}
