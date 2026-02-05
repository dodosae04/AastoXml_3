using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class Aas3DocumentationGoldenTests
{
    [Fact]
    public void Convert_Aas3_Documentation_Matches_Golden_Structure()
    {
        var inputPath = ResolveSampleInputPath();
        if (inputPath is null)
        {
            throw new SkipException("Sample 입력 파일이 없어 테스트를 건너뜁니다.");
        }

        var outputDir = Path.Combine(Path.GetTempPath(), "AasExcelToXml.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "sample.aas3.xml");

        var result = Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions
        {
            Version = AasVersion.Aas3_0
        });

        Assert.True(File.Exists(result.OutputPath));
        var document = XDocument.Load(result.OutputPath);

        var documentationSubmodel = FindSubmodel(document, "Documentation");
        Assert.NotNull(documentationSubmodel);

        var documentCollections = documentationSubmodel!
            .Descendants()
            .Where(e => e.Name.LocalName == "submodelElementCollection")
            .Where(e => GetChildValue(e, "idShort").StartsWith("Document", StringComparison.Ordinal))
            .ToList();

        Assert.Single(documentCollections);
        Assert.Equal("Document01", GetChildValue(documentCollections[0], "idShort"));

        var doc01Children = ExtractValueChildren(documentCollections[0]).Select(e => GetChildValue(e, "idShort")).ToList();
        Assert.Equal(new[]
        {
            "DocumentId",
            "IsPrimaryDocumentId",
            "DocumentClassId",
            "DocumentClassName",
            "DocumentClassificationSystem",
            "DocumentVersion01"
        }, doc01Children);

        Assert.DoesNotContain("DigitalFile", doc01Children);

        var versionCollection = ExtractValueChildren(documentCollections[0])
            .FirstOrDefault(e => e.Name.LocalName == "submodelElementCollection" && GetChildValue(e, "idShort") == "DocumentVersion01");
        Assert.NotNull(versionCollection);

        var versionChildren = ExtractValueChildren(versionCollection!)
            .Select(e => GetChildValue(e, "idShort"))
            .ToList();

        Assert.Equal(new[]
        {
            "Language01",
            "DocumentVersionId",
            "Title",
            "Summary",
            "KeyWords",
            "SetDate",
            "StatusValue",
            "Role",
            "OrganizationName",
            "OrganizationOfficialName",
            "DigitalFile"
        }, versionChildren);

        Assert.Equal("kr", GetChildValue(FindChild(versionCollection!, "Language01"), "value"));
        Assert.Equal("V1.2", GetChildValue(FindChild(versionCollection!, "DocumentVersionId"), "value"));

        AssertSemanticId(documentCollections[0], DocumentationSemanticUris.Document);
        AssertSemanticId(FindChild(documentCollections[0], "DocumentId"), DocumentationSemanticUris.DocumentId);
        AssertSemanticId(versionCollection!, DocumentationSemanticUris.DocumentVersion);
        AssertSemanticId(FindChild(versionCollection!, "DigitalFile"), DocumentationSemanticUris.DigitalFile);
    }

    [Fact]
    public void Convert_Excel_Produces_No_Warnings()
    {
        var inputPath = ResolveSampleInputPath();
        if (inputPath is null)
        {
            throw new SkipException("Sample 입력 파일이 없어 테스트를 건너뜁니다.");
        }

        var outputDir = Path.Combine(Path.GetTempPath(), "AasExcelToXml.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "sample.aas3.xml");

        var result = Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions
        {
            Version = AasVersion.Aas3_0
        });

        Assert.Equal(0, result.Diagnostics.WarningCount);
    }

    private static XElement? FindSubmodel(XDocument document, string idShort)
    {
        return document.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "submodel" && GetChildValue(e, "idShort") == idShort);
    }

    private static IEnumerable<XElement> ExtractValueChildren(XElement element)
    {
        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value" || e.Name.LocalName == "submodelElements");
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

    private static XElement FindChild(XElement parent, string idShort)
    {
        return ExtractValueChildren(parent)
            .First(e => GetChildValue(e, "idShort") == idShort);
    }

    private static void AssertSemanticId(XElement element, string expectedValue)
    {
        var semanticId = element.Elements().FirstOrDefault(e => e.Name.LocalName == "semanticId");
        Assert.NotNull(semanticId);

        var key = semanticId!.Descendants().FirstOrDefault(e => e.Name.LocalName == "key");
        Assert.NotNull(key);

        var type = key!.Elements().FirstOrDefault(e => e.Name.LocalName == "type")?.Value;
        var value = key.Elements().FirstOrDefault(e => e.Name.LocalName == "value")?.Value;

        Assert.Equal("GlobalReference", type);
        Assert.Equal(expectedValue, value);
    }

    private static string? ResolveSampleInputPath()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
        {
            return null;
        }

        foreach (var candidate in new[]
        {
            Path.Combine(repoRoot, "Sample"),
            Path.Combine(repoRoot, "AasExcelToXml.Cli", "Sample")
        })
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var input = Directory.GetFiles(candidate, "*.xlsx").FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
        }

        return null;
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

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }
}
