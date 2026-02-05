using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AasExcelToXml.Core.GoldenDiff;

public static class Aas3GoldenDiffAnalyzer
{
    private static readonly Regex ExampleIriPattern = new(@"^https://example\.com/ids/(?<type>sm|asset)/\d{4}_\d{4}_\d{4}_\d{4}$", RegexOptions.Compiled);

    public static Aas3GoldenDiffReport Analyze(string goldenPath, string generatedPath)
    {
        var golden = LoadSnapshot(goldenPath);
        var generated = LoadSnapshot(generatedPath);

        var report = new Aas3GoldenDiffReport();

        CheckIdentifiers(generated, report);
        CompareShells(golden, generated, report);
        CompareSubmodels(golden, generated, report);
        CompareSubmodelElements(golden, generated, report);
        CheckReferenceElements(generated, report);
        CheckRelationshipElements(generated, report);

        return report;
    }

    public static string BuildSummary(Aas3GoldenDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AAS3 Golden diff report");
        builder.AppendLine($"- Missing in generated: {report.MissingInGenerated.Count}");
        builder.AppendLine($"- Extra in generated: {report.ExtraInGenerated.Count}");
        builder.AppendLine($"- Different value/type: {report.DifferentValues.Count}");
        builder.AppendLine($"- Identifier issues: {report.IdentifierIssues.Count}");
        builder.AppendLine($"- Reference issues: {report.ReferenceIssues.Count}");
        builder.AppendLine($"- Relationship issues: {report.RelationshipIssues.Count}");
        return builder.ToString();
    }

    public static string BuildReport(Aas3GoldenDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Missing in generated");
        AppendSection(builder, report.MissingInGenerated);
        builder.AppendLine("Extra in generated");
        AppendSection(builder, report.ExtraInGenerated);
        builder.AppendLine("Different value/type");
        AppendSection(builder, report.DifferentValues);
        builder.AppendLine("Identifier issues");
        AppendSection(builder, report.IdentifierIssues);
        builder.AppendLine("Reference issues");
        AppendSection(builder, report.ReferenceIssues);
        builder.AppendLine("Relationship issues");
        AppendSection(builder, report.RelationshipIssues);
        return builder.ToString();
    }

    private static Aas3Snapshot LoadSnapshot(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            return Aas3Snapshot.FromJson(path);
        }

        return Aas3Snapshot.FromXml(path);
    }

    private static void CheckIdentifiers(Aas3Snapshot snapshot, Aas3GoldenDiffReport report)
    {
        var allIds = snapshot.GetAllIdentifiers().ToList();
        var duplicates = allIds.GroupBy(id => id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var duplicate in duplicates)
        {
            report.IdentifierIssues.Add($"Duplicate identifier: {duplicate}");
        }

        foreach (var id in allIds)
        {
            if (id.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
            {
                report.IdentifierIssues.Add($"Identifier uses UUID URN: {id}");
            }
        }

        foreach (var shell in snapshot.Shells.Values)
        {
            if (!string.IsNullOrWhiteSpace(shell.AssetGlobalAssetId)
                && !ExampleIriPattern.IsMatch(shell.AssetGlobalAssetId))
            {
                report.IdentifierIssues.Add($"Asset globalAssetId is not example IRI: {shell.AssetGlobalAssetId}");
            }
        }

        foreach (var submodel in snapshot.Submodels.Values)
        {
            if (!string.IsNullOrWhiteSpace(submodel.Id) && !ExampleIriPattern.IsMatch(submodel.Id))
            {
                report.IdentifierIssues.Add($"Submodel id is not example IRI: {submodel.Id}");
            }
        }
    }

    private static void CompareShells(Aas3Snapshot golden, Aas3Snapshot generated, Aas3GoldenDiffReport report)
    {
        foreach (var goldenShell in golden.Shells.Values)
        {
            if (!generated.Shells.TryGetValue(goldenShell.IdShort, out var actualShell))
            {
                report.MissingInGenerated.Add($"Shell: {goldenShell.IdShort}");
                continue;
            }

            if (!CompareIdentifier(goldenShell.AssetGlobalAssetId, actualShell.AssetGlobalAssetId))
            {
                report.DifferentValues.Add($"Shell AssetInformation mismatch: {goldenShell.IdShort} (golden={goldenShell.AssetGlobalAssetId}, generated={actualShell.AssetGlobalAssetId})");
            }
        }

        foreach (var generatedShell in generated.Shells.Values)
        {
            if (!golden.Shells.ContainsKey(generatedShell.IdShort))
            {
                report.ExtraInGenerated.Add($"Shell: {generatedShell.IdShort}");
            }
        }
    }

    private static void CompareSubmodels(Aas3Snapshot golden, Aas3Snapshot generated, Aas3GoldenDiffReport report)
    {
        foreach (var goldenSubmodel in golden.Submodels.Values)
        {
            if (!generated.Submodels.TryGetValue(goldenSubmodel.IdShort, out var actualSubmodel))
            {
                report.MissingInGenerated.Add($"Submodel: {goldenSubmodel.IdShort}");
                continue;
            }

            if (!CompareIdentifier(goldenSubmodel.Id, actualSubmodel.Id))
            {
                report.DifferentValues.Add($"Submodel id mismatch: {goldenSubmodel.IdShort} (golden={goldenSubmodel.Id}, generated={actualSubmodel.Id})");
            }

            if (!CompareKeySequence(goldenSubmodel.SemanticIdKeys, actualSubmodel.SemanticIdKeys))
            {
                report.DifferentValues.Add($"Submodel semanticId mismatch: {goldenSubmodel.IdShort}");
            }
        }

        foreach (var generatedSubmodel in generated.Submodels.Values)
        {
            if (!golden.Submodels.ContainsKey(generatedSubmodel.IdShort))
            {
                report.ExtraInGenerated.Add($"Submodel: {generatedSubmodel.IdShort}");
            }
        }
    }

    private static void CompareSubmodelElements(Aas3Snapshot golden, Aas3Snapshot generated, Aas3GoldenDiffReport report)
    {
        foreach (var (submodelIdShort, goldenSubmodel) in golden.Submodels)
        {
            if (!generated.Submodels.TryGetValue(submodelIdShort, out var actualSubmodel))
            {
                continue;
            }

            foreach (var (path, goldenElement) in goldenSubmodel.Elements)
            {
                if (!actualSubmodel.Elements.TryGetValue(path, out var actualElement))
                {
                    report.MissingInGenerated.Add($"Element: {submodelIdShort}/{path}");
                    continue;
                }

                if (!string.Equals(goldenElement.Kind, actualElement.Kind, StringComparison.Ordinal))
                {
                    report.DifferentValues.Add($"Element kind mismatch: {submodelIdShort}/{path} (golden={goldenElement.Kind}, generated={actualElement.Kind})");
                    continue;
                }

                if (!CompareValues(goldenElement, actualElement))
                {
                    report.DifferentValues.Add($"Element value mismatch: {submodelIdShort}/{path} (golden={goldenElement.Value}, generated={actualElement.Value})");
                }

                if (!CompareKeySequence(goldenElement.ReferenceKeys, actualElement.ReferenceKeys))
                {
                    report.DifferentValues.Add($"ReferenceElement keys mismatch: {submodelIdShort}/{path}");
                }

                if (!CompareRelationship(goldenElement.Relationship, actualElement.Relationship))
                {
                    report.DifferentValues.Add($"Relationship mismatch: {submodelIdShort}/{path}");
                }
            }

            foreach (var (path, actualElement) in actualSubmodel.Elements)
            {
                if (!goldenSubmodel.Elements.ContainsKey(path))
                {
                    report.ExtraInGenerated.Add($"Element: {submodelIdShort}/{path}");
                }
            }
        }
    }

    private static bool CompareValues(ElementSnapshot golden, ElementSnapshot generated)
    {
        if (golden.Value is null && generated.Value is null)
        {
            return true;
        }

        if (golden.Value is null || generated.Value is null)
        {
            return false;
        }

        return string.Equals(golden.Value, generated.Value, StringComparison.Ordinal);
    }

    private static bool CompareRelationship(RelationshipSnapshot? golden, RelationshipSnapshot? generated)
    {
        if (golden is null && generated is null)
        {
            return true;
        }

        if (golden is null || generated is null)
        {
            return false;
        }

        return string.Equals(golden.FirstTarget, generated.FirstTarget, StringComparison.Ordinal)
            && string.Equals(golden.SecondTarget, generated.SecondTarget, StringComparison.Ordinal)
            && CompareKeySequence(golden.FirstKeys, generated.FirstKeys)
            && CompareKeySequence(golden.SecondKeys, generated.SecondKeys);
    }

    private static void CheckReferenceElements(Aas3Snapshot snapshot, Aas3GoldenDiffReport report)
    {
        foreach (var element in snapshot.Submodels.Values.SelectMany(sm => sm.Elements.Values))
        {
            if (!string.Equals(element.Kind, "ReferenceElement", StringComparison.Ordinal))
            {
                continue;
            }

            var keys = element.ReferenceKeys;
            if (keys.Count != 2)
            {
                report.ReferenceIssues.Add($"ReferenceElement key count mismatch: {element.Path}");
                continue;
            }

            if (!string.Equals(keys[0].Type, "Submodel", StringComparison.Ordinal)
                || !string.Equals(keys[1].Type, "Property", StringComparison.Ordinal))
            {
                report.ReferenceIssues.Add($"ReferenceElement key sequence mismatch: {element.Path}");
            }
        }
    }

    private static void CheckRelationshipElements(Aas3Snapshot snapshot, Aas3GoldenDiffReport report)
    {
        foreach (var element in snapshot.Submodels.Values.SelectMany(sm => sm.Elements.Values))
        {
            if (!string.Equals(element.Kind, "RelationshipElement", StringComparison.Ordinal))
            {
                continue;
            }

            if (element.Relationship is null)
            {
                continue;
            }

            if (element.Relationship.FirstTarget?.StartsWith("Ent_", StringComparison.OrdinalIgnoreCase) == true
                || element.Relationship.SecondTarget?.StartsWith("Ent_", StringComparison.OrdinalIgnoreCase) == true)
            {
                report.RelationshipIssues.Add($"Relationship target still has Ent_ prefix: {element.Path}");
            }
        }
    }

    private static bool CompareKeySequence(IReadOnlyList<ReferenceKey> golden, IReadOnlyList<ReferenceKey> generated)
    {
        if (golden.Count == 0 && generated.Count == 0)
        {
            return true;
        }

        if (golden.Count != generated.Count)
        {
            return false;
        }

        for (var i = 0; i < golden.Count; i++)
        {
            var g = golden[i];
            var a = generated[i];
            if (!string.Equals(g.Type, a.Type, StringComparison.Ordinal))
            {
                return false;
            }

            if (!CompareIdentifier(g.Value, a.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CompareIdentifier(string? golden, string? generated)
    {
        if (string.IsNullOrWhiteSpace(golden) && string.IsNullOrWhiteSpace(generated))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(golden) || string.IsNullOrWhiteSpace(generated))
        {
            return false;
        }

        if (TryNormalizeExampleIri(golden, out var goldenNormalized) && TryNormalizeExampleIri(generated, out var generatedNormalized))
        {
            return string.Equals(goldenNormalized, generatedNormalized, StringComparison.Ordinal);
        }

        return string.Equals(golden, generated, StringComparison.Ordinal);
    }

    private static bool TryNormalizeExampleIri(string value, out string normalized)
    {
        normalized = string.Empty;
        var match = ExampleIriPattern.Match(value);
        if (!match.Success)
        {
            return false;
        }

        normalized = $"https://example.com/ids/{match.Groups["type"].Value}/####_####_####_####";
        return true;
    }

    private static void AppendSection(StringBuilder builder, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"  - {item}");
        }
    }
}

public sealed class Aas3GoldenDiffReport
{
    public List<string> MissingInGenerated { get; } = new();
    public List<string> ExtraInGenerated { get; } = new();
    public List<string> DifferentValues { get; } = new();
    public List<string> IdentifierIssues { get; } = new();
    public List<string> ReferenceIssues { get; } = new();
    public List<string> RelationshipIssues { get; } = new();
    public bool HasDiffs =>
        MissingInGenerated.Count > 0
        || ExtraInGenerated.Count > 0
        || DifferentValues.Count > 0
        || IdentifierIssues.Count > 0
        || ReferenceIssues.Count > 0
        || RelationshipIssues.Count > 0;
}

internal sealed class Aas3Snapshot
{
    public Dictionary<string, ShellSnapshot> Shells { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, SubmodelSnapshot> Submodels { get; } = new(StringComparer.Ordinal);

    public static Aas3Snapshot FromXml(string path)
    {
        var doc = XDocument.Load(path);
        var snapshot = new Aas3Snapshot();

        foreach (var shell in doc.Descendants().Where(e => e.Name.LocalName == "assetAdministrationShell"))
        {
            var idShort = GetChildValue(shell, "idShort");
            if (string.IsNullOrWhiteSpace(idShort))
            {
                continue;
            }

            var id = GetChildValue(shell, "id");
            var assetInfo = shell.Elements().FirstOrDefault(e => e.Name.LocalName == "assetInformation");
            var globalAssetId = assetInfo?.Elements().FirstOrDefault(e => e.Name.LocalName == "globalAssetId")?.Value;
            snapshot.Shells[idShort] = new ShellSnapshot(idShort, id, globalAssetId);
        }

        foreach (var submodel in doc.Descendants().Where(e => e.Name.LocalName == "submodel"))
        {
            var idShort = GetChildValue(submodel, "idShort");
            if (string.IsNullOrWhiteSpace(idShort))
            {
                continue;
            }

            var id = GetChildValue(submodel, "id");
            var semanticIdKeys = ExtractReferenceKeys(submodel.Elements().FirstOrDefault(e => e.Name.LocalName == "semanticId"));
            var submodelSnapshot = new SubmodelSnapshot(idShort, id, semanticIdKeys);
            var elementsRoot = submodel.Elements().FirstOrDefault(e => e.Name.LocalName == "submodelElements");
            if (elementsRoot is not null)
            {
                foreach (var child in elementsRoot.Elements())
                {
                    ParseElement(child, string.Empty, submodelSnapshot);
                }
            }

            snapshot.Submodels[idShort] = submodelSnapshot;
        }

        return snapshot;
    }

    public static Aas3Snapshot FromJson(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var snapshot = new Aas3Snapshot();

        if (root.TryGetProperty("assetAdministrationShells", out var shells))
        {
            foreach (var shell in shells.EnumerateArray())
            {
                var idShort = GetJsonString(shell, "idShort");
                if (string.IsNullOrWhiteSpace(idShort))
                {
                    continue;
                }

                var id = GetJsonString(shell, "id");
                var globalAssetId = shell.TryGetProperty("assetInformation", out var assetInfo)
                    ? GetJsonString(assetInfo, "globalAssetId")
                    : null;
                snapshot.Shells[idShort] = new ShellSnapshot(idShort, id, globalAssetId);
            }
        }

        if (root.TryGetProperty("submodels", out var submodels))
        {
            foreach (var submodel in submodels.EnumerateArray())
            {
                var idShort = GetJsonString(submodel, "idShort");
                if (string.IsNullOrWhiteSpace(idShort))
                {
                    continue;
                }

                var id = GetJsonString(submodel, "id");
                var semanticIdKeys = ExtractReferenceKeys(submodel.TryGetProperty("semanticId", out var semanticId) ? semanticId : default);
                var snapshotItem = new SubmodelSnapshot(idShort, id, semanticIdKeys);
                if (submodel.TryGetProperty("submodelElements", out var elements))
                {
                    foreach (var element in elements.EnumerateArray())
                    {
                        ParseElement(element, string.Empty, snapshotItem);
                    }
                }

                snapshot.Submodels[idShort] = snapshotItem;
            }
        }

        return snapshot;
    }

    public IEnumerable<string> GetAllIdentifiers()
    {
        foreach (var shell in Shells.Values)
        {
            if (!string.IsNullOrWhiteSpace(shell.Id))
            {
                yield return shell.Id!;
            }

            if (!string.IsNullOrWhiteSpace(shell.AssetGlobalAssetId))
            {
                yield return shell.AssetGlobalAssetId!;
            }
        }

        foreach (var submodel in Submodels.Values)
        {
            if (!string.IsNullOrWhiteSpace(submodel.Id))
            {
                yield return submodel.Id!;
            }
        }
    }

    private static void ParseElement(XElement element, string pathPrefix, SubmodelSnapshot submodel)
    {
        var kind = NormalizeKind(element.Name.LocalName);
        var idShort = GetChildValue(element, "idShort");
        if (string.IsNullOrWhiteSpace(idShort))
        {
            return;
        }

        var path = string.IsNullOrWhiteSpace(pathPrefix) ? idShort : $"{pathPrefix}/{idShort}";
        var snapshot = new ElementSnapshot(path, kind);

        switch (kind)
        {
            case "Property":
                snapshot.Value = GetChildValue(element, "value");
                break;
            case "MultiLanguageProperty":
                snapshot.Value = ExtractMultiLanguageValue(element.Elements().FirstOrDefault(e => e.Name.LocalName == "value"));
                break;
            case "File":
                snapshot.Value = GetChildValue(element, "value");
                break;
            case "ReferenceElement":
                snapshot.ReferenceKeys = ExtractReferenceKeys(element);
                break;
            case "RelationshipElement":
                snapshot.Relationship = ExtractRelationship(element);
                break;
        }

        submodel.Elements[path] = snapshot;

        if (kind == "SubmodelElementCollection" || kind == "SubmodelElementList")
        {
            var valueElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
            if (valueElement is not null)
            {
                foreach (var child in valueElement.Elements())
                {
                    ParseElement(child, path, submodel);
                }
            }
        }
    }

    private static void ParseElement(JsonElement element, string pathPrefix, SubmodelSnapshot submodel)
    {
        var idShort = GetJsonString(element, "idShort");
        if (string.IsNullOrWhiteSpace(idShort))
        {
            return;
        }

        var kind = NormalizeKind(GetJsonModelType(element));
        var path = string.IsNullOrWhiteSpace(pathPrefix) ? idShort : $"{pathPrefix}/{idShort}";
        var snapshot = new ElementSnapshot(path, kind);

        switch (kind)
        {
            case "Property":
                snapshot.Value = GetJsonString(element, "value");
                break;
            case "MultiLanguageProperty":
                if (element.TryGetProperty("value", out var mlpValue))
                {
                    snapshot.Value = ExtractMultiLanguageValue(mlpValue);
                }
                break;
            case "File":
                snapshot.Value = GetJsonString(element, "value");
                break;
            case "ReferenceElement":
                if (element.TryGetProperty("value", out var referenceValue))
                {
                    snapshot.ReferenceKeys = ExtractReferenceKeys(referenceValue);
                }
                break;
            case "RelationshipElement":
                snapshot.Relationship = ExtractRelationship(element);
                break;
        }

        submodel.Elements[path] = snapshot;

        if (kind == "SubmodelElementCollection" || kind == "SubmodelElementList")
        {
            if (element.TryGetProperty("value", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    ParseElement(child, path, submodel);
                }
            }
        }
    }

    private static string NormalizeKind(string raw)
    {
        return raw switch
        {
            "property" => "Property",
            "multiLanguageProperty" => "MultiLanguageProperty",
            "file" => "File",
            "referenceElement" => "ReferenceElement",
            "relationshipElement" => "RelationshipElement",
            "entity" => "Entity",
            "submodelElementCollection" => "SubmodelElementCollection",
            "submodelElementList" => "SubmodelElementList",
            _ => raw
        };
    }

    private static string? GetChildValue(XElement parent, string childName)
    {
        return parent.Elements().FirstOrDefault(e => e.Name.LocalName == childName)?.Value;
    }

    private static List<ReferenceKey> ExtractReferenceKeys(XElement? referenceElement)
    {
        if (referenceElement is null)
        {
            return new List<ReferenceKey>();
        }

        var keys = referenceElement.Descendants().Where(e => e.Name.LocalName == "key").Select(key =>
        {
            var type = key.Attribute("type")?.Value ?? key.Elements().FirstOrDefault(e => e.Name.LocalName == "type")?.Value ?? string.Empty;
            var value = key.Attribute("value")?.Value ?? key.Elements().FirstOrDefault(e => e.Name.LocalName == "value")?.Value ?? string.Empty;
            return new ReferenceKey(type, value);
        }).Where(k => !string.IsNullOrWhiteSpace(k.Type) || !string.IsNullOrWhiteSpace(k.Value)).ToList();

        return keys;
    }

    private static List<ReferenceKey> ExtractReferenceKeys(JsonElement referenceElement)
    {
        if (referenceElement.ValueKind == JsonValueKind.Undefined)
        {
            return new List<ReferenceKey>();
        }

        if (!referenceElement.TryGetProperty("keys", out var keys))
        {
            return new List<ReferenceKey>();
        }

        var list = new List<ReferenceKey>();
        foreach (var key in keys.EnumerateArray())
        {
            var type = GetJsonString(key, "type") ?? string.Empty;
            var value = GetJsonString(key, "value") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(type) || !string.IsNullOrWhiteSpace(value))
            {
                list.Add(new ReferenceKey(type, value));
            }
        }

        return list;
    }

    private static RelationshipSnapshot? ExtractRelationship(XElement element)
    {
        var first = element.Elements().FirstOrDefault(e => e.Name.LocalName == "first");
        var second = element.Elements().FirstOrDefault(e => e.Name.LocalName == "second");
        var firstKeys = ExtractReferenceKeys(first);
        var secondKeys = ExtractReferenceKeys(second);
        if (firstKeys.Count == 0 && secondKeys.Count == 0)
        {
            return null;
        }

        return new RelationshipSnapshot(
            firstKeys,
            secondKeys,
            firstKeys.LastOrDefault(k => string.Equals(k.Type, "Entity", StringComparison.Ordinal))?.Value,
            secondKeys.LastOrDefault(k => string.Equals(k.Type, "Entity", StringComparison.Ordinal))?.Value);
    }

    private static RelationshipSnapshot? ExtractRelationship(JsonElement element)
    {
        if (!element.TryGetProperty("first", out var first) && !element.TryGetProperty("second", out var second))
        {
            return null;
        }

        element.TryGetProperty("first", out first);
        element.TryGetProperty("second", out second);

        var firstKeys = ExtractReferenceKeys(first);
        var secondKeys = ExtractReferenceKeys(second);
        if (firstKeys.Count == 0 && secondKeys.Count == 0)
        {
            return null;
        }

        return new RelationshipSnapshot(
            firstKeys,
            secondKeys,
            firstKeys.LastOrDefault(k => string.Equals(k.Type, "Entity", StringComparison.Ordinal))?.Value,
            secondKeys.LastOrDefault(k => string.Equals(k.Type, "Entity", StringComparison.Ordinal))?.Value);
    }

    private static string ExtractMultiLanguageValue(XElement? valueElement)
    {
        if (valueElement is null)
        {
            return string.Empty;
        }

        var entries = valueElement.Descendants()
            .Where(e => e.Name.LocalName is "langString" or "langStringTextType")
            .Select(e =>
            {
                var lang = e.Attribute("lang")?.Value
                    ?? e.Elements().FirstOrDefault(child => child.Name.LocalName == "language")?.Value
                    ?? string.Empty;
                var text = e.Name.LocalName == "langStringTextType"
                    ? e.Elements().FirstOrDefault(child => child.Name.LocalName == "text")?.Value ?? string.Empty
                    : e.Value ?? string.Empty;
                return $"{lang}:{text}";
            })
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return string.Join("|", entries);
    }

    private static string ExtractMultiLanguageValue(JsonElement valueElement)
    {
        if (valueElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var entries = new List<string>();
        foreach (var item in valueElement.EnumerateArray())
        {
            var lang = GetJsonString(item, "language") ?? GetJsonString(item, "lang") ?? string.Empty;
            var text = GetJsonString(item, "text") ?? string.Empty;
            entries.Add($"{lang}:{text}");
        }

        entries.Sort(StringComparer.Ordinal);
        return string.Join("|", entries);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static string GetJsonModelType(JsonElement element)
    {
        if (element.TryGetProperty("modelType", out var modelType))
        {
            if (modelType.ValueKind == JsonValueKind.Object && modelType.TryGetProperty("name", out var name))
            {
                return name.GetString() ?? string.Empty;
            }

            if (modelType.ValueKind == JsonValueKind.String)
            {
                return modelType.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}

public sealed record ShellSnapshot(string IdShort, string? Id, string? AssetGlobalAssetId);

public sealed class SubmodelSnapshot
{
    public SubmodelSnapshot(string idShort, string? id, List<ReferenceKey> semanticIdKeys)
    {
        IdShort = idShort;
        Id = id;
        SemanticIdKeys = semanticIdKeys;
    }

    public string IdShort { get; }
    public string? Id { get; }
    public List<ReferenceKey> SemanticIdKeys { get; }
    public Dictionary<string, ElementSnapshot> Elements { get; } = new(StringComparer.Ordinal);
}

public sealed class ElementSnapshot
{
    public ElementSnapshot(string path, string kind)
    {
        Path = path;
        Kind = kind;
    }

    public string Path { get; }
    public string Kind { get; }
    public string? Value { get; set; }
    public List<ReferenceKey> ReferenceKeys { get; set; } = new();
    public RelationshipSnapshot? Relationship { get; set; }
}

public sealed record ReferenceKey(string Type, string Value);

public sealed record RelationshipSnapshot(
    List<ReferenceKey> FirstKeys,
    List<ReferenceKey> SecondKeys,
    string? FirstTarget,
    string? SecondTarget
);
