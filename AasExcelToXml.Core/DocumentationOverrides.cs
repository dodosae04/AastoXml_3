using System.Text.Json;
using System.IO;

namespace AasExcelToXml.Core;

internal sealed class DocumentationOverrideProfile
{
    public List<DocumentationOverrideRule> Overrides { get; set; } = new();

    public DocumentationOverrideRule? Resolve(DocumentInput input)
    {
        foreach (var rule in Overrides)
        {
            if (rule.Matches(input))
            {
                return rule;
            }
        }

        return null;
    }
}

internal sealed class DocumentationOverrideRule
{
    public string? MatchNameContains { get; set; }
    public string? MatchTypeContains { get; set; }
    public string? MatchFileNameContains { get; set; }
    public string? DocumentId { get; set; }
    public string? IsPrimaryDocumentId { get; set; }
    public string? DocumentClassId { get; set; }
    public string? DocumentClassName { get; set; }
    public string? DocumentClassificationSystem { get; set; }
    public string? DocumentVersionId { get; set; }
    public string? Language { get; set; }

    public bool Matches(DocumentInput input)
    {
        if (!MatchContains(input.Name, MatchNameContains))
        {
            return false;
        }

        if (!MatchContains(input.Type, MatchTypeContains))
        {
            return false;
        }

        var fileName = string.IsNullOrWhiteSpace(input.FilePath) ? string.Empty : Path.GetFileName(input.FilePath);
        if (!MatchContains(fileName, MatchFileNameContains))
        {
            return false;
        }

        return true;
    }

    private static bool MatchContains(string? source, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class DocumentationOverrideLoader
{
    public static DocumentationOverrideProfile? Load(ConvertOptions options, SpecDiagnostics diagnostics)
    {
        var path = ResolveOverridePath(options);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<DocumentationOverrideProfile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return profile;
        }
        catch (Exception ex)
        {
            diagnostics.AutoCorrections.Add($"문서 오버라이드 파일 파싱 실패 → 기본 규칙 사용: {ex.Message}");
            return null;
        }
    }

    private static string? ResolveOverridePath(ConvertOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DocumentOverridePath))
        {
            return options.DocumentOverridePath;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
        {
            return null;
        }

        return Path.Combine(repoRoot, "artifacts", "doc_overrides.json");
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
