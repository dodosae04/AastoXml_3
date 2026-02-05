using System.Text.Json;
using System.Xml.Linq;
using AasExcelToXml.Core;

namespace ExtractGoldenDocProfile;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("사용법: dotnet run --project tools/ExtractGoldenDocProfile -- <정답_XML> [--version 2|3] [output.json]");
            return 1;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"정답 XML을 찾을 수 없습니다: {inputPath}");
            return 1;
        }

        var version = ResolveVersion(args);
        var outputPath = ResolveOutputPath(args, version);
        var document = XDocument.Load(inputPath, LoadOptions.PreserveWhitespace);
        var profile = DocumentationProfileExtractor.Extract(document);
        if (profile is null)
        {
            Console.Error.WriteLine("Documentation Submodel을 찾을 수 없어 프로파일 추출에 실패했습니다.");
            return 1;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(profile, options));
        Console.WriteLine("정답 Documentation 프로파일 추출 완료");
        Console.WriteLine($"- {outputPath}");
        return 0;
    }

    private static string ResolveDefaultOutputPath()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
        return Path.Combine(repoRoot, "Templates", "golden_doc_profile_v2.json");
    }

    private static string ResolveDefaultOutputPathV3()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
        return Path.Combine(repoRoot, "Templates", "golden_doc_profile_v3.json");
    }

    private static string ResolveOutputPath(string[] args, int version)
    {
        var outputArg = args.FirstOrDefault(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(outputArg))
        {
            return outputArg;
        }

        return version == 3 ? ResolveDefaultOutputPathV3() : ResolveDefaultOutputPath();
    }

    private static int ResolveVersion(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1].Trim() == "3" ? 3 : 2;
            }

            if (args[i].StartsWith("--version=", StringComparison.OrdinalIgnoreCase))
            {
                var value = args[i].Substring("--version=".Length);
                return value.Trim() == "3" ? 3 : 2;
            }
        }

        return 2;
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
                template.ValueType = valueType.Value.Trim();
            }
        }

        if (kind == DocumentationElementKind.SubmodelElementCollection)
        {
            template.Ordered = ParseBool(GetChildValue(element, "ordered"));
            template.AllowDuplicates = ParseBool(GetChildValue(element, "allowDuplicates"));
            template.Children = ParseChildren(element);
        }

        if (kind == DocumentationElementKind.MultiLanguageProperty)
        {
            template.DefaultValue = null;
            template.LangStrings = ParseLangStrings(element);
        }

        return template;
    }

    private static List<DocumentationLangString> ParseLangStrings(XElement element)
    {
        var valueElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (valueElement is null)
        {
            return new List<DocumentationLangString>();
        }

        var results = new List<DocumentationLangString>();
        foreach (var langString in valueElement.Elements())
        {
            if (langString.Name.LocalName == "langString")
            {
                var lang = langString.Attribute("lang")?.Value ?? string.Empty;
                results.Add(new DocumentationLangString(lang, langString.Value));
                continue;
            }

            if (langString.Name.LocalName == "langStringTextType")
            {
                var lang = langString.Elements().FirstOrDefault(e => e.Name.LocalName == "language")?.Value ?? string.Empty;
                var text = langString.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? string.Empty;
                results.Add(new DocumentationLangString(lang, text));
            }
        }

        return results;
    }

    private static IEnumerable<XElement> GetValueChildren(XElement element)
    {
        var valueElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (valueElement is null)
        {
            yield break;
        }

        foreach (var child in valueElement.Elements())
        {
            if (child.Name.LocalName == "submodelElement")
            {
                var inner = child.Elements().FirstOrDefault();
                if (inner is not null)
                {
                    yield return inner;
                }
            }
            else
            {
                yield return child;
            }
        }
    }

    private static DocumentationReference? ParseReference(XElement element, string name)
    {
        var reference = element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (reference is null)
        {
            return null;
        }

        var referenceRoot = reference.Elements().FirstOrDefault(e => e.Name.LocalName == "reference") ?? reference;
        var referenceType = referenceRoot.Attribute("type")?.Value
            ?? referenceRoot.Elements().FirstOrDefault(e => e.Name.LocalName == "type")?.Value;

        var keys = reference.Descendants().Where(e => e.Name.LocalName == "key").ToList();
        if (keys.Count == 0)
        {
            return new DocumentationReference { Type = referenceType };
        }

        return new DocumentationReference
        {
            Type = referenceType,
            Keys = keys.Select(key => new DocumentationKey(
                    ResolveKeyValue(key, "type", key.Attribute("type")?.Value),
                    ResolveKeyValue(key, "local", key.Attribute("local")?.Value) == "true",
                    ResolveKeyValue(key, "idType", key.Attribute("idType")?.Value),
                    ResolveKeyValue(key, "value", key.Value))).ToList()
        };
    }

    private static bool HasQualifier(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == "qualifier")
            {
                return true;
            }

            if (child.Name.LocalName == "qualifiers"
                && child.Elements().Any(e => e.Name.LocalName == "qualifier"))
            {
                return true;
            }
        }

        return false;
    }

    private static string? FindFirstFileValue(XElement documentCollection)
    {
        return documentCollection.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "file")
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == "value")
            ?.Value;
    }

    private static string ResolveFileBasePath(string? fileValue)
    {
        if (string.IsNullOrWhiteSpace(fileValue))
        {
            return "/aasx/files/";
        }

        var index = fileValue.LastIndexOf('/');
        if (index < 0)
        {
            return "/aasx/files/";
        }

        return fileValue[..(index + 1)];
    }

    private static bool? ParseBool(string value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }

    private static string ResolveKeyValue(XElement keyElement, string childName, string? attributeFallback)
    {
        if (!string.IsNullOrWhiteSpace(attributeFallback))
        {
            return attributeFallback;
        }

        var child = keyElement.Elements().FirstOrDefault(e => e.Name.LocalName == childName);
        if (child is not null)
        {
            return child.Value;
        }

        if (string.Equals(childName, "value", StringComparison.Ordinal))
        {
            return keyElement.Value ?? string.Empty;
        }

        return string.Empty;
    }

    private static string BuildIdShortPattern(string idShort)
    {
        if (string.IsNullOrWhiteSpace(idShort))
        {
            return "Document{N}";
        }

        var digits = new string(idShort.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return idShort + "{N}";
        }

        var prefix = idShort[..^digits.Length];
        var token = digits.Length == 2 ? "{N:00}" : "{N}";
        return prefix + token;
    }
}
