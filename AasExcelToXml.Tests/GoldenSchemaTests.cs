using System.Text.Json;
using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;
using Xunit.Sdk;

namespace AasExcelToXml.Tests;

public sealed class GoldenSchemaTests
{
    [Fact]
    public void Convert_Aas2_Output_Matches_Golden_Schema()
    {
        AssertMatchesSchema(AasVersion.Aas2_0, "golden_schema_aas2.json");
    }

    [Fact]
    public void Convert_Aas3_Output_Matches_Golden_Schema()
    {
        AssertMatchesSchema(AasVersion.Aas3_0, "golden_schema_aas3.json");
    }

    private static void AssertMatchesSchema(AasVersion version, string schemaFileName)
    {
        var schemaPath = ResolveArtifactsPath(schemaFileName);
        if (schemaPath is null)
        {
            throw new SkipException("golden_schema JSON이 없어 테스트를 건너뜁니다.");
        }

        var inputPath = ResolveSampleInputPath();
        if (inputPath is null)
        {
            throw new SkipException("Sample 입력 파일이 없어 테스트를 건너뜁니다.");
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"schema_{version}_{Guid.NewGuid():N}.xml");
        Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions { Version = version });

        var schema = LoadSchema(schemaPath);
        var doc = XDocument.Load(outputPath, LoadOptions.PreserveWhitespace);
        Assert.NotNull(doc.Root);

        Assert.Equal(schema.Root.Name, doc.Root!.Name.LocalName);
        Assert.Equal(schema.Root.Namespace, doc.Root!.Name.NamespaceName);

        var sectionNames = doc.Root!.Elements().Select(e => e.Name.LocalName).ToList();
        foreach (var section in schema.Sections)
        {
            Assert.Contains(section, sectionNames);
        }

        var shell = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "assetAdministrationShell");
        Assert.NotNull(shell);

        var idElement = shell!.Elements().FirstOrDefault(e => e.Name.LocalName == schema.Identification.ElementName);
        Assert.NotNull(idElement);
        if (schema.Identification.HasIdTypeAttribute)
        {
            Assert.NotNull(idElement!.Attribute("idType"));
        }
        else
        {
            Assert.Null(idElement!.Attribute("idType"));
        }

        if (schema.Identification.HasTextValue)
        {
            Assert.False(string.IsNullOrWhiteSpace(idElement.Value));
        }

        var submodelContainer = shell.Elements().FirstOrDefault(e => e.Name.LocalName == schema.ShellReference.ContainerElement);
        Assert.NotNull(submodelContainer);
        if (!string.IsNullOrWhiteSpace(schema.ShellReference.ItemElement))
        {
            Assert.Contains(submodelContainer!.Elements().Select(e => e.Name.LocalName), name => name == schema.ShellReference.ItemElement);
        }

        var referenceElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "reference");
        if (referenceElement is not null && !string.IsNullOrWhiteSpace(schema.ReferenceStructure.ReferenceTypeMode))
        {
            if (schema.ReferenceStructure.ReferenceTypeMode == "Attribute")
            {
                Assert.NotNull(referenceElement.Attribute("type"));
            }
            else if (schema.ReferenceStructure.ReferenceTypeMode == "Element")
            {
                Assert.NotNull(referenceElement.Elements().FirstOrDefault(e => e.Name.LocalName == "type"));
            }
        }

        var keyElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "key");
        Assert.NotNull(keyElement);
        if (schema.KeyStructure.Mode == "Attribute")
        {
            foreach (var attrName in schema.KeyStructure.AttributeNames)
            {
                Assert.NotNull(keyElement!.Attribute(attrName));
            }
        }
        else if (schema.KeyStructure.Mode == "Element")
        {
            foreach (var childName in schema.KeyStructure.ChildElementNames)
            {
                Assert.NotNull(keyElement!.Elements().FirstOrDefault(e => e.Name.LocalName == childName));
            }
        }

        var submodelElements = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "submodelElements");
        Assert.NotNull(submodelElements);
        var hasWrapper = submodelElements!.Elements().Any(e => e.Name.LocalName == "submodelElement");
        Assert.Equal(schema.SubmodelElementWrapper, hasWrapper);

        var descriptionElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "description");
        if (descriptionElement is not null)
        {
            if (schema.DescriptionStructure.Mode == "LangString")
            {
                var langString = descriptionElement.Elements().FirstOrDefault(e => e.Name.LocalName == "langString");
                Assert.NotNull(langString);
                foreach (var attrName in schema.DescriptionStructure.AttributeNames)
                {
                    Assert.NotNull(langString!.Attribute(attrName));
                }
            }
            else if (schema.DescriptionStructure.Mode == "LangStringTextType")
            {
                var langStringTextType = descriptionElement.Elements().FirstOrDefault(e => e.Name.LocalName == "langStringTextType");
                Assert.NotNull(langStringTextType);
                foreach (var childName in schema.DescriptionStructure.SubElementNames)
                {
                    Assert.NotNull(langStringTextType!.Elements().FirstOrDefault(e => e.Name.LocalName == childName));
                }
            }
        }

        foreach (var rule in schema.ElementRules)
        {
            var element = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == rule.ElementName);
            if (element is null)
            {
                continue;
            }

            var childNames = element.Elements().Select(e => e.Name.LocalName).ToHashSet(StringComparer.Ordinal);
            foreach (var required in rule.RequiredChildNames)
            {
                Assert.Contains(required, childNames);
            }
        }

        var actualTags = doc.DescendantsAndSelf().Select(e => e.Name.LocalName).ToHashSet(StringComparer.Ordinal);
        foreach (var tag in schema.TagNames)
        {
            Assert.Contains(tag, actualTags);
        }
    }

    private static GoldenSchema LoadSchema(string path)
    {
        var json = File.ReadAllText(path);
        var schema = JsonSerializer.Deserialize<GoldenSchema>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return schema ?? throw new InvalidOperationException("golden_schema JSON을 읽을 수 없습니다.");
    }

    private static string? ResolveArtifactsPath(string schemaFileName)
    {
        var repoRoot = FindRepoRoot() ?? throw new InvalidOperationException("레포 루트를 찾을 수 없습니다.");
        var candidate = Path.Combine(repoRoot, "artifacts", schemaFileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? ResolveSampleInputPath()
    {
        var repoRoot = FindRepoRoot() ?? throw new InvalidOperationException("레포 루트를 찾을 수 없습니다.");

        var candidates = new[]
        {
            Path.Combine(repoRoot, "Sample"),
            Path.Combine(repoRoot, "AasExcelToXml.Cli", "Sample")
        };

        foreach (var baseDir in candidates)
        {
            var input = Path.Combine(baseDir, "하누리 에어밸런스로봇 사양 정리_2025_05_02_r1.xlsx");
            if (File.Exists(input))
            {
                return input;
            }
        }

        return null;
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
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

    private sealed class GoldenSchema
    {
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
    }

    private sealed class GoldenRoot
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
    }

    private sealed class GoldenIdentification
    {
        public string ElementName { get; set; } = string.Empty;
        public bool HasIdTypeAttribute { get; set; }
        public bool HasTextValue { get; set; }
    }

    private sealed class GoldenShellReference
    {
        public string ContainerElement { get; set; } = string.Empty;
        public string ItemElement { get; set; } = string.Empty;
    }

    private sealed class GoldenReferenceStructure
    {
        public string ReferenceTypeMode { get; set; } = string.Empty;
    }

    private sealed class GoldenKeyStructure
    {
        public string Mode { get; set; } = string.Empty;
        public List<string> AttributeNames { get; set; } = new();
        public List<string> ChildElementNames { get; set; } = new();
        public bool HasTextValue { get; set; }
    }

    private sealed class GoldenDescriptionStructure
    {
        public string Mode { get; set; } = string.Empty;
        public List<string> ChildElementNames { get; set; } = new();
        public List<string> SubElementNames { get; set; } = new();
        public List<string> AttributeNames { get; set; } = new();
    }

    private sealed class GoldenElementRule
    {
        public string ElementName { get; set; } = string.Empty;
        public List<string> RequiredChildNames { get; set; } = new();
    }
}
