using System.Text;

namespace AasExcelToXml.Core;

public sealed class SpecDiagnostics
{
    public List<string> AutoCorrections { get; } = new();
    public List<string> MissingEntityReferences { get; } = new();
    public List<string> MissingRelationshipReferences { get; } = new();
    public List<string> TypeAdjustments { get; } = new();
    public List<string> DuplicateIdShorts { get; } = new();
    public List<string> Aas3ValidationIssues { get; } = new();
    public List<string> ExternalReferenceIssues { get; } = new();

    public bool HasWarnings =>
        AutoCorrections.Count > 0
        || MissingEntityReferences.Count > 0
        || MissingRelationshipReferences.Count > 0
        || TypeAdjustments.Count > 0
        || DuplicateIdShorts.Count > 0
        || Aas3ValidationIssues.Count > 0
        || ExternalReferenceIssues.Count > 0;

    public int WarningCount => GetTotalCount();

    public int GetTotalCount()
    {
        return AutoCorrections.Count
            + MissingEntityReferences.Count
            + MissingRelationshipReferences.Count
            + TypeAdjustments.Count
            + DuplicateIdShorts.Count
            + Aas3ValidationIssues.Count
            + ExternalReferenceIssues.Count;
    }

    public string CreateReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("AAS 변환 경고 리포트");
        builder.AppendLine($"- 자동 보정: {AutoCorrections.Count}");
        AppendList(builder, AutoCorrections);
        builder.AppendLine($"- 깨진 참조(Entity): {MissingEntityReferences.Count}");
        AppendList(builder, MissingEntityReferences);
        builder.AppendLine($"- 깨진 참조(Relationship): {MissingRelationshipReferences.Count}");
        AppendList(builder, MissingRelationshipReferences);
        builder.AppendLine($"- 타입 자동 교정: {TypeAdjustments.Count}");
        AppendList(builder, TypeAdjustments);
        builder.AppendLine($"- 중복 idShort: {DuplicateIdShorts.Count}");
        AppendList(builder, DuplicateIdShorts);
        builder.AppendLine($"- AAS3 구조 검증: {Aas3ValidationIssues.Count}");
        AppendList(builder, Aas3ValidationIssues);
        builder.AppendLine($"- 외부참조: {ExternalReferenceIssues.Count}");
        AppendList(builder, ExternalReferenceIssues);
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, List<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"  - {item}");
        }
    }
}
