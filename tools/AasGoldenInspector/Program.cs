using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace AasGoldenInspector;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("사용법: dotnet run --project tools/AasGoldenInspector -- <AAS2_XML> <AAS3_XML>");
            return 1;
        }

        var aas2Path = args[0];
        var aas3Path = args[1];

        if (!File.Exists(aas2Path))
        {
            Console.Error.WriteLine($"AAS 2.0 정답 XML을 찾을 수 없습니다: {aas2Path}");
            return 1;
        }

        if (!File.Exists(aas3Path))
        {
            Console.Error.WriteLine($"AAS 3.0 정답 XML을 찾을 수 없습니다: {aas3Path}");
            return 1;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
        var artifactsDir = Path.Combine(repoRoot, "artifacts");
        Directory.CreateDirectory(artifactsDir);

        var schema2 = GoldenSchemaAnalyzer.Analyze(aas2Path, "AAS 2.0");
        var schema3 = GoldenSchemaAnalyzer.Analyze(aas3Path, "AAS 3.0");

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var aas2JsonPath = Path.Combine(artifactsDir, "golden_schema_aas2.json");
        var aas3JsonPath = Path.Combine(artifactsDir, "golden_schema_aas3.json");
        File.WriteAllText(aas2JsonPath, JsonSerializer.Serialize(schema2, jsonOptions), Encoding.UTF8);
        File.WriteAllText(aas3JsonPath, JsonSerializer.Serialize(schema3, jsonOptions), Encoding.UTF8);

        var summaryPath = Path.Combine(artifactsDir, "summary.md");
        File.WriteAllText(summaryPath, GoldenSchemaAnalyzer.BuildSummary(schema2, schema3), Encoding.UTF8);

        Console.WriteLine("정답 구조 분석이 완료되었습니다.");
        Console.WriteLine($"- {aas2JsonPath}");
        Console.WriteLine($"- {aas3JsonPath}");
        Console.WriteLine($"- {summaryPath}");
        return 0;
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AasExcelToXml.slnx"))
                || Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

internal static class GoldenSchemaAnalyzer
{
    private static readonly string[] ElementTypes =
    {
        "property",
        "submodelElementCollection",
        "entity",
        "relationshipElement",
        "referenceElement"
    };

    public static GoldenSchema Analyze(string xmlPath, string version)
    {
        var doc = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
        var root = doc.Root ?? throw new InvalidOperationException("루트 요소를 찾을 수 없습니다.");
        var ns = root.Name.NamespaceName;

        var sections = root.Elements().Select(e => e.Name.LocalName).ToList();
        var tagNames = root.DescendantsAndSelf().Select(e => e.Name.LocalName).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();

        var shell = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "assetAdministrationShell");
        var idElementName = version.Contains("2.0", StringComparison.Ordinal) ? "identification" : "id";
        var identificationElement = shell?.Elements().FirstOrDefault(e => e.Name.LocalName == idElementName)
            ?? root.Descendants().FirstOrDefault(e => e.Name.LocalName == idElementName);

        var identificationInfo = new GoldenIdentification
        {
            ElementName = idElementName,
            HasIdTypeAttribute = identificationElement?.Attribute("idType") is not null,
            HasTextValue = !string.IsNullOrWhiteSpace(identificationElement?.Value)
        };

        var submodelContainer = shell?.Elements().FirstOrDefault(e => e.Name.LocalName is "submodelRefs" or "submodels");
        var shellReferenceInfo = new GoldenShellReference
        {
            ContainerElement = submodelContainer?.Name.LocalName ?? string.Empty,
            ItemElement = submodelContainer?.Elements().FirstOrDefault()?.Name.LocalName ?? string.Empty
        };

        var referenceElement = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "reference");
        var referenceTypeMode = string.Empty;
        if (referenceElement is not null)
        {
            referenceTypeMode = referenceElement.Attribute("type") is not null ? "Attribute" :
                referenceElement.Elements().Any(e => e.Name.LocalName == "type") ? "Element" : "None";
        }

        var keyElement = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "key");
        var keyStructure = new GoldenKeyStructure
        {
            Mode = keyElement is null
                ? "None"
                : keyElement.HasAttributes ? "Attribute" : "Element",
            AttributeNames = keyElement?.Attributes().Select(a => a.Name.LocalName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList() ?? new List<string>(),
            ChildElementNames = keyElement?.Elements().Select(e => e.Name.LocalName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList() ?? new List<string>(),
            HasTextValue = keyElement is not null && !string.IsNullOrWhiteSpace(keyElement.Value)
        };

        var descriptionElement = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "description");
        var descriptionStructure = new GoldenDescriptionStructure
        {
            Mode = "Unknown",
            ChildElementNames = new List<string>(),
            AttributeNames = new List<string>()
        };
        if (descriptionElement is not null)
        {
            var langString = descriptionElement.Elements().FirstOrDefault(e => e.Name.LocalName == "langString");
            var langStringTextType = descriptionElement.Elements().FirstOrDefault(e => e.Name.LocalName == "langStringTextType");

            if (langString is not null)
            {
                descriptionStructure.Mode = "LangString";
                descriptionStructure.ChildElementNames.Add("langString");
                descriptionStructure.AttributeNames = langString.Attributes().Select(a => a.Name.LocalName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            }
            else if (langStringTextType is not null)
            {
                descriptionStructure.Mode = "LangStringTextType";
                descriptionStructure.ChildElementNames.Add("langStringTextType");
                descriptionStructure.SubElementNames = langStringTextType.Elements().Select(e => e.Name.LocalName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            }
        }

        var elementRules = new List<GoldenElementRule>();
        var elementExamples = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var elementType in ElementTypes)
        {
            var element = root.Descendants().FirstOrDefault(e => e.Name.LocalName == elementType);
            if (element is null)
            {
                continue;
            }

            var children = element.Elements().Select(e => e.Name.LocalName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            elementRules.Add(new GoldenElementRule
            {
                ElementName = elementType,
                RequiredChildNames = children
            });
            elementExamples[elementType] = element.ToString(SaveOptions.DisableFormatting);
        }

        var submodelElements = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "submodelElements");
        var hasSubmodelWrapper = submodelElements?.Elements().Any(e => e.Name.LocalName == "submodelElement") == true;

        return new GoldenSchema
        {
            Version = version,
            Root = new GoldenRoot
            {
                Name = root.Name.LocalName,
                Namespace = ns
            },
            Sections = sections,
            Identification = identificationInfo,
            ShellReference = shellReferenceInfo,
            ReferenceStructure = new GoldenReferenceStructure
            {
                ReferenceTypeMode = referenceTypeMode
            },
            KeyStructure = keyStructure,
            SubmodelElementWrapper = hasSubmodelWrapper,
            DescriptionStructure = descriptionStructure,
            ElementRules = elementRules,
            TagNames = tagNames,
            ElementExamples = elementExamples
        };
    }

    public static string BuildSummary(GoldenSchema schema2, GoldenSchema schema3)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 정답 구조 요약");
        builder.AppendLine();
        AppendSchemaSummary(builder, schema2);
        builder.AppendLine();
        AppendSchemaSummary(builder, schema3);
        return builder.ToString();
    }

    private static void AppendSchemaSummary(StringBuilder builder, GoldenSchema schema)
    {
        builder.AppendLine($"## {schema.Version}");
        builder.AppendLine();
        builder.AppendLine($"- 루트: {schema.Root.Name}");
        builder.AppendLine($"- 네임스페이스: {schema.Root.Namespace}");
        builder.AppendLine($"- 섹션: {string.Join(", ", schema.Sections)}");
        builder.AppendLine($"- identification/id 요소: {schema.Identification.ElementName}");
        builder.AppendLine($"  - idType 속성: {(schema.Identification.HasIdTypeAttribute ? "있음" : "없음")}");
        builder.AppendLine($"  - 텍스트 값: {(schema.Identification.HasTextValue ? "있음" : "없음")}");
        builder.AppendLine($"- Shell 참조 컨테이너: {schema.ShellReference.ContainerElement}");
        builder.AppendLine($"- Shell 참조 항목: {schema.ShellReference.ItemElement}");
        builder.AppendLine($"- reference type 표현: {schema.ReferenceStructure.ReferenceTypeMode}");
        builder.AppendLine($"- key 구조: {schema.KeyStructure.Mode}");
        if (schema.KeyStructure.AttributeNames.Count > 0)
        {
            builder.AppendLine($"  - key 속성: {string.Join(", ", schema.KeyStructure.AttributeNames)}");
        }
        if (schema.KeyStructure.ChildElementNames.Count > 0)
        {
            builder.AppendLine($"  - key 하위 요소: {string.Join(", ", schema.KeyStructure.ChildElementNames)}");
        }
        builder.AppendLine($"- submodelElement 래퍼: {(schema.SubmodelElementWrapper ? "사용" : "미사용")}");
        builder.AppendLine($"- description 구조: {schema.DescriptionStructure.Mode}");
        if (schema.DescriptionStructure.ChildElementNames.Count > 0)
        {
            builder.AppendLine($"  - description 하위 요소: {string.Join(", ", schema.DescriptionStructure.ChildElementNames)}");
        }
        if (schema.DescriptionStructure.SubElementNames.Count > 0)
        {
            builder.AppendLine($"  - description 세부 요소: {string.Join(", ", schema.DescriptionStructure.SubElementNames)}");
        }
        builder.AppendLine();
        builder.AppendLine("### 요소 타입별 필수 태그");
        foreach (var rule in schema.ElementRules)
        {
            builder.AppendLine($"- {rule.ElementName}: {string.Join(", ", rule.RequiredChildNames)}");
        }
        builder.AppendLine();
        builder.AppendLine("### 태그 목록");
        builder.AppendLine(string.Join(", ", schema.TagNames));
        builder.AppendLine();
        builder.AppendLine("### 요소 예시");
        foreach (var example in schema.ElementExamples)
        {
            builder.AppendLine($"- {example.Key}: `{example.Value}`");
        }
    }
}

internal sealed class GoldenSchema
{
    public string Version { get; set; } = string.Empty;
    public GoldenRoot Root { get; set; } = new();
    public List<string> Sections { get; set; } = new();
    public GoldenIdentification Identification { get; set; } = new();
    public GoldenShellReference ShellReference { get; set; } = new();
    public GoldenReferenceStructure ReferenceStructure { get; set; } = new();
    public GoldenKeyStructure KeyStructure { get; set; } = new();
    public bool SubmodelElementWrapper { get; set; }
    public GoldenDescriptionStructure DescriptionStructure { get; set; } = new();
    public List<GoldenElementRule> ElementRules { get; set; } = new();
    public List<string> TagNames { get; set; } = new();
    public Dictionary<string, string> ElementExamples { get; set; } = new();
}

internal sealed class GoldenRoot
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
}

internal sealed class GoldenIdentification
{
    public string ElementName { get; set; } = string.Empty;
    public bool HasIdTypeAttribute { get; set; }
    public bool HasTextValue { get; set; }
}

internal sealed class GoldenShellReference
{
    public string ContainerElement { get; set; } = string.Empty;
    public string ItemElement { get; set; } = string.Empty;
}

internal sealed class GoldenReferenceStructure
{
    public string ReferenceTypeMode { get; set; } = string.Empty;
}

internal sealed class GoldenKeyStructure
{
    public string Mode { get; set; } = string.Empty;
    public List<string> AttributeNames { get; set; } = new();
    public List<string> ChildElementNames { get; set; } = new();
    public bool HasTextValue { get; set; }
}

internal sealed class GoldenDescriptionStructure
{
    public string Mode { get; set; } = string.Empty;
    public List<string> ChildElementNames { get; set; } = new();
    public List<string> SubElementNames { get; set; } = new();
    public List<string> AttributeNames { get; set; } = new();
}

internal sealed class GoldenElementRule
{
    public string ElementName { get; set; } = string.Empty;
    public List<string> RequiredChildNames { get; set; } = new();
}
