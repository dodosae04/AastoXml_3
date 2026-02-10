using System.Collections.Generic;

namespace AasExcelToXml.Core;

internal sealed class SubmodelSkeletonProfile
{
    public Dictionary<string, SubmodelSkeleton> Submodels { get; set; } = new(StringComparer.Ordinal);

    public bool TryGet(string idShort, out SubmodelSkeleton skeleton)
    {
        return Submodels.TryGetValue(idShort, out skeleton!);
    }
}

internal sealed class SubmodelSkeleton
{
    public string? Kind { get; set; }
    public string? Category { get; set; }
    public SubmodelSkeletonReference? SemanticId { get; set; }
    public Dictionary<string, SubmodelSkeletonCollection> Collections { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class SubmodelSkeletonCollection
{
    public string? Category { get; set; }
    public bool IncludeDescription { get; set; } = true;
    public SubmodelSkeletonReference? SemanticId { get; set; }
    public List<SubmodelSkeletonPlaceholder> Placeholders { get; set; } = new();
}

internal sealed class SubmodelSkeletonPlaceholder
{
    public string Kind { get; set; } = "property";
    public string IdShort { get; set; } = string.Empty;
    public string? Category { get; set; }
    public SubmodelSkeletonReference? SemanticId { get; set; }
    public string? Value { get; set; }
    public string? ContentType { get; set; }
}

internal sealed class SubmodelSkeletonReference
{
    public string? Type { get; set; }
    public List<SubmodelSkeletonKey> Keys { get; set; } = new();
}

internal sealed class SubmodelSkeletonKey
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Local { get; set; }
    public string? IdType { get; set; }
}
