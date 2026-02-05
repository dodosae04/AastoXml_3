namespace AasExcelToXml.Core;

public sealed class ReferenceIndex
{
    private readonly Dictionary<(string AasIdShort, string SubmodelIdShort), string> _submodelIriByAasSubmodel;
    private readonly Dictionary<string, List<PropertyEntry>> _propertiesByAas;

    private ReferenceIndex(
        Dictionary<(string AasIdShort, string SubmodelIdShort), string> submodelIriByAasSubmodel,
        Dictionary<string, List<PropertyEntry>> propertiesByAas)
    {
        _submodelIriByAasSubmodel = submodelIriByAasSubmodel;
        _propertiesByAas = propertiesByAas;
    }

    public static ReferenceIndex Build(AasEnvironmentSpec spec, Dictionary<string, string> submodelIdMap)
    {
        var submodelIriMap = new Dictionary<(string, string), string>();
        var propertyMap = new Dictionary<string, List<PropertyEntry>>(StringComparer.Ordinal);

        foreach (var aas in spec.Assets)
        {
            foreach (var submodel in aas.Submodels)
            {
                var key = $"{aas.IdShort}::{submodel.IdShort}";
                if (!submodelIdMap.TryGetValue(key, out var submodelIri))
                {
                    continue;
                }

                submodelIriMap[(aas.IdShort, submodel.IdShort)] = submodelIri;

                foreach (var element in submodel.Elements)
                {
                    if (element.Kind != ElementKind.Property)
                    {
                        continue;
                    }

                    if (!propertyMap.TryGetValue(aas.IdShort, out var list))
                    {
                        list = new List<PropertyEntry>();
                        propertyMap[aas.IdShort] = list;
                    }

                    list.Add(new PropertyEntry(element.IdShort, submodel.IdShort, submodelIri));
                }
            }
        }

        return new ReferenceIndex(submodelIriMap, propertyMap);
    }

    public ReferenceResolution ResolvePropertyReference(string currentAasIdShort, string currentSubmodelIdShort, string? targetAasIdShort, string propertyIdShort)
    {
        if (string.IsNullOrWhiteSpace(propertyIdShort))
        {
            return ReferenceResolution.Unresolved(propertyIdShort);
        }

        var resolvedTargetAas = string.IsNullOrWhiteSpace(targetAasIdShort) ? currentAasIdShort : targetAasIdShort;
        if (!_propertiesByAas.TryGetValue(resolvedTargetAas, out var candidates) || candidates.Count == 0)
        {
            return ReferenceResolution.Unresolved(propertyIdShort);
        }

        var normalizedProperty = NormalizeMatchKey(propertyIdShort);
        var matched = candidates.Where(c => NormalizeMatchKey(c.PropertyIdShort) == normalizedProperty).ToList();
        if (matched.Count == 0)
        {
            var best = FindClosest(candidates, normalizedProperty, 2);
            if (best is null)
            {
                return ReferenceResolution.Unresolved(propertyIdShort);
            }

            return ResolveWithContext(best.PropertyIdShort, resolvedTargetAas, currentAasIdShort, currentSubmodelIdShort, true);
        }

        if (matched.Count == 1)
        {
            return new ReferenceResolution(matched[0].SubmodelIri, matched[0].PropertyIdShort, false, true);
        }

        var preferred = matched.FirstOrDefault(c =>
            string.Equals(resolvedTargetAas, currentAasIdShort, StringComparison.Ordinal)
            && string.Equals(c.SubmodelIdShort, currentSubmodelIdShort, StringComparison.Ordinal));

        var selected = preferred ?? matched.OrderBy(m => m.SubmodelIdShort, StringComparer.Ordinal).First();
        return new ReferenceResolution(selected.SubmodelIri, selected.PropertyIdShort, false, true);
    }

    public string? GetFallbackSubmodelIri(string aasIdShort, string submodelIdShort)
    {
        return _submodelIriByAasSubmodel.TryGetValue((aasIdShort, submodelIdShort), out var iri) ? iri : null;
    }

    private ReferenceResolution ResolveWithContext(string propertyIdShort, string targetAasIdShort, string currentAasIdShort, string currentSubmodelIdShort, bool autoCorrected)
    {
        if (!_propertiesByAas.TryGetValue(targetAasIdShort, out var candidates))
        {
            return ReferenceResolution.Unresolved(propertyIdShort);
        }

        var matched = candidates.Where(c => string.Equals(c.PropertyIdShort, propertyIdShort, StringComparison.Ordinal)).ToList();
        if (matched.Count == 0)
        {
            return ReferenceResolution.Unresolved(propertyIdShort);
        }

        var preferred = matched.FirstOrDefault(c =>
            string.Equals(targetAasIdShort, currentAasIdShort, StringComparison.Ordinal)
            && string.Equals(c.SubmodelIdShort, currentSubmodelIdShort, StringComparison.Ordinal));

        var selected = preferred ?? matched.OrderBy(m => m.SubmodelIdShort, StringComparer.Ordinal).First();
        return new ReferenceResolution(selected.SubmodelIri, selected.PropertyIdShort, autoCorrected, true);
    }

    private static PropertyEntry? FindClosest(IEnumerable<PropertyEntry> candidates, string normalized, int maxDistance)
    {
        var bestDistance = int.MaxValue;
        PropertyEntry? best = null;
        foreach (var candidate in candidates)
        {
            var distance = LevenshteinDistance(normalized, NormalizeMatchKey(candidate.PropertyIdShort), maxDistance);
            if (distance <= maxDistance && distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static string NormalizeMatchKey(string value)
    {
        var filtered = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return filtered.ToLowerInvariant();
    }

    private static int LevenshteinDistance(string source, string target, int maxDistance)
    {
        if (Math.Abs(source.Length - target.Length) > maxDistance)
        {
            return maxDistance + 1;
        }

        var costs = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            costs[0] = i;
            var prevCost = i - 1;
            var minInRow = costs[0];
            for (var j = 1; j <= target.Length; j++)
            {
                var currentCost = costs[j];
                var substitutionCost = source[i - 1] == target[j - 1] ? 0 : 1;
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), prevCost + substitutionCost);
                prevCost = currentCost;
                if (costs[j] < minInRow)
                {
                    minInRow = costs[j];
                }
            }

            if (minInRow > maxDistance)
            {
                return maxDistance + 1;
            }
        }

        return costs[target.Length];
    }

    private sealed record PropertyEntry(string PropertyIdShort, string SubmodelIdShort, string SubmodelIri);
}

public readonly record struct ReferenceResolution(string? SubmodelIri, string? PropertyIdShort, bool IsAutoCorrected, bool IsResolved)
{
    public static ReferenceResolution Unresolved(string? propertyIdShort)
    {
        return new ReferenceResolution(null, propertyIdShort, false, false);
    }
}
