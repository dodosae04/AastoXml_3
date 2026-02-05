using System.Text.Json;
using System.Text.Json.Serialization;

namespace AasExcelToXml.Core;

public static class DocumentationProfileLoader
{
    public static DocumentationProfile Load(ConvertOptions options, SpecDiagnostics diagnostics)
    {
        return Load(options, diagnostics, "golden_doc_profile_v2.json", options.GoldenDocProfilePath);
    }

    public static DocumentationProfile LoadV3(ConvertOptions options, SpecDiagnostics diagnostics)
    {
        return Load(options, diagnostics, "golden_doc_profile_v3.json", options.GoldenDocProfilePathV3);
    }

    private static DocumentationProfile Load(ConvertOptions options, SpecDiagnostics diagnostics, string defaultFileName, string? overridePath)
    {
        var path = ResolveProfilePath(options, defaultFileName, overridePath);
        if (path is null || !File.Exists(path))
        {
            // 정답 XML에서 추출한 스켈레톤이 없으면 VDI2770 기본 구조로 폴백한다.
            diagnostics.AutoCorrections.Add("골든 문서 프로파일 없음 → Documentation 폴백 스켈레톤 사용");
            return DocumentationProfile.CreateFallback();
        }

        try
        {
            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<DocumentationProfile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });

            if (profile is null || profile.DocumentFields.Count == 0)
            {
                diagnostics.AutoCorrections.Add("골든 문서 프로파일 로드 실패 → Documentation 폴백 스켈레톤 사용");
                return DocumentationProfile.CreateFallback();
            }

            return profile;
        }
        catch (Exception ex)
        {
            diagnostics.AutoCorrections.Add($"골든 문서 프로파일 파싱 실패 → Documentation 폴백 스켈레톤 사용: {ex.Message}");
            return DocumentationProfile.CreateFallback();
        }
    }

    private static string? ResolveProfilePath(ConvertOptions options, string defaultFileName, string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
        {
            return null;
        }

        return Path.Combine(repoRoot, "Templates", defaultFileName);
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
