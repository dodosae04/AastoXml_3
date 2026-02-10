namespace AasExcelToXml.Core;

public enum AasVersion
{
    Aas2_0,
    Aas3_0
}

public enum IdScheme
{
    ExampleIri,
    UuidUrn
}

public enum ExampleIriDigitsMode
{
    DeterministicHash,
    RandomSecure
}

public sealed class ConvertOptions
{
    public AasVersion Version { get; init; } = AasVersion.Aas2_0;
    public string? NamespaceOverride { get; init; }
    public bool IncludeKoreanDescription { get; init; }
    public bool IncludeAllDocumentation { get; init; } = false;
    public long DocumentIdSeed { get; init; } = 64879470;
    public string? GoldenDocProfilePath { get; init; }
    public string? GoldenDocProfilePathV3 { get; init; }
    public string? GoldenAas3ProfilePath { get; init; }
    public string? DocumentOverridePath { get; init; }
    public IdScheme IdScheme { get; init; } = IdScheme.ExampleIri;
    public ExampleIriDigitsMode ExampleIriDigitsMode { get; init; } = ExampleIriDigitsMode.DeterministicHash;
    public string BaseIri { get; init; } = "https://example.com/ids";
    public string? InputFileName { get; set; }
    public string DocumentDefaultLanguage { get; init; } = "kr";
    public string DocumentDefaultVersionId { get; init; } = "V1.2";
    public bool UseFixedSetDate { get; init; }
    public string DocumentDefaultSetDate { get; init; } = string.Empty;
    public string DocumentDefaultStatusValue { get; init; } = "Released";
    public string DocumentDefaultRole { get; init; } = "Author";
    public string DocumentDefaultOrganizationName { get; init; } = "Hanuri";
    public string DocumentDefaultOrganizationOfficialName { get; init; } = "Hanuri";
    public string DocumentDefaultClassId { get; init; } = "02-01";
    public string DocumentDefaultClassName { get; init; } = "Technical specifiction";
    public string DocumentDefaultClassificationSystem { get; init; } = "VDI2770:2018";
    public bool WriteWarningsOnlyWhenNeeded { get; init; }
    public bool FillMissingCategoryWithConstant { get; init; }
    public string MissingCategoryConstant { get; init; } = string.Empty;
}
