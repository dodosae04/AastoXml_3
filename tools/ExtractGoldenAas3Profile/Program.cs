using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using AasExcelToXml.Core;

namespace ExtractGoldenAas3Profile;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("사용법: dotnet run --project tools/ExtractGoldenAas3Profile -- <AAS3_정답_XML> [output.json]");
            return 1;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"정답 AAS3 XML을 찾을 수 없습니다: {inputPath}");
            return 1;
        }

        var outputPath = args.Length > 1 ? args[1] : ResolveDefaultOutputPath();
        var document = XDocument.Load(inputPath, LoadOptions.PreserveWhitespace);
        var profile = Aas3ProfileExtractor.Extract(document);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(profile, options));
        Console.WriteLine("정답 AAS3 프로파일 추출 완료");
        Console.WriteLine($"- {outputPath}");
        return 0;
    }

    private static string ResolveDefaultOutputPath()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
        return Path.Combine(repoRoot, "Templates", "golden_profile_aas3.json");
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

internal static class Aas3ProfileExtractor
{
    private static readonly string[] DefaultElementOrderTargets =
    {
        "environment",
        "assetAdministrationShell",
        "assetInformation",
        "submodels",
        "reference",
        "submodel",
        "submodelElements",
        "submodelElementCollection",
        "property",
        "referenceElement",
        "relationshipElement",
        "entity",
        "file",
        "multiLanguageProperty",
        "conceptDescription"
    };

    public static Aas3Profile Extract(XDocument document)
    {
        var profile = new Aas3Profile
        {
            Reference = ExtractReferenceProfile(document),
            Description = ExtractDescriptionProfile(document),
            MultiLanguageValue = ExtractMultiLanguageValueProfile(document),
            SubmodelElementWrapper = HasSubmodelElementWrapper(document),
            ElementOrders = ExtractElementOrders(document),
            ReferenceElementKeyTypes = ExtractReferenceElementKeyTypes(document)
        };

        return profile;
    }

    private static Aas3ReferenceProfile ExtractReferenceProfile(XDocument document)
    {
        var referenceElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "reference");
        var referenceTypeMode = Aas3ReferenceTypeMode.None;
        if (referenceElement is not null)
        {
            referenceTypeMode = referenceElement.Attribute("type") is not null
                ? Aas3ReferenceTypeMode.Attribute
                : referenceElement.Elements().Any(e => e.Name.LocalName == "type")
                    ? Aas3ReferenceTypeMode.Element
                    : Aas3ReferenceTypeMode.None;
        }

        var keyElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "key");
        var keyProfile = new Aas3KeyProfile();
        if (keyElement is not null)
        {
            if (keyElement.HasAttributes)
            {
                keyProfile.Mode = "Attribute";
                keyProfile.AttributeNames = keyElement.Attributes()
                    .Select(a => a.Name.LocalName)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            else
            {
                keyProfile.Mode = "Element";
                keyProfile.ChildElementNames = keyElement.Elements()
                    .Select(e => e.Name.LocalName)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }

        var semanticId = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "semanticId");
        var semanticWraps = semanticId?.Elements().Any(e => e.Name.LocalName == "reference") == true;

        var refElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "referenceElement");
        var refValue = refElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        var valueWraps = refValue?.Elements().Any(e => e.Name.LocalName == "reference") == true;

        var shell = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "assetAdministrationShell");
        var submodels = shell?.Elements().FirstOrDefault(e => e.Name.LocalName == "submodels");
        var submodelWraps = submodels?.Elements().FirstOrDefault()?.Name.LocalName == "reference";

        var referenceChildOrder = referenceElement?.Elements().Select(e => e.Name.LocalName).ToList()
                                 ?? new List<string> { "type", "keys" };

        return new Aas3ReferenceProfile
        {
            SemanticIdWrapsReference = semanticWraps,
            ReferenceElementValueWrapsReference = valueWraps,
            SubmodelReferenceWrapsReference = submodelWraps,
            ReferenceTypeMode = referenceTypeMode,
            Key = keyProfile,
            ReferenceChildOrder = referenceChildOrder
        };
    }

    private static Aas3DescriptionProfile ExtractDescriptionProfile(XDocument document)
    {
        var description = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "description");
        if (description is null)
        {
            return new Aas3DescriptionProfile();
        }

        var langString = description.Elements().FirstOrDefault(e => e.Name.LocalName == "langString");
        if (langString is not null)
        {
            return new Aas3DescriptionProfile
            {
                Mode = "LangString",
                AttributeNames = langString.Attributes().Select(a => a.Name.LocalName).Distinct(StringComparer.Ordinal).ToList()
            };
        }

        var langStringTextType = description.Elements().FirstOrDefault(e => e.Name.LocalName == "langStringTextType");
        if (langStringTextType is not null)
        {
            return new Aas3DescriptionProfile
            {
                Mode = "LangStringTextType",
                SubElementNames = langStringTextType.Elements().Select(e => e.Name.LocalName).Distinct(StringComparer.Ordinal).ToList()
            };
        }

        return new Aas3DescriptionProfile();
    }

    private static Aas3MultiLanguageValueProfile ExtractMultiLanguageValueProfile(XDocument document)
    {
        var multiLanguageProperty = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "multiLanguageProperty");
        var value = multiLanguageProperty?.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (value is null)
        {
            return new Aas3MultiLanguageValueProfile();
        }

        var langString = value.Elements().FirstOrDefault(e => e.Name.LocalName == "langString");
        if (langString is not null)
        {
            return new Aas3MultiLanguageValueProfile
            {
                Mode = "LangString",
                AttributeNames = langString.Attributes().Select(a => a.Name.LocalName).Distinct(StringComparer.Ordinal).ToList()
            };
        }

        var langStringTextType = value.Elements().FirstOrDefault(e => e.Name.LocalName == "langStringTextType");
        if (langStringTextType is not null)
        {
            return new Aas3MultiLanguageValueProfile
            {
                Mode = "LangStringTextType",
                SubElementNames = langStringTextType.Elements().Select(e => e.Name.LocalName).Distinct(StringComparer.Ordinal).ToList()
            };
        }

        return new Aas3MultiLanguageValueProfile();
    }

    private static bool HasSubmodelElementWrapper(XDocument document)
    {
        var submodelElements = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "submodelElements");
        return submodelElements?.Elements().Any(e => e.Name.LocalName == "submodelElement") == true;
    }

    private static Dictionary<string, List<string>> ExtractElementOrders(XDocument document)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var name in DefaultElementOrderTargets)
        {
            var element = document.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == name);
            if (element is null)
            {
                continue;
            }

            var order = element.Elements().Select(e => e.Name.LocalName).ToList();
            if (order.Count > 0)
            {
                result[name] = order;
            }
        }

        return result;
    }

    private static List<string> ExtractReferenceElementKeyTypes(XDocument document)
    {
        var referenceElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "referenceElement");
        var value = referenceElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        var keyElements = value?.Descendants().Where(e => e.Name.LocalName == "key").ToList();
        if (keyElements is null || keyElements.Count == 0)
        {
            return new List<string>();
        }

        var types = new List<string>();
        foreach (var key in keyElements)
        {
            var type = key.Attribute("type")?.Value
                ?? key.Elements().FirstOrDefault(e => e.Name.LocalName == "type")?.Value
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(type))
            {
                types.Add(type);
            }
        }

        return types;
    }
}
