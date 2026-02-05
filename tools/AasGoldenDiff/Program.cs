using System;
using System.Collections.Generic;
using System.Text;
using AasExcelToXml.Core;

namespace AasGoldenDiff;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("사용법: dotnet run --project tools/AasGoldenDiff -- [--version 2|3] <GOLDEN_XML> <ACTUAL_XML>");
            return 1;
        }

        var version = 2;
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--version=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg.Substring("--version=".Length);
                version = value.Trim() == "3" ? 3 : 2;
                continue;
            }

            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                version = args[i + 1].Trim() == "3" ? 3 : 2;
                i++;
                continue;
            }

            positional.Add(arg);
        }

        if (positional.Count < 2)
        {
            Console.WriteLine("사용법: dotnet run --project tools/AasGoldenDiff -- [--version 2|3] <GOLDEN_XML> <ACTUAL_XML>");
            return 1;
        }

        var goldenPath = positional[0];
        var actualPath = positional[1];
        if (!File.Exists(goldenPath))
        {
            Console.Error.WriteLine($"정답 XML을 찾을 수 없습니다: {goldenPath}");
            return 1;
        }

        if (!File.Exists(actualPath))
        {
            Console.Error.WriteLine($"비교 대상 XML을 찾을 수 없습니다: {actualPath}");
            return 1;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
        var artifactsDir = Path.Combine(repoRoot, "artifacts");
        Directory.CreateDirectory(artifactsDir);

        if (version == 3)
        {
            var report = Aas3GoldenDiffAnalyzer.Analyze(goldenPath, actualPath);
            var jsonPath = Path.Combine(artifactsDir, "golden_diff_report_aas3.json");
            File.WriteAllText(jsonPath, Aas3GoldenDiffAnalyzer.ToJson(report), Encoding.UTF8);
            Console.WriteLine(Aas3GoldenDiffAnalyzer.BuildSummary(report));
            Console.WriteLine($"- JSON 리포트: {jsonPath}");
            return 0;
        }

        var aas2Report = GoldenDiffAnalyzer.Analyze(goldenPath, actualPath);
        var aas2JsonPath = Path.Combine(artifactsDir, "golden_diff_report.json");
        File.WriteAllText(aas2JsonPath, GoldenDiffAnalyzer.ToJson(aas2Report), Encoding.UTF8);

        Console.WriteLine(GoldenDiffAnalyzer.BuildSummary(aas2Report));
        Console.WriteLine($"- JSON 리포트: {aas2JsonPath}");
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
