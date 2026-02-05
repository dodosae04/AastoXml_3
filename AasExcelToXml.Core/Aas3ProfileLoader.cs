using System.Text.Json;
using System.Text.Json.Serialization;

namespace AasExcelToXml.Core;

public static class Aas3ProfileLoader
{
    public static Aas3Profile Load(ConvertOptions options, SpecDiagnostics diagnostics)
    {
        var path = ResolveProfilePath(options);
        if (path is null || !File.Exists(path))
        {
            diagnostics.AutoCorrections.Add("AAS3 골든 프로파일 없음 → 기본 참조 규칙 사용");
            return Aas3Profile.CreateFallback();
        }

        try
        {
            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<Aas3Profile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });

            if (profile is null)
            {
                diagnostics.AutoCorrections.Add("AAS3 골든 프로파일 로드 실패 → 기본 참조 규칙 사용");
                return Aas3Profile.CreateFallback();
            }

            return profile;
        }
        catch (Exception ex)
        {
            diagnostics.AutoCorrections.Add($"AAS3 골든 프로파일 파싱 실패 → 기본 참조 규칙 사용: {ex.Message}");
            return Aas3Profile.CreateFallback();
        };
    }

    private static string? ResolveProfilePath(ConvertOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.GoldenAas3ProfilePath))
        {
            return options.GoldenAas3ProfilePath;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
        {
            return null;
        }

        return Path.Combine(repoRoot, "Templates", "golden_profile_aas3.json");
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
