using AasExcelToXml.Core;
using AasExcelToXml.Core.GoldenDiff;
using Xunit;
using Xunit.Sdk;

namespace AasExcelToXml.Tests;

public sealed class GoldenDiffTests
{
    [Fact]
    public void Convert_Aas2_Output_Passes_GoldenDiff_Rules()
    {
        var paths = ResolveSamplePathsOrSkip();
        var outputPath = Path.Combine(Path.GetTempPath(), $"diff_{Guid.NewGuid():N}.xml");

        Converter.Convert(
            paths.InputExcel,
            outputPath,
            "사양시트",
            new ConvertOptions
            {
                Version = AasVersion.Aas2_0
            });

        var report = GoldenDiffAnalyzer.Analyze(paths.GoldenAas2, outputPath);
        Assert.Empty(report.QualifierIssues);
        Assert.Empty(report.PlaceholderIssues);
        Assert.Empty(report.ReferenceIssues);
        Assert.Empty(report.DocumentationIssues);
        Assert.Empty(report.StructureIssues);
    }

    [Fact]
    public void Convert_Aas3_Output_Passes_GoldenDiff_Rules()
    {
        var paths = ResolveSamplePathsOrSkip();
        var outputPath = Path.Combine(Path.GetTempPath(), $"diff_{Guid.NewGuid():N}.xml");

        Converter.Convert(
            paths.InputExcel,
            outputPath,
            "사양시트",
            new ConvertOptions
            {
                Version = AasVersion.Aas3_0
            });

        var report = Aas3GoldenDiffAnalyzer.Analyze(paths.GoldenAas3, outputPath);
        Assert.Empty(report.MissingInGenerated);
        Assert.Empty(report.ExtraInGenerated);
        Assert.Empty(report.DifferentValues);
        Assert.Empty(report.IdentifierIssues);
        Assert.Empty(report.ReferenceIssues);
        Assert.Empty(report.RelationshipIssues);
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
