using System;
using System.IO;
using System.Text.Json;

namespace AasExcelToXml.Core;

internal static class SubmodelSkeletonLoader
{
    public static SubmodelSkeletonProfile? Load(SpecDiagnostics diagnostics)
    {
        var path = ResolveProfilePath();
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SubmodelSkeletonProfile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            diagnostics.AutoCorrections.Add($"AAS3 서브모델 스켈레톤 로딩 실패 → 기본 규칙 사용: {ex.Message}");
            return null;
        }
    }

    private static string? ResolveProfilePath()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
        {
            return null;
        }

        return Path.Combine(repoRoot, "Templates", "submodel_skeleton_v3.json");
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
