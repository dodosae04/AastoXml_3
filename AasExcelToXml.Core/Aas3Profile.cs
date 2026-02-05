using System.Text.Json.Serialization;

namespace AasExcelToXml.Core;

public sealed class Aas3Profile
{
    public Aas3ReferenceProfile Reference { get; set; } = new();
    public Dictionary<string, List<string>> ElementOrders { get; set; } = new(StringComparer.Ordinal);
    public Aas3DescriptionProfile Description { get; set; } = new();
    public Aas3MultiLanguageValueProfile MultiLanguageValue { get; set; } = new();
    public bool SubmodelElementWrapper { get; set; }
    public List<string> ReferenceElementKeyTypes { get; set; } = new();

    public static Aas3Profile CreateFallback()
    {
        return new Aas3Profile
        {
            Reference = new Aas3ReferenceProfile
            {
                SemanticIdWrapsReference = false,
                ReferenceElementValueWrapsReference = false,
                SubmodelReferenceWrapsReference = true,
                ReferenceTypeMode = Aas3ReferenceTypeMode.Element,
                Key = new Aas3KeyProfile
                {
                    Mode = "Element",
                    ChildElementNames = new List<string> { "type", "value" }
                },
                ReferenceChildOrder = new List<string> { "type", "keys" }
            },
            Description = new Aas3DescriptionProfile
            {
                Mode = "LangStringTextType",
                SubElementNames = new List<string> { "language", "text" }
            },
            MultiLanguageValue = new Aas3MultiLanguageValueProfile
            {
                Mode = "LangStringTextType",
                SubElementNames = new List<string> { "language", "text" }
            },
            ReferenceElementKeyTypes = new List<string> { "Submodel", "Property" }
        };
    }
}

public sealed class Aas3ReferenceProfile
{
    public bool SemanticIdWrapsReference { get; set; }
    public bool ReferenceElementValueWrapsReference { get; set; }
    public bool SubmodelReferenceWrapsReference { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Aas3ReferenceTypeMode ReferenceTypeMode { get; set; } = Aas3ReferenceTypeMode.Element;

    public Aas3KeyProfile Key { get; set; } = new();
    public List<string> ReferenceChildOrder { get; set; } = new();
}

public enum Aas3ReferenceTypeMode
{
    None,
    Attribute,
    Element
}

public sealed class Aas3KeyProfile
{
    public string Mode { get; set; } = "Element";
    public List<string> AttributeNames { get; set; } = new();
    public List<string> ChildElementNames { get; set; } = new();
}

public sealed class Aas3DescriptionProfile
{
    public string Mode { get; set; } = "LangStringTextType";
    public List<string> AttributeNames { get; set; } = new();
    public List<string> SubElementNames { get; set; } = new();
}

public sealed class Aas3MultiLanguageValueProfile
{
    public string Mode { get; set; } = "LangStringTextType";
    public List<string> AttributeNames { get; set; } = new();
    public List<string> SubElementNames { get; set; } = new();
}
