using System;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace AasExcelToXml.Core;

public static class Aas3GoldenDiffAnalyzer
{
    private static readonly string[] TargetElements =
    {
        "environment",
        "assetAdministrationShell",
        "assetInformation",
        "submodels",
        "submodel",
        "submodelElements",
        "submodelElementCollection",
        "property",
        "referenceElement",
        "relationshipElement",
        "entity",
        "file",
        "multiLanguageProperty",
        "conceptDescription"
    };

    public static Aas3GoldenDiffReport Analyze(string goldenPath, string actualPath)
    {
        var golden = XDocument.Load(goldenPath, LoadOptions.PreserveWhitespace);
        var actual = XDocument.Load(actualPath, LoadOptions.PreserveWhitespace);

        var report = new Aas3GoldenDiffReport();
        var goldenRules = ExtractRules(golden);
        CheckMissingElements(actual, goldenRules, report);
        CheckMissingValueTypes(actual, report);
        return report;
    }

    public static string BuildSummary(Aas3GoldenDiffReport report)
    {
        var builder = new List<string>
        {
            $"- AAS3 구조 누락: {report.MissingElementIssues.Count}",
            $"- AAS3 valueType 문제: {report.ValueTypeIssues.Count}"
        };

        if (report.MissingElementIssues.Count > 0)
        {
            builder.Add("  - 누락 항목 예시:");
            builder.AddRange(report.MissingElementIssues.Take(5).Select(issue => $"    - {issue}"));
        }

        if (report.ValueTypeIssues.Count > 0)
        {
            builder.Add("  - valueType 문제 예시:");
            builder.AddRange(report.ValueTypeIssues.Take(5).Select(issue => $"    - {issue}"));
        }

        return string.Join(Environment.NewLine, builder);
    }

    public static string ToJson(Aas3GoldenDiffReport report)
    {
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static Dictionary<string, HashSet<string>> ExtractRules(XDocument golden)
    {
        var rules = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in TargetElements)
        {
            var element = golden.Descendants().FirstOrDefault(e => e.Name.LocalName == name);
            if (element is null)
            {
                continue;
            }

            var children = element.Elements()
                .Select(e => e.Name.LocalName)
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            rules[name] = children;
        }

        return rules;
    }

    private static void CheckMissingElements(XDocument actual, Dictionary<string, HashSet<string>> goldenRules, Aas3GoldenDiffReport report)
    {
        foreach (var rule in goldenRules)
        {
            var actualElements = actual.Descendants().Where(e => e.Name.LocalName == rule.Key).ToList();
            if (actualElements.Count == 0)
            {
                report.MissingElementIssues.Add($"{rule.Key} 요소를 찾을 수 없습니다.");
                continue;
            }

            foreach (var element in actualElements)
            {
                foreach (var requiredChild in rule.Value)
                {
                    if (!element.Elements().Any(e => e.Name.LocalName == requiredChild))
                    {
                        report.MissingElementIssues.Add($"{rule.Key} 요소에 {requiredChild} 하위 노드가 없습니다.");
                    }
                }
            }
        }
    }

    private static void CheckMissingValueTypes(XDocument actual, Aas3GoldenDiffReport report)
    {
        foreach (var property in actual.Descendants().Where(e => e.Name.LocalName == "property"))
        {
            var valueType = property.Elements().FirstOrDefault(e => e.Name.LocalName == "valueType");
            if (valueType is null)
            {
                report.ValueTypeIssues.Add("property에 valueType이 없습니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(valueType.Value))
            {
                report.ValueTypeIssues.Add("property valueType 값이 비어 있습니다.");
                continue;
            }
        }
    }
}

public sealed class Aas3GoldenDiffReport
{
    public List<string> MissingElementIssues { get; } = new();
    public List<string> ValueTypeIssues { get; } = new();
}
