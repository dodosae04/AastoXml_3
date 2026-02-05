using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;
using Xunit.Sdk;

namespace AasExcelToXml.Tests;

public sealed class GoldenFileTests
{
    [Fact]
    public void Convert_Aas2_Output_Matches_Golden()
    {
        var paths = ResolveSamplePathsOrSkip();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas2_{Guid.NewGuid():N}.xml");

        Converter.Convert(
            paths.InputExcel,
            outputPath,
            "사양시트",
            new ConvertOptions
            {
                Version = AasVersion.Aas2_0
            });

        AssertXmlStructureEqual(paths.GoldenAas2, outputPath);
    }

    [Fact]
    public void Convert_Aas3_Output_Matches_Golden()
    {
        var paths = ResolveSamplePathsOrSkip();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas3_{Guid.NewGuid():N}.xml");

        Converter.Convert(
            paths.InputExcel,
            outputPath,
            "사양시트",
            new ConvertOptions
            {
                Version = AasVersion.Aas3_0
            });

        AssertXmlStructureEqual(paths.GoldenAas3, outputPath);
    }

    private static void AssertXmlStructureEqual(string expectedPath, string actualPath)
    {
        var expected = CanonicalizeStructure(expectedPath);
        var actual = CanonicalizeStructure(actualPath);
        Assert.Equal(expected, actual);
    }

    private static string CanonicalizeStructure(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        if (doc.Root is null)
        {
            return string.Empty;
        }

        var normalized = new XDocument(NormalizeStructureElement(doc.Root));
        return normalized.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement NormalizeStructureElement(XElement element)
    {
        // 값 비교는 최소화하고 구조만 비교한다.
        var nodes = element.Nodes()
            .Where(node => node is XElement)
            .Select(node => NormalizeStructureElement((XElement)node));

        var attributes = element.Attributes()
            .OrderBy(attr => attr.Name.ToString(), StringComparer.Ordinal);

        return new XElement(element.Name, attributes, nodes);
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
            var goldenAas2 = Path.Combine(baseDir, "Air_balance_robot_aas_model_정답_aas2.0.xml");
            var goldenAas3 = Path.Combine(baseDir, "Air_balance_robot_aas_model_정답_aas3.0.xml");

            if (File.Exists(input) && File.Exists(goldenAas2) && File.Exists(goldenAas3))
            {
                return new SamplePaths(input, goldenAas2, goldenAas3);
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

    private sealed record SamplePaths(string InputExcel, string GoldenAas2, string GoldenAas3);
}
