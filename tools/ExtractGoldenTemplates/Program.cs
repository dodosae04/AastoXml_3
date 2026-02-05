using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using AasExcelToXml.Core;

namespace ExtractGoldenTemplates;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var outputDir = ResolveOutputDir(args);
        var useFallback = args.Any(arg => string.Equals(arg, "--fallback", StringComparison.OrdinalIgnoreCase));

        DocumentationProfile docProfileV2;
        DocumentationProfile docProfileV3;
        Aas3Profile aas3Profile;

        if (useFallback)
        {
            // 정답 XML이 없을 때는 기본 스켈레톤을 생성한다.
            docProfileV2 = DocumentationProfile.CreateFallback();
            docProfileV3 = DocumentationProfile.CreateFallback();
            aas3Profile = Aas3Profile.CreateFallback();
        }
        else
        {
            var aas2Path = ResolveArgValue(args, "--aas2");
            var aas3Path = ResolveArgValue(args, "--aas3");
            if (string.IsNullOrWhiteSpace(aas2Path) || string.IsNullOrWhiteSpace(aas3Path))
            {
                PrintUsage();
                return 1;
            }

            if (!File.Exists(aas2Path) || !File.Exists(aas3Path))
            {
                Console.Error.WriteLine("정답 XML 경로가 올바르지 않습니다.");
                return 1;
            }

            var docV2 = XDocument.Load(aas2Path, LoadOptions.PreserveWhitespace);
            var docV3 = XDocument.Load(aas3Path, LoadOptions.PreserveWhitespace);
            docProfileV2 = DocumentationProfileExtractor.Extract(docV2) ?? DocumentationProfile.CreateFallback();
            docProfileV3 = DocumentationProfileExtractor.Extract(docV3) ?? DocumentationProfile.CreateFallback();
            aas3Profile = Aas3ProfileExtractor.Extract(docV3);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "golden_doc_profile_v2.json"), JsonSerializer.Serialize(docProfileV2, options));
        File.WriteAllText(Path.Combine(outputDir, "golden_doc_profile_v3.json"), JsonSerializer.Serialize(docProfileV3, options));
        File.WriteAllText(Path.Combine(outputDir, "golden_profile_aas3.json"), JsonSerializer.Serialize(aas3Profile, options));

        Console.WriteLine("정답 스켈레톤 템플릿 추출 완료");
        Console.WriteLine($"- {outputDir}");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("사용법: dotnet run --project tools/ExtractGoldenTemplates -- --aas2 <정답_AAS2.xml> --aas3 <정답_AAS3.xml> [--output <dir>]");
        Console.WriteLine("       dotnet run --project tools/ExtractGoldenTemplates -- --fallback [--output <dir>]");
    }

    private static string ResolveOutputDir(string[] args)
    {
        var outputArg = ResolveArgValue(args, "--output");
        if (!string.IsNullOrWhiteSpace(outputArg))
        {
            return outputArg;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
        return Path.Combine(repoRoot, "Templates");
    }

    private static string? ResolveArgValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (args[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i].Substring(key.Length + 1);
            }
        }

        return null;
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

internal static class DocumentationProfileExtractor
{
    public static DocumentationProfile? Extract(XDocument document)
    {
        var documentationSubmodel = FindDocumentationSubmodel(document);
        if (documentationSubmodel is null)
        {
            return null;
        }

        var documentCollection = FindDocumentCollection(documentationSubmodel);
        if (documentCollection is null)
        {
            return null;
        }

        var documentIdShort = GetChildValue(documentCollection, "idShort");
        var documentVersionCollection = FindDocumentVersionCollection(documentCollection);
        var documentVersionIdShort = documentVersionCollection is null
            ? "DocumentVersion01"
            : GetChildValue(documentVersionCollection, "idShort");

        var profile = new DocumentationProfile
        {
            DocumentCollectionIdShortPattern = BuildIdShortPattern(documentIdShort),
            DocumentVersionIdShort = string.IsNullOrWhiteSpace(documentVersionIdShort) ? "DocumentVersion01" : documentVersionIdShort,
            DocumentCollectionCategory = GetChildValue(documentCollection, "category"),
            DocumentCollectionOrdered = ParseBool(GetChildValue(documentCollection, "ordered")),
            DocumentCollectionAllowDuplicates = ParseBool(GetChildValue(documentCollection, "allowDuplicates")),
            DocumentCollectionSemanticId = ParseReference(documentCollection, "semanticId"),
            DocumentCollectionHasQualifier = HasQualifier(documentCollection),
            DocumentFields = ParseChildren(documentCollection)
        };

        var fileValue = FindFirstFileValue(documentCollection);
        var basePath = ResolveFileBasePath(fileValue);
        profile.FilePattern = new DocumentationFilePattern { BasePath = basePath };

        return profile;
    }

    private static XElement? FindDocumentationSubmodel(XDocument document)
    {
        return document.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "submodel"
            && string.Equals(GetChildValue(e, "idShort"), "Documentation", StringComparison.Ordinal));
    }

    private static XElement? FindDocumentCollection(XElement documentationSubmodel)
    {
        return documentationSubmodel.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "submodelElementCollection"
            && (GetChildValue(e, "idShort").StartsWith("Document", StringComparison.Ordinal)));
    }

    private static XElement? FindDocumentVersionCollection(XElement documentCollection)
    {
        return documentCollection.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "submodelElementCollection"
            && GetChildValue(e, "idShort").StartsWith("DocumentVersion", StringComparison.Ordinal));
    }

    private static List<DocumentationElementTemplate> ParseChildren(XElement collection)
    {
        var result = new List<DocumentationElementTemplate>();
        foreach (var child in GetValueChildren(collection))
        {
            var template = ParseElement(child);
            if (template is not null)
            {
                result.Add(template);
            }
        }

        return result;
    }

    private static DocumentationElementTemplate? ParseElement(XElement element)
    {
        var kind = element.Name.LocalName switch
        {
            "property" => DocumentationElementKind.Property,
            "file" => DocumentationElementKind.File,
            "submodelElementCollection" => DocumentationElementKind.SubmodelElementCollection,
            "multiLanguageProperty" => DocumentationElementKind.MultiLanguageProperty,
            _ => (DocumentationElementKind?)null
        };

        if (kind is null)
        {
            return null;
        }

        var defaultValue = kind == DocumentationElementKind.File ? string.Empty : GetChildValue(element, "value");
        var template = new DocumentationElementTemplate(kind.Value, GetChildValue(element, "idShort"))
        {
            Category = GetChildValue(element, "category"),
            DefaultValue = defaultValue,
            MimeType = GetChildValue(element, "mimeType"),
            SemanticId = ParseReference(element, "semanticId"),
            ValueId = ParseReference(element, "valueId"),
            HasQualifier = HasQualifier(element)
        };

        if (kind == DocumentationElementKind.Property)
        {
            var valueType = element.Elements().FirstOrDefault(e => e.Name.LocalName == "valueType");
            if (valueType is null)
            {
                template.ValueType = null;
            }
            else if (string.IsNullOrWhiteSpace(valueType.Value))
            {
                template.UseEmptyValueType = true;
            }
            else
            {
                template.ValueType = valueType.Value;
            }
        }

        if (kind == DocumentationElementKind.SubmodelElementCollection)
        {
            template.Ordered = ParseBool(GetChildValue(element, "ordered"));
            template.AllowDuplicates = ParseBool(GetChildValue(element, "allowDuplicates"));
            foreach (var child in GetValueChildren(element))
            {
                var childTemplate = ParseElement(child);
                if (childTemplate is not null)
                {
                    template.Children.Add(childTemplate);
                }
            }
        }

        if (kind == DocumentationElementKind.MultiLanguageProperty)
        {
            var langs = element.Descendants().Where(e => e.Name.LocalName is "langString" or "langStringTextType").ToList();
            foreach (var lang in langs)
            {
                if (lang.Name.LocalName == "langString")
                {
                    template.LangStrings.Add(new DocumentationLangString(
                        lang.Attribute("lang")?.Value ?? string.Empty,
                        lang.Attribute("text")?.Value ?? string.Empty));
                }
                else
                {
                    template.LangStrings.Add(new DocumentationLangString(
                        lang.Elements().FirstOrDefault(e => e.Name.LocalName == "language")?.Value ?? string.Empty,
                        lang.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? string.Empty));
                }
            }
        }

        return template;
    }

    private static IEnumerable<XElement> GetValueChildren(XElement element)
    {
        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (value is null)
        {
            return Enumerable.Empty<XElement>();
        }

        return value.Elements();
    }

    private static DocumentationReference? ParseReference(XElement element, string name)
    {
        var refElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (refElement is null)
        {
            return null;
        }

        var reference = refElement.Elements().FirstOrDefault(e => e.Name.LocalName == "reference");
        var referenceRoot = reference ?? refElement;

        var result = new DocumentationReference
        {
            Type = referenceRoot.Attribute("type")?.Value,
            Keys = new List<DocumentationKey>()
        };

        var keys = referenceRoot.Descendants().FirstOrDefault(e => e.Name.LocalName == "keys");
        if (keys is null)
        {
            return result;
        }

        foreach (var key in keys.Elements().Where(e => e.Name.LocalName == "key"))
        {
            result.Keys.Add(new DocumentationKey(
                key.Attribute("type")?.Value ?? string.Empty,
                key.Attribute("local")?.Value == "true",
                key.Attribute("idType")?.Value,
                key.Value));
        }

        return result;
    }

    private static bool HasQualifier(XElement element)
    {
        return element.Elements().Any(e => e.Name.LocalName == "qualifier" || e.Name.LocalName == "qualifiers");
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }

    private static bool? ParseBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIdShortPattern(string idShort)
    {
        if (string.IsNullOrWhiteSpace(idShort))
        {
            return "Document{N}";
        }

        if (idShort.Contains("01", StringComparison.Ordinal))
        {
            return idShort.Replace("01", "{N:00}");
        }

        return idShort;
    }

    private static string ResolveFileBasePath(string? fileValue)
    {
        if (string.IsNullOrWhiteSpace(fileValue))
        {
            return "/aasx/files/";
        }

        if (fileValue.Contains('/') || fileValue.Contains('\\'))
        {
            return fileValue[..(fileValue.LastIndexOfAny(new[] { '/', '\\' }) + 1)];
        }

        return "/aasx/files/";
    }

    private static string? FindFirstFileValue(XElement element)
    {
        return element.Descendants().FirstOrDefault(e => e.Name.LocalName == "file")
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == "value")?.Value;
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
        return document.Descendants().Any(e => e.Name.LocalName == "submodelElement");
    }

    private static Dictionary<string, List<string>> ExtractElementOrders(XDocument document)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var name in DefaultElementOrderTargets)
        {
            var element = document.Descendants().FirstOrDefault(e => e.Name.LocalName == name);
            if (element is null)
            {
                continue;
            }

            result[name] = element.Elements().Select(e => e.Name.LocalName).Distinct(StringComparer.Ordinal).ToList();
        }

        return result;
    }

    private static List<string> ExtractReferenceElementKeyTypes(XDocument document)
    {
        var referenceElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "referenceElement");
        var value = referenceElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        var reference = value?.Elements().FirstOrDefault(e => e.Name.LocalName == "reference") ?? value;
        var keys = reference?.Elements().FirstOrDefault(e => e.Name.LocalName == "keys");
        if (keys is null)
        {
            return new List<string>();
        }

        return keys.Elements()
            .Where(e => e.Name.LocalName == "key")
            .Select(e => e.Attribute("type")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList()!;
    }
}
