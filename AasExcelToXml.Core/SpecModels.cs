namespace AasExcelToXml.Core;

public record SpecRow(
    string Aas,
    string Submodel,
    string Collection,
    string PropKor,
    string PropEng,
    string PropType,
    string Value,
    string Uom,
    string Category,
    string ReferenceData = ""
);

public sealed record LangStringSpec(string Language, string Text);

public sealed record ConceptDescriptionSpec(
    string IdShort,
    string Id,
    string Category,
    List<LangStringSpec> Description,
    string? IsCaseOf = null
);

public sealed record ExternalReferenceRow(
    string IdShort,
    string Category,
    string DescriptionLanguage,
    string Description,
    string IdentifiableId,
    string IsCaseOf,
    string SourceKey = ""
);

public record AasEnvironmentSpec(List<AasSpec> Assets, List<ConceptDescriptionSpec>? ConceptDescriptions = null);

public record AasSpec(string Name, string IdShort, List<SubmodelSpec> Submodels);

public record SubmodelSpec(
    string Name,
    string IdShort,
    List<ElementSpec> Elements,
    string? Category = null);

public record ElementSpec(
    string Collection,
    string IdShort,
    string DisplayNameKo,
    ElementKind Kind,
    string ValueType,
    string Value,
    string Uom,
    string ReferenceData,
    string? SemanticId,
    string? ReferenceTarget,
    RelationshipSpec? Relationship,
    string? Category = null,
    string? ReferenceTargetAasIdShort = null,
    string? ReferenceTargetSubmodelHint = null,
    ResolvedReference? ResolvedReference = null
);

public record RelationshipSpec(string First, string Second);

public record ResolvedReference(
    string TargetAasIdShort,
    string TargetSubmodelIdShort,
    string TargetElementIdShort,
    string KeyType
);

public enum ElementKind
{
    Property,
    Entity,
    Relationship,
    ReferenceElement,
    DocumentationInput
}
