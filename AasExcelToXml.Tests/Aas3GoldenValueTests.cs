using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;
using Xunit.Sdk;

namespace AasExcelToXml.Tests;

public sealed class Aas3GoldenValueTests
{
    [Fact]
    public void Convert_Aas3_Output_Matches_Golden_With_Normalized_Ids()
    {
        var paths = ResolveSamplePathsOrSkip();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas3_values_{Guid.NewGuid():N}.xml");

        Converter.Convert(
            paths.InputExcel,
            outputPath,
            "사양시트",
            new ConvertOptions
            {
                Version = AasVersion.Aas3_0
            });

        var expected = NormalizeXml(paths.GoldenAas3);
        var actual = NormalizeXml(outputPath);
        Assert.Equal(expected, actual);
    }

    private static string NormalizeXml(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        if (doc.Root is null)
        {
            return string.Empty;
        }

        var normalized = new XDocument(NormalizeElement(doc.Root));
        return normalized.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement NormalizeElement(XElement element)
    {
        var attributes = element.Attributes()
            .OrderBy(attr => attr.Name.ToString(), StringComparer.Ordinal);

        var childElements = element.Nodes()
            .OfType<XElement>()
            .Select(NormalizeElement)
            .ToList();

        if (childElements.Count == 0)
        {
            var value = ShouldIgnoreValue(element) ? string.Empty : element.Value;
            return new XElement(element.Name, attributes, value);
        }

        return new XElement(element.Name, attributes, childElements);
    }

    private static bool ShouldIgnoreValue(XElement element)
    {
        var localName = element.Name.LocalName;
        if (string.Equals(localName, "id", StringComparison.OrdinalIgnoreCase)
            || string.Equals(localName, "globalAssetId", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(localName, "value", StringComparison.OrdinalIgnoreCase)
            && element.Parent is not null
            && string.Equals(element.Parent.Name.LocalName, "key", StringComparison.OrdinalIgnoreCase))
        {
            var keyType = element.Parent.Elements()
                .FirstOrDefault(child => string.Equals(child.Name.LocalName, "type", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return string.Equals(keyType, "Submodel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(keyType, "AssetAdministrationShell", StringComparison.OrdinalIgnoreCase)
                || string.Equals(keyType, "Asset", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static SamplePaths ResolveSamplePathsOrSkip()
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
            var goldenAas3 = Path.Combine(baseDir, "Air_balance_robot_aas_model_정답_aas3.0.xml");

            if (File.Exists(input) && File.Exists(goldenAas3))
            {
                return new SamplePaths(input, goldenAas3);
            }
        }

        throw new SkipException("Sample 폴더에 필요한 입력/정답 파일이 없어 테스트를 건너뜁니다.");
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

    private sealed record SamplePaths(string InputExcel, string GoldenAas3);
}
