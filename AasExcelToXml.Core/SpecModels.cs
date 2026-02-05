namespace AasExcelToXml.Core;

public record SpecRow(
    string Aas,
    string Submodel,
    string Collection,
    string PropKor,
    string PropEng,
    string PropType,
    string Value,
    string Uom
);

public record AasEnvironmentSpec(List<AasSpec> Assets);

public record AasSpec(string Name, string IdShort, List<SubmodelSpec> Submodels);

public record SubmodelSpec(string Name, string IdShort, List<ElementSpec> Elements);

public record ElementSpec(
    string Collection,
    string IdShort,
    string DisplayNameKo,
    ElementKind Kind,
    string ValueType,
    string Value,
    string Uom,
    string? ReferenceTarget,
    RelationshipSpec? Relationship,
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
