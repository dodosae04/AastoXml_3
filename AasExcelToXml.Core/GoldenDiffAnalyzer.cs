using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace AasExcelToXml.Core;

public static class GoldenDiffAnalyzer
{
    public static GoldenDiffReport Analyze(string goldenPath, string actualPath)
    {
        var golden = XDocument.Load(goldenPath, LoadOptions.PreserveWhitespace);
        var actual = XDocument.Load(actualPath, LoadOptions.PreserveWhitespace);

        var report = new GoldenDiffReport();

        CheckQualifiers(actual, report);
        CheckPlaceholders(actual, report);
        CheckReferenceElements(actual, report);
        CompareShellIdShorts(golden, actual, report);
        CheckDocumentationStructure(golden, actual, report);
        CompareStructureOrder(golden.Root, actual.Root, report);

        return report;
    }

    public static string BuildSummary(GoldenDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AAS 정답 비교 요약");
        builder.AppendLine($"- qualifier 구조 문제: {report.QualifierIssues.Count}");
        builder.AppendLine($"- placeholder 누락: {report.PlaceholderIssues.Count}");
        builder.AppendLine($"- ReferenceElement 구조 오류: {report.ReferenceIssues.Count}");
        builder.AppendLine($"- Documentation 구조 오류: {report.DocumentationIssues.Count}");
        builder.AppendLine($"- 구조/순서 불일치: {report.StructureIssues.Count}");
        return builder.ToString();
    }

    public static string ToJson(GoldenDiffReport report)
    {
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static void CheckQualifiers(XDocument actual, GoldenDiffReport report)
    {
        foreach (var qualifiers in actual.Descendants().Where(e => e.Name.LocalName == "qualifiers"))
        {
            if (!qualifiers.Elements().Any(e => e.Name.LocalName == "qualifier"))
            {
                report.QualifierIssues.Add("qualifiers 아래에 qualifier가 없습니다.");
                break;
            }
        }
    }

    private static void CheckPlaceholders(XDocument actual, GoldenDiffReport report)
    {
        foreach (var elementName in new[] { "assetIdentificationModelRef", "billOfMaterialRef" })
        {
            foreach (var element in actual.Descendants().Where(e => e.Name.LocalName == elementName))
            {
                if (!element.Elements().Any(e => e.Name.LocalName == "keys"))
                {
                    report.PlaceholderIssues.Add($"{elementName}에 keys가 없습니다.");
                }
            }
        }

        foreach (var property in actual.Descendants().Where(e => e.Name.LocalName == "property"))
        {
            var valueId = property.Elements().FirstOrDefault(e => e.Name.LocalName == "valueId");
            if (valueId is not null && !valueId.Elements().Any(e => e.Name.LocalName == "keys"))
            {
                report.PlaceholderIssues.Add("property.valueId가 있으나 keys가 없습니다.");
                break;
            }
        }

        foreach (var collection in actual.Descendants().Where(e => e.Name.LocalName == "submodelElementCollection"))
        {
            if (collection.Elements().All(e => e.Name.LocalName != "ordered"))
            {
                report.PlaceholderIssues.Add("SMC ordered 필드가 누락되었습니다.");
                break;
            }
            if (collection.Elements().All(e => e.Name.LocalName != "allowDuplicates"))
            {
                report.PlaceholderIssues.Add("SMC allowDuplicates 필드가 누락되었습니다.");
                break;
            }
        }
    }

    private static void CheckReferenceElements(XDocument actual, GoldenDiffReport report)
    {
        foreach (var referenceElement in actual.Descendants().Where(e => e.Name.LocalName == "referenceElement"))
        {
            var keys = referenceElement.Descendants().Where(e => e.Name.LocalName == "key").ToList();
            if (keys.Count != 2)
            {
                report.ReferenceIssues.Add("ReferenceElement가 2-key 구조가 아닙니다.");
                break;
            }

            var firstType = keys[0].Attribute("type")?.Value;
            var secondType = keys[1].Attribute("type")?.Value;
            if (!string.Equals(firstType, "Submodel", StringComparison.Ordinal) || !string.Equals(secondType, "Property", StringComparison.Ordinal))
            {
                report.ReferenceIssues.Add("ReferenceElement key type 순서가 Submodel/Property가 아닙니다.");
                break;
            }
        }
    }

    private static void CheckDocumentationStructure(XDocument golden, XDocument actual, GoldenDiffReport report)
    {
        var goldenSnapshot = DocumentationSnapshot.Extract(golden);
        var actualSnapshot = DocumentationSnapshot.Extract(actual);
        if (goldenSnapshot is null || actualSnapshot is null)
        {
            report.DocumentationIssues.Add("Documentation Submodel을 찾을 수 없습니다.");
            return;
        }

        var goldenDocs = goldenSnapshot.DocumentCollections;
        var actualDocs = actualSnapshot.DocumentCollections;

        var goldenIds = new HashSet<string>(goldenDocs.Select(d => d.IdShort), StringComparer.Ordinal);
        var actualIds = new HashSet<string>(actualDocs.Select(d => d.IdShort), StringComparer.Ordinal);
        foreach (var id in goldenIds.Except(actualIds))
        {
            report.DocumentationIssues.Add($"Documentation 문서 누락: {id}");
        }

        foreach (var id in actualIds.Except(goldenIds))
        {
            report.DocumentationIssues.Add($"Documentation 문서 추가됨: {id}");
        }

        foreach (var goldenDoc in goldenDocs)
        {
            var scope = $"Documentation/{goldenDoc.IdShort}";
            var actualDoc = actualDocs.FirstOrDefault(d => string.Equals(d.IdShort, goldenDoc.IdShort, StringComparison.Ordinal));
            if (actualDoc is null)
            {
                continue;
            }

            if (goldenDoc.Ordered.HasValue && actualDoc.Ordered != goldenDoc.Ordered)
            {
                report.DocumentationIssues.Add($"{scope} ordered 불일치: 정답={goldenDoc.Ordered}, 출력={actualDoc.Ordered}");
            }

            if (goldenDoc.AllowDuplicates.HasValue && actualDoc.AllowDuplicates != goldenDoc.AllowDuplicates)
            {
                report.DocumentationIssues.Add($"{scope} allowDuplicates 불일치: 정답={goldenDoc.AllowDuplicates}, 출력={actualDoc.AllowDuplicates}");
            }

            if (!string.Equals(goldenDoc.DocumentVersionIdShort, actualDoc.DocumentVersionIdShort, StringComparison.Ordinal))
            {
                report.DocumentationIssues.Add($"{scope} DocumentVersion idShort 불일치: 정답={goldenDoc.DocumentVersionIdShort}, 출력={actualDoc.DocumentVersionIdShort}");
            }

            CompareElementList(goldenDoc.DocumentElements, actualDoc.DocumentElements, scope, report);
            if (goldenDoc.VersionElements.Count > 0 || actualDoc.VersionElements.Count > 0)
            {
                CompareElementList(goldenDoc.VersionElements, actualDoc.VersionElements, $"{scope}/{goldenDoc.DocumentVersionIdShort}", report);
            }
        }
    }

    private static void CompareShellIdShorts(XDocument golden, XDocument actual, GoldenDiffReport report)
    {
        var goldenShells = ExtractShellIdShorts(golden);
        var actualShells = ExtractShellIdShorts(actual);

        foreach (var idShort in goldenShells.Except(actualShells))
        {
            report.StructureIssues.Add($"AAS Shell 누락: {idShort}");
        }

        foreach (var idShort in actualShells.Except(goldenShells))
        {
            report.StructureIssues.Add($"AAS Shell 추가됨: {idShort}");
        }
    }

    private static HashSet<string> ExtractShellIdShorts(XDocument document)
    {
        return document.Descendants()
            .Where(e => e.Name.LocalName == "assetAdministrationShell")
            .Select(e => GetChildValue(e, "idShort"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }

    private static void CompareStructureOrder(XElement? goldenRoot, XElement? actualRoot, GoldenDiffReport report)
    {
        if (goldenRoot is null || actualRoot is null)
        {
            report.StructureIssues.Add("루트 요소 비교에 실패했습니다.");
            return;
        }

        CompareElementOrder(goldenRoot, actualRoot, "/" + goldenRoot.Name.LocalName, report);
    }

    private static void CompareElementOrder(XElement golden, XElement actual, string path, GoldenDiffReport report)
    {
        var goldenChildren = golden.Elements().ToList();
        var actualChildren = actual.Elements().ToList();

        var max = Math.Max(goldenChildren.Count, actualChildren.Count);
        for (var i = 0; i < max; i++)
        {
            if (i >= goldenChildren.Count)
            {
                report.StructureIssues.Add($"{path}: 정답에 없는 요소가 출력됨 -> {actualChildren[i].Name.LocalName}");
                continue;
            }

            if (i >= actualChildren.Count)
            {
                report.StructureIssues.Add($"{path}: 출력에 누락된 요소 -> {goldenChildren[i].Name.LocalName}");
                continue;
            }

            var goldenName = goldenChildren[i].Name.LocalName;
            var actualName = actualChildren[i].Name.LocalName;
            if (!string.Equals(goldenName, actualName, StringComparison.Ordinal))
            {
                report.StructureIssues.Add($"{path}: 요소 순서 불일치(정답={goldenName}, 출력={actualName})");
                continue;
            }

            CompareElementOrder(goldenChildren[i], actualChildren[i], $"{path}/{goldenName}", report);
        }
    }

    private static void CompareElementList(IReadOnlyList<DocumentationElementSnapshot> goldenElements, IReadOnlyList<DocumentationElementSnapshot> actualElements, string scope, GoldenDiffReport report)
    {
        if (goldenElements.Count == 0)
        {
            report.DocumentationIssues.Add($"{scope} 정답 필드가 비어 있습니다.");
            return;
        }

        if (actualElements.Count == 0)
        {
            report.DocumentationIssues.Add($"{scope} 출력 필드가 비어 있습니다.");
            return;
        }

        var minCount = Math.Min(goldenElements.Count, actualElements.Count);
        if (goldenElements.Count != actualElements.Count)
        {
            report.DocumentationIssues.Add($"{scope} 필드 개수 불일치: 정답={goldenElements.Count}, 출력={actualElements.Count}");
        }

        for (var i = 0; i < minCount; i++)
        {
            var golden = goldenElements[i];
            var actual = actualElements[i];
            var label = $"{scope}[{i}]";

            if (!string.Equals(golden.IdShort, actual.IdShort, StringComparison.Ordinal))
            {
                report.DocumentationIssues.Add($"{label} idShort 불일치: 정답={golden.IdShort}, 출력={actual.IdShort}");
            }

            if (!string.Equals(golden.Kind, actual.Kind, StringComparison.Ordinal))
            {
                report.DocumentationIssues.Add($"{label} 타입 불일치: 정답={golden.Kind}, 출력={actual.Kind}");
            }

            if (!string.Equals(golden.Category, actual.Category, StringComparison.Ordinal))
            {
                report.DocumentationIssues.Add($"{label} category 불일치: 정답={golden.Category}, 출력={actual.Category}");
            }

            if (golden.Ordered.HasValue && actual.Ordered != golden.Ordered)
            {
                report.DocumentationIssues.Add($"{label} ordered 불일치: 정답={golden.Ordered}, 출력={actual.Ordered}");
            }

            if (golden.AllowDuplicates.HasValue && actual.AllowDuplicates != golden.AllowDuplicates)
            {
                report.DocumentationIssues.Add($"{label} allowDuplicates 불일치: 정답={golden.AllowDuplicates}, 출력={actual.AllowDuplicates}");
            }

            if (golden.HasQualifier != actual.HasQualifier)
            {
                report.DocumentationIssues.Add($"{label} qualifier 존재 여부 불일치: 정답={golden.HasQualifier}, 출력={actual.HasQualifier}");
            }

            if (!CompareKeys(golden.SemanticIdKeys, actual.SemanticIdKeys))
            {
                report.DocumentationIssues.Add($"{label} semanticId 키 불일치");
            }

            if (!CompareKeys(golden.ValueIdKeys, actual.ValueIdKeys))
            {
                report.DocumentationIssues.Add($"{label} valueId 키 불일치");
            }

            if (golden.ValueTypeIsEmpty && !actual.ValueTypeIsEmpty)
            {
                report.DocumentationIssues.Add($"{label} valueType 빈 태그가 아닙니다.");
            }

            if (!string.IsNullOrWhiteSpace(golden.ValueType) && !string.Equals(golden.ValueType, actual.ValueType, StringComparison.Ordinal))
            {
                report.DocumentationIssues.Add($"{label} valueType 값 불일치: 정답={golden.ValueType}, 출력={actual.ValueType}");
            }

            if (!CompareLanguages(golden.Languages, actual.Languages))
            {
                report.DocumentationIssues.Add($"{label} multiLanguageProperty 언어 구성 불일치");
            }
        }
    }

    private static bool CompareKeys(IReadOnlyList<DocumentationKeySnapshot> golden, IReadOnlyList<DocumentationKeySnapshot> actual)
    {
        if (golden.Count != actual.Count)
        {
            return false;
        }

        for (var i = 0; i < golden.Count; i++)
        {
            var left = golden[i];
            var right = actual[i];
            if (!string.Equals(left.Type, right.Type, StringComparison.Ordinal)
                || !string.Equals(left.IdType, right.IdType, StringComparison.Ordinal)
                || left.Local != right.Local
                || !string.Equals(left.Value, right.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CompareLanguages(IReadOnlyList<string> golden, IReadOnlyList<string> actual)
    {
        if (golden.Count != actual.Count)
        {
            return false;
        }

        for (var i = 0; i < golden.Count; i++)
        {
            if (!string.Equals(golden[i], actual[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class DocumentationSnapshot
{
    public List<DocumentationCollectionSnapshot> DocumentCollections { get; } = new();

    public static DocumentationSnapshot? Extract(XDocument document)
    {
        var documentation = document.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "submodel"
            && string.Equals(GetChildValue(e, "idShort"), "Documentation", StringComparison.Ordinal));

        if (documentation is null)
        {
            return null;
        }

        var snapshot = new DocumentationSnapshot();
        var docCollections = documentation.Descendants().Where(e =>
            e.Name.LocalName == "submodelElementCollection"
            && GetChildValue(e, "idShort").StartsWith("Document", StringComparison.Ordinal)
            && !GetChildValue(e, "idShort").StartsWith("DocumentVersion", StringComparison.Ordinal));

        foreach (var collection in docCollections)
        {
            var docSnapshot = DocumentationCollectionSnapshot.FromCollection(collection);
            if (docSnapshot is not null)
            {
                snapshot.DocumentCollections.Add(docSnapshot);
            }
        }

        return snapshot;
    }

    private static IEnumerable<DocumentationElementSnapshot> ExtractChildren(XElement collection)
    {
        foreach (var child in GetValueChildren(collection))
        {
            var snapshot = DocumentationElementSnapshot.FromElement(child);
            if (snapshot is not null)
            {
                yield return snapshot;
            }
        }
    }

    private static IEnumerable<XElement> GetValueChildren(XElement element)
    {
        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (value is null)
        {
            yield break;
        }

        foreach (var child in value.Elements())
        {
            if (child.Name.LocalName == "submodelElement")
            {
                var inner = child.Elements().FirstOrDefault();
                if (inner is not null)
                {
                    yield return inner;
                }

                continue;
            }

            yield return child;
        }
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }
}

internal sealed class DocumentationCollectionSnapshot
{
    public string IdShort { get; init; } = string.Empty;
    public string DocumentVersionIdShort { get; set; } = string.Empty;
    public bool? Ordered { get; init; }
    public bool? AllowDuplicates { get; init; }
    public List<DocumentationElementSnapshot> DocumentElements { get; } = new();
    public List<DocumentationElementSnapshot> VersionElements { get; } = new();

    public static DocumentationCollectionSnapshot? FromCollection(XElement collection)
    {
        var idShort = GetChildValue(collection, "idShort");
        if (string.IsNullOrWhiteSpace(idShort))
        {
            return null;
        }

        var snapshot = new DocumentationCollectionSnapshot
        {
            IdShort = idShort,
            Ordered = DocumentationElementSnapshot.ParseBool(GetChildValue(collection, "ordered")),
            AllowDuplicates = DocumentationElementSnapshot.ParseBool(GetChildValue(collection, "allowDuplicates"))
        };
        foreach (var child in GetValueChildren(collection))
        {
            if (child.Name.LocalName == "submodelElementCollection"
                && GetChildValue(child, "idShort").StartsWith("DocumentVersion", StringComparison.Ordinal))
            {
                snapshot.DocumentVersionIdShort = GetChildValue(child, "idShort");
                snapshot.VersionElements.AddRange(ExtractChildren(child));
                continue;
            }

            var elementSnapshot = DocumentationElementSnapshot.FromElement(child);
            if (elementSnapshot is not null)
            {
                snapshot.DocumentElements.Add(elementSnapshot);
            }
        }

        return snapshot;
    }

    private static IEnumerable<DocumentationElementSnapshot> ExtractChildren(XElement collection)
    {
        foreach (var child in GetValueChildren(collection))
        {
            var snapshot = DocumentationElementSnapshot.FromElement(child);
            if (snapshot is not null)
            {
                yield return snapshot;
            }
        }
    }

    private static IEnumerable<XElement> GetValueChildren(XElement element)
    {
        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (value is null)
        {
            yield break;
        }

        foreach (var child in value.Elements())
        {
            if (child.Name.LocalName == "submodelElement")
            {
                var inner = child.Elements().FirstOrDefault();
                if (inner is not null)
                {
                    yield return inner;
                }

                continue;
            }

            yield return child;
        }
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }
}

internal sealed class DocumentationElementSnapshot
{
    public string Kind { get; init; } = string.Empty;
    public string IdShort { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool? Ordered { get; init; }
    public bool? AllowDuplicates { get; init; }
    public bool HasQualifier { get; init; }
    public bool ValueTypeIsEmpty { get; init; }
    public string? ValueType { get; init; }
    public List<DocumentationKeySnapshot> SemanticIdKeys { get; init; } = new();
    public List<DocumentationKeySnapshot> ValueIdKeys { get; init; } = new();
    public List<string> Languages { get; init; } = new();

    public static DocumentationElementSnapshot? FromElement(XElement element)
    {
        var kind = element.Name.LocalName;
        if (kind is not ("property" or "file" or "multiLanguageProperty" or "submodelElementCollection"))
        {
            return null;
        }

        var valueTypeElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "valueType");
        var valueTypeIsEmpty = valueTypeElement is not null && string.IsNullOrWhiteSpace(valueTypeElement.Value);
        var valueType = valueTypeElement is not null && !string.IsNullOrWhiteSpace(valueTypeElement.Value)
            ? valueTypeElement.Value.Trim()
            : null;

        return new DocumentationElementSnapshot
        {
            Kind = kind,
            IdShort = GetChildValue(element, "idShort"),
            Category = GetChildValue(element, "category"),
            Ordered = ParseBool(GetChildValue(element, "ordered")),
            AllowDuplicates = ParseBool(GetChildValue(element, "allowDuplicates")),
            HasQualifier = element.Elements().Any(e => e.Name.LocalName == "qualifier"),
            ValueTypeIsEmpty = valueTypeIsEmpty,
            ValueType = valueType,
            SemanticIdKeys = ParseKeys(element, "semanticId"),
            ValueIdKeys = ParseKeys(element, "valueId"),
            Languages = ParseLanguages(element)
        };
    }

    private static List<DocumentationKeySnapshot> ParseKeys(XElement element, string name)
    {
        var target = element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (target is null)
        {
            return new List<DocumentationKeySnapshot>();
        }

        return target.Descendants().Where(e => e.Name.LocalName == "key")
            .Select(key => new DocumentationKeySnapshot(
                key.Attribute("type")?.Value ?? string.Empty,
                key.Attribute("idType")?.Value ?? string.Empty,
                string.Equals(key.Attribute("local")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                key.Value))
            .ToList();
    }

    private static List<string> ParseLanguages(XElement element)
    {
        if (element.Name.LocalName != "multiLanguageProperty")
        {
            return new List<string>();
        }

        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (value is null)
        {
            return new List<string>();
        }

        return value.Elements().Where(e => e.Name.LocalName == "langString")
            .Select(e => e.Attribute("lang")?.Value ?? string.Empty)
            .ToList();
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }

    internal static bool? ParseBool(string value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}

internal sealed record DocumentationKeySnapshot(string Type, string IdType, bool Local, string Value);

public sealed class GoldenDiffReport
{
    public List<string> QualifierIssues { get; } = new();
    public List<string> PlaceholderIssues { get; } = new();
    public List<string> ReferenceIssues { get; } = new();
    public List<string> DocumentationIssues { get; } = new();
    public List<string> StructureIssues { get; } = new();
}
