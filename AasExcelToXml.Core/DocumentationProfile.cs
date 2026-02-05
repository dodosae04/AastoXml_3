using System.Text.Json.Serialization;

namespace AasExcelToXml.Core;

public sealed class DocumentationProfile
{
    public string DocumentCollectionIdShortPattern { get; set; } = "Document{N}";
    public string DocumentVersionIdShort { get; set; } = "DocumentVersion01";
    public string? DocumentCollectionCategory { get; set; }
    public bool? DocumentCollectionOrdered { get; set; }
    public bool? DocumentCollectionAllowDuplicates { get; set; }
    public DocumentationReference? DocumentCollectionSemanticId { get; set; }
    public bool DocumentCollectionHasQualifier { get; set; }
    public List<DocumentationElementTemplate> DocumentFields { get; set; } = new();
    public DocumentationFilePattern? FilePattern { get; set; }

    public static DocumentationProfile CreateFallback()
    {
        return new DocumentationProfile
        {
            DocumentCollectionIdShortPattern = "Document{N}",
            DocumentVersionIdShort = "DocumentVersion01",
            DocumentCollectionCategory = "CONSTANT",
            DocumentCollectionOrdered = false,
            DocumentCollectionAllowDuplicates = false,
            DocumentCollectionSemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.Document),
            DocumentCollectionHasQualifier = false,
            DocumentFields = new List<DocumentationElementTemplate>
            {
                new(DocumentationElementKind.Property, "DocumentId")
                {
                    UseEmptyValueType = true,
                    Category = "CONSTANT",
                    SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentId),
                    HasQualifier = false
                },
                new(DocumentationElementKind.Property, "IsPrimaryDocumentId")
                {
                    UseEmptyValueType = true,
                    Category = "CONSTANT",
                    SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.IsPrimaryDocumentId),
                    HasQualifier = false
                },
                new(DocumentationElementKind.Property, "DocumentClassId")
                {
                    UseEmptyValueType = true,
                    Category = "CONSTANT",
                    DefaultValue = "02-01",
                    SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentClassId),
                    HasQualifier = false
                },
                new(DocumentationElementKind.Property, "DocumentClassName")
                {
                    UseEmptyValueType = true,
                    Category = "CONSTANT",
                    DefaultValue = "Technical specifiction",
                    SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentClassName),
                    HasQualifier = false
                },
                new(DocumentationElementKind.Property, "DocumentClassificationSystem")
                {
                    UseEmptyValueType = true,
                    Category = "CONSTANT",
                    DefaultValue = "VDI2770:2018",
                    SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentClassificationSystem),
                    HasQualifier = false
                },
                new(DocumentationElementKind.SubmodelElementCollection, "DocumentVersion01")
                {
                    Category = "CONSTANT",
                    Ordered = false,
                    AllowDuplicates = false,
                    SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentVersion),
                    HasQualifier = false,
                    Children = new List<DocumentationElementTemplate>
                    {
                        new(DocumentationElementKind.Property, "Language01")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            DefaultValue = "kr",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentVersionLanguage),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.Property, "DocumentVersionId")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            DefaultValue = "V1.2",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DocumentVersionId),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.MultiLanguageProperty, "Title")
                        {
                            Category = "CONSTANT",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.Title),
                            HasQualifier = false,
                            LangStrings = new List<DocumentationLangString>
                            {
                                new("EN", string.Empty)
                            }
                        },
                        new(DocumentationElementKind.MultiLanguageProperty, "Summary")
                        {
                            Category = "CONSTANT",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.Summary),
                            HasQualifier = false,
                            LangStrings = new List<DocumentationLangString>
                            {
                                new("kr", string.Empty)
                            }
                        },
                        new(DocumentationElementKind.MultiLanguageProperty, "KeyWords")
                        {
                            Category = "CONSTANT",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.KeyWords),
                            HasQualifier = false,
                            LangStrings = new List<DocumentationLangString>
                            {
                                new("kr", string.Empty)
                            }
                        },
                        new(DocumentationElementKind.Property, "SetDate")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.SetDate),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.Property, "StatusValue")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            DefaultValue = "Released",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.StatusValue),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.Property, "Role")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            DefaultValue = "Author",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.Role),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.Property, "OrganizationName")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            DefaultValue = "Hanuri",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.OrganizationName),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.Property, "OrganizationOfficialName")
                        {
                            UseEmptyValueType = true,
                            Category = "CONSTANT",
                            DefaultValue = "Hanuri",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.OrganizationOfficialName),
                            HasQualifier = false
                        },
                        new(DocumentationElementKind.File, "DigitalFile")
                        {
                            Category = "CONSTANT",
                            DefaultValue = string.Empty,
                            MimeType = "application/pdf",
                            SemanticId = CreateVdi2770Semantic(DocumentationSemanticUris.DigitalFile),
                            HasQualifier = false
                        }
                    }
                }
            },
            FilePattern = new DocumentationFilePattern
            {
                BasePath = "/aasx/files/"
            }
        };
    }

    private static DocumentationReference CreateVdi2770Semantic(string uri)
    {
        return new DocumentationReference
        {
            Type = "ExternalReference",
            Keys = new List<DocumentationKey>
            {
                new DocumentationKey("GlobalReference", false, "IRI", uri)
            }
        };
    }

    private static DocumentationReference CreateVdi2770ValueId(string irdi)
    {
        return new DocumentationReference
        {
            Type = "ExternalReference",
            Keys = new List<DocumentationKey>
            {
                new DocumentationKey("GlobalReference", false, "IRDI", irdi)
            }
        };
    }
}

public sealed class DocumentationFilePattern
{
    public string BasePath { get; set; } = "/aasx/files/";
}

public sealed class DocumentationElementTemplate
{
    public DocumentationElementTemplate(DocumentationElementKind kind, string idShort)
    {
        Kind = kind;
        IdShort = idShort;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DocumentationElementKind Kind { get; set; }
    public string IdShort { get; set; }
    public string? ValueType { get; set; }
    public bool UseEmptyValueType { get; set; }
    public string? DefaultValue { get; set; }
    public string? Category { get; set; }
    public string? DescriptionLang { get; set; }
    public DocumentationReference? SemanticId { get; set; }
    public DocumentationReference? ValueId { get; set; }
    public bool HasQualifier { get; set; }
    public bool? Ordered { get; set; }
    public bool? AllowDuplicates { get; set; }
    public string? MimeType { get; set; }
    public List<DocumentationElementTemplate> Children { get; set; } = new();
    public List<DocumentationLangString> LangStrings { get; set; } = new();
}

public enum DocumentationElementKind
{
    Property,
    SubmodelElementCollection,
    File,
    MultiLanguageProperty
}

public sealed class DocumentationReference
{
    public string? Type { get; set; }
    public List<DocumentationKey> Keys { get; set; } = new();
}

public sealed class DocumentationKey
{
    public DocumentationKey()
    {
    }

    public DocumentationKey(string type, bool local, string idType, string value)
    {
        Type = type;
        Local = local;
        IdType = idType;
        Value = value;
    }

    public string Type { get; set; } = string.Empty;
    public bool Local { get; set; }
    public string IdType { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class DocumentationLangString
{
    public DocumentationLangString()
    {
    }

    public DocumentationLangString(string lang, string value)
    {
        Lang = lang;
        Value = value;
    }

    public string Lang { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
