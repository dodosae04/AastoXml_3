using System.IO;
using System.Xml.Linq;

namespace AasExcelToXml.Core;

public sealed class DocumentationSkeletonBuilderV2
{
    private readonly XNamespace _aasNs;
    private readonly ConvertOptions _options;
    private readonly SpecDiagnostics _diagnostics;
    private readonly DocumentationProfile _profile;
    private readonly DocumentIdGenerator _documentIdGenerator;
    private int _primaryDocumentIndex = 1;

    public DocumentationSkeletonBuilderV2(XNamespace aasNs, ConvertOptions options, SpecDiagnostics diagnostics, DocumentationProfile profile, DocumentIdGenerator documentIdGenerator)
    {
        _aasNs = aasNs;
        _options = options;
        _diagnostics = diagnostics;
        _profile = profile;
        _documentIdGenerator = documentIdGenerator;
    }

    public IEnumerable<XElement> Build(SubmodelSpec submodel, string aasIdShort)
    {
        // 정답 XML에서 추출한 Documentation 스켈레톤(프로파일) 구조를 유지한 채 값만 주입한다.
        var documentInputs = ExtractDocumentInputs(submodel.Elements);
        if (documentInputs.Count == 0)
        {
            documentInputs.Add(new DocumentInput(1));
        }

        var overrides = DocumentationOverrideLoader.Load(_options, _diagnostics);
        var primaryInput = SelectPrimaryDocumentInput(documentInputs, overrides);
        _primaryDocumentIndex = primaryInput.Index;
        if (!_options.IncludeAllDocumentation && documentInputs.Count > 1)
        {
            documentInputs = new List<DocumentInput> { primaryInput };
        }

        var list = new List<XElement>();
        foreach (var input in documentInputs)
        {
            var idShort = DocumentationIdShortMapper.ResolveDocumentCollectionIdShort(
                input.CollectionIdShort,
                input.Index,
                aasIdShort,
                _profile.DocumentCollectionIdShortPattern);
            if (string.IsNullOrWhiteSpace(idShort))
            {
                continue;
            }

            var element = BuildDocumentSpecCollection(idShort, input, overrides, primaryInput);
            list.Add(WrapSubmodelElement(element));
        }

        return list;
    }

    private XElement BuildDocumentSpecCollection(string idShort, DocumentInput input, DocumentationOverrideProfile? overrides, DocumentInput primaryInput)
    {
        var overrideRule = overrides?.Resolve(input);
        var documentId = _documentIdGenerator.NextId();
        var isPrimary = ResolveIsPrimaryValue(input, overrides, primaryInput);

        var classId = overrideRule?.DocumentClassId ?? ResolveFieldValue(input, "classid", "documentclassid");
        var className = overrideRule?.DocumentClassName ?? ResolveFieldValue(input, "classname", "documentclassname", "documentclass");
        var classificationSystem = overrideRule?.DocumentClassificationSystem ?? ResolveFieldValue(input, "classificationsystem", "documentclassificationsystem");
        var language = overrideRule?.Language ?? ResolveFieldValue(input, "language");
        var title = ResolveFieldValue(input, "title", "documenttitle", "documentname") ?? input.Name;
        var summary = ResolveFieldValue(input, "summary");
        var keyWords = ResolveFieldValue(input, "keywords", "keyword");
        var setDate = ResolveFieldValue(input, "setdate");
        var statusValue = ResolveFieldValue(input, "statusvalue");
        var role = ResolveFieldValue(input, "role");
        var organizationName = ResolveFieldValue(input, "organizationname");
        var organizationOfficialName = ResolveFieldValue(input, "organizationofficialname");

        classId ??= _options.DocumentDefaultClassId;
        className ??= _options.DocumentDefaultClassName;
        classificationSystem ??= _options.DocumentDefaultClassificationSystem;
        language ??= _options.DocumentDefaultLanguage;
        title ??= _options.InputFileName;
        if (string.IsNullOrWhiteSpace(setDate) && _options.UseFixedSetDate && !string.IsNullOrWhiteSpace(_options.DocumentDefaultSetDate))
        {
            setDate = _options.DocumentDefaultSetDate;
        }
        statusValue ??= _options.DocumentDefaultStatusValue;
        role ??= _options.DocumentDefaultRole;
        organizationName ??= _options.DocumentDefaultOrganizationName;
        organizationOfficialName ??= _options.DocumentDefaultOrganizationOfficialName;

        var versionChildren = new List<XElement>
        {
            BuildVdiProperty("Language1", language, "xs:string", "Language"),
            BuildVdiMultiLanguageProperty("Title", title, "Description/Title", "EN"),
            BuildVdiMultiLanguageProperty("Summary", summary, "Description/Summary", "kr"),
            BuildVdiMultiLanguageProperty("KeyWords", keyWords, "Description/KeyWords", "kr"),
            BuildVdiProperty("SetDate", setDate, "xs:string", "SetDate"),
            BuildVdiProperty("StatusValue", statusValue, "xs:string", "StatusValue"),
            BuildVdiProperty("Role", role, "xs:string", "Role"),
            BuildVdiProperty("OrganizationName", organizationName, "xs:string", "OrganizationName"),
            BuildVdiProperty("OrganizationOfficialName", organizationOfficialName, "xs:string", "OrganizationOfficialName"),
            BuildVdiFile("DigitalFile", input)
        };

        return new XElement(_aasNs + "submodelElementCollection",
            new XElement(_aasNs + "idShort", idShort),
            new XElement(_aasNs + "category", "CONSTANT"),
            new XElement(_aasNs + "kind", "Instance"),
            new XElement(_aasNs + "ordered", "false"),
            new XElement(_aasNs + "allowDuplicates", "false"),
            CreateDescription(idShort),
            CreateSemanticId(CreateVdi2770Semantic("Document")),
            CreateQualifier(),
            new XElement(_aasNs + "value",
                WrapSubmodelElement(BuildVdiProperty("DocumentId", documentId, "xs:string", "DocumentId")),
                WrapSubmodelElement(BuildVdiProperty("IsPrimary", isPrimary, "xs:boolean", "IsPrimaryDocumentId")),
                WrapSubmodelElement(BuildVdiProperty("ClassId", classId, "xs:string", "DocumentClassId")),
                WrapSubmodelElement(BuildVdiProperty("ClassName", className, "xs:string", "DocumentClassName")),
                WrapSubmodelElement(BuildVdiProperty("ClassificationSystem", classificationSystem, "xs:string", "DocumentClassificationSystem")),
                WrapSubmodelElement(BuildVdiDocumentVersionCollection("DocumentVersion", versionChildren))));
    }

    private XElement BuildVdiDocumentVersionCollection(string idShort, IEnumerable<XElement> childrenElements)
    {
        return new XElement(_aasNs + "submodelElementCollection",
            new XElement(_aasNs + "idShort", idShort),
            new XElement(_aasNs + "category", "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            new XElement(_aasNs + "ordered", "false"),
            new XElement(_aasNs + "allowDuplicates", "false"),
            CreateDescription(idShort),
            CreateSemanticId(CreateVdi2770Semantic("DocumentVersion")),
            CreateQualifier(),
            new XElement(_aasNs + "value",
                childrenElements.Select(WrapSubmodelElement)));
    }

    private XElement BuildVdiProperty(string idShort, string? value, string valueType, string semanticSuffix)
    {
        return new XElement(_aasNs + "property",
            new XElement(_aasNs + "idShort", idShort),
            new XElement(_aasNs + "category", "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(idShort),
            CreateSemanticId(CreateVdi2770Semantic(semanticSuffix)),
            CreateQualifier(),
            new XElement(_aasNs + "valueType", valueType),
            new XElement(_aasNs + "value", value ?? string.Empty));
    }

    private XElement BuildVdiMultiLanguageProperty(string idShort, string? value, string semanticSuffix, string lang)
    {
        var valueElement = new XElement(_aasNs + "value",
            string.IsNullOrWhiteSpace(value)
                ? new XElement(_aasNs + "langString", new XAttribute("lang", lang))
                : new XElement(_aasNs + "langString", new XAttribute("lang", lang), value));

        return new XElement(_aasNs + "multiLanguageProperty",
            new XElement(_aasNs + "idShort", idShort),
            new XElement(_aasNs + "category", "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(idShort),
            CreateSemanticId(CreateVdi2770Semantic(semanticSuffix)),
            CreateQualifier(),
            valueElement);
    }

    private XElement BuildVdiFile(string idShort, DocumentInput input)
    {
        var resolvedPath = ResolveDigitalFilePath(input);
        return new XElement(_aasNs + "file",
            new XElement(_aasNs + "idShort", idShort),
            new XElement(_aasNs + "category", "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(idShort),
            CreateSemanticId(CreateVdi2770Semantic("DigitalFile")),
            CreateQualifier(),
            new XElement(_aasNs + "value", resolvedPath ?? string.Empty),
            new XElement(_aasNs + "mimeType", ResolveContentType(resolvedPath ?? input.FilePath) ?? string.Empty));
    }

    private DocumentationReference CreateVdi2770Semantic(string suffix)
    {
        return new DocumentationReference
        {
            Type = "ExternalReference",
            Keys = new List<DocumentationKey>
            {
                new DocumentationKey("GlobalReference", false, "IRI", $"http://admin-shell.io/vdi/2770/1/0/{suffix}")
            }
        };
    }

    private string? ResolveFieldValue(DocumentInput input, params string[] normalizedKeys)
    {
        foreach (var key in normalizedKeys)
        {
            if (input.Fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ResolveDigitalFilePath(DocumentInput input)
    {
        if (string.IsNullOrWhiteSpace(input.FilePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(input.FilePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return "/aasx/files/" + fileName;
    }

    private static string? ResolveContentType(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
    }

    private static string? ResolveBooleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed ? "true" : "false";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "y" or "yes" => "true",
            "0" or "n" or "no" => "false",
            _ => value
        };
    }

    private string? ResolveIsPrimaryValue(DocumentInput input, DocumentationOverrideProfile? overrides, DocumentInput primaryInput)
    {
        var overrideRule = overrides?.Resolve(input);
        var explicitValue = ResolveBooleanValue(overrideRule?.IsPrimaryDocumentId ?? ResolveFieldValue(input, "isprimarydocumentid", "isprimary"));
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        return ReferenceEquals(input, primaryInput) ? "true" : "false";
    }

    private XElement BuildTemplateCollection(string idShort, List<DocumentationElementTemplate> templates, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var category = _profile.DocumentCollectionCategory ?? "PARAMETER";
        var ordered = _profile.DocumentCollectionOrdered ?? false;
        var allowDuplicates = _profile.DocumentCollectionAllowDuplicates ?? false;
        return new XElement(_aasNs + "submodelElementCollection",
            new XElement(_aasNs + "idShort", idShort),
            new XElement(_aasNs + "category", category),
            new XElement(_aasNs + "kind", "Instance"),
            new XElement(_aasNs + "ordered", ordered ? "true" : "false"),
            new XElement(_aasNs + "allowDuplicates", allowDuplicates ? "true" : "false"),
            CreateDescription(idShort),
            CreateSemanticId(_profile.DocumentCollectionSemanticId),
            _profile.DocumentCollectionHasQualifier ? CreateQualifier() : null,
            new XElement(_aasNs + "value",
                templates.Select(template => WrapSubmodelElement(BuildTemplateElement(template, input, fieldMap))))
        );
    }

    private XElement BuildTemplateElement(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        return template.Kind switch
        {
            DocumentationElementKind.Property => BuildProperty(template, input, fieldMap),
            DocumentationElementKind.File => BuildFile(template, input, fieldMap),
            DocumentationElementKind.SubmodelElementCollection => BuildCollection(template, input, fieldMap),
            DocumentationElementKind.MultiLanguageProperty => BuildMultiLanguageProperty(template, input, fieldMap),
            _ => BuildProperty(template, input, fieldMap)
        };
    }

    private XElement BuildProperty(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var value = fieldMap.ResolvePropertyValue(template, input);
        return new XElement(_aasNs + "property",
            new XElement(_aasNs + "idShort", template.IdShort),
            new XElement(_aasNs + "category", template.Category ?? "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(template.IdShort),
            CreateSemanticId(template.SemanticId),
            template.HasQualifier ? CreateQualifier() : null,
            CreateValueTypeElement(template),
            new XElement(_aasNs + "value", value ?? template.DefaultValue ?? string.Empty),
            template.ValueId is null ? null : CreateValueId(template.ValueId)
        );
    }

    private XElement BuildFile(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var resolvedPath = fieldMap.ResolveFilePath(input, _profile.FilePattern?.BasePath ?? "/aasx/files/");
        return new XElement(_aasNs + "file",
            new XElement(_aasNs + "idShort", template.IdShort),
            new XElement(_aasNs + "category", template.Category ?? "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(template.IdShort),
            CreateSemanticId(template.SemanticId),
            template.HasQualifier ? CreateQualifier() : null,
            new XElement(_aasNs + "value", resolvedPath ?? template.DefaultValue ?? string.Empty),
            new XElement(_aasNs + "mimeType", template.MimeType ?? string.Empty)
        );
    }

    private XElement BuildCollection(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var ordered = template.Ordered ?? false;
        var allowDuplicates = template.AllowDuplicates ?? false;
        return new XElement(_aasNs + "submodelElementCollection",
            new XElement(_aasNs + "idShort", template.IdShort),
            new XElement(_aasNs + "category", template.Category ?? "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            new XElement(_aasNs + "ordered", ordered ? "true" : "false"),
            new XElement(_aasNs + "allowDuplicates", allowDuplicates ? "true" : "false"),
            CreateDescription(template.IdShort),
            CreateSemanticId(template.SemanticId),
            template.HasQualifier ? CreateQualifier() : null,
            new XElement(_aasNs + "value",
                template.Children.Select(child => WrapSubmodelElement(BuildTemplateElement(child, input, fieldMap))))
        );
    }

    private XElement BuildMultiLanguageProperty(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var resolved = fieldMap.ResolveMultiLanguageValue(template.IdShort, input);
        var langStrings = template.LangStrings.Count > 0
            ? template.LangStrings
            : new List<DocumentationLangString> { new("EN", string.Empty) };

        var valueElement = new XElement(_aasNs + "value",
            langStrings.Select(lang =>
            {
                var value = resolved ?? lang.Value ?? string.Empty;
                return string.IsNullOrWhiteSpace(value)
                    ? new XElement(_aasNs + "langString", new XAttribute("lang", lang.Lang))
                    : new XElement(_aasNs + "langString", new XAttribute("lang", lang.Lang), value);
            })
        );

        return new XElement(_aasNs + "multiLanguageProperty",
            new XElement(_aasNs + "idShort", template.IdShort),
            new XElement(_aasNs + "category", template.Category ?? "PARAMETER"),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(template.IdShort),
            CreateSemanticId(template.SemanticId),
            template.HasQualifier ? CreateQualifier() : null,
            valueElement
        );
    }

    private XElement CreateDescription(string name)
    {
        var description = new XElement(_aasNs + "description",
            new XElement(_aasNs + "langString",
                new XAttribute("lang", "en")
            )
        );

        if (_options.IncludeKoreanDescription)
        {
            description.Add(
                new XElement(_aasNs + "langString",
                    new XAttribute("lang", "ko"),
                    name
                )
            );
        }

        return description;
    }

    private XElement? CreateSemanticId(DocumentationReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        return new XElement(_aasNs + "semanticId",
            new XElement(_aasNs + "keys",
                reference.Keys.Select(CreateKey)
            )
        );
    }

    private XElement CreateValueId(DocumentationReference reference)
    {
        return new XElement(_aasNs + "valueId",
            new XElement(_aasNs + "keys",
                reference.Keys.Select(CreateKey)
            )
        );
    }

    private XElement CreateQualifier()
    {
        return new XElement(_aasNs + "qualifier");
    }

    private XElement CreateValueTypeElement(DocumentationElementTemplate template)
    {
        if (template.UseEmptyValueType || string.IsNullOrWhiteSpace(template.ValueType))
        {
            return new XElement(_aasNs + "valueType");
        }

        return new XElement(_aasNs + "valueType", template.ValueType);
    }

    private XElement CreateKey(DocumentationKey key)
    {
        return new XElement(_aasNs + "key",
            new XAttribute("type", key.Type),
            new XAttribute("local", key.Local ? "true" : "false"),
            new XAttribute("idType", key.IdType),
            key.Value
        );
    }

    private XElement WrapSubmodelElement(XElement element)
    {
        return new XElement(_aasNs + "submodelElement", element);
    }

    private List<DocumentInput> ExtractDocumentInputs(List<ElementSpec> elements)
    {
        var groups = elements
            .Where(IsDocumentationInputElement)
            .Where(e => !string.IsNullOrWhiteSpace(e.Collection))
            .GroupBy(e => e.Collection);

        var result = new List<DocumentInput>();
        var index = 1;
        foreach (var group in groups)
        {
            var input = new DocumentInput(index++);
            input.CollectionIdShort = group.Key;
            foreach (var element in group)
            {
                var key = NormalizeMatchKey(element.IdShort);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    input.Fields[key] = element.Value ?? string.Empty;
                }

                if (key.Contains("documentname") || key.Contains("documenttitle") || key == "title")
                {
                    input.Name = element.Value;
                    continue;
                }

                if (key.Contains("documenttype") || key.Contains("documentclass") || key.Contains("doctype"))
                {
                    input.Type = element.Value;
                    continue;
                }

                if (key.Contains("documentfile") || key.Contains("filepath") || key.Contains("digitalfile"))
                {
                    input.FilePath = element.Value;
                    continue;
                }

            }

            result.Add(input);
        }

        return result;
    }

    private DocumentInput SelectPrimaryDocumentInput(IEnumerable<DocumentInput> inputs, DocumentationOverrideProfile? overrides)
    {
        foreach (var input in inputs)
        {
            var overrideRule = overrides?.Resolve(input);
            var resolved = ResolveBooleanValue(overrideRule?.IsPrimaryDocumentId ?? ResolveFieldValue(input, "isprimarydocumentid", "isprimary"));
            if (string.Equals(resolved, "true", StringComparison.OrdinalIgnoreCase))
            {
                return input;
            }
        }

        return inputs.MinBy(input => input.Index)!;
    }

    private static string NormalizeMatchKey(string value)
    {
        var filtered = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return filtered.ToLowerInvariant();
    }

    private static bool IsDocumentationInputElement(ElementSpec element)
    {
        if (element.Kind == ElementKind.DocumentationInput)
        {
            return true;
        }

        if (element.Kind != ElementKind.Property)
        {
            return false;
        }

        var key = NormalizeMatchKey(element.IdShort);
        return key.Contains("documentname")
            || key.Contains("documenttitle")
            || key == "title"
            || key == "documentid"
            || key.Contains("documentversionid")
            || key.Contains("isprimary")
            || key.Contains("classid")
            || key.Contains("classname")
            || key.Contains("classificationsystem")
            || key.Contains("documenttype")
            || key.Contains("documentclass")
            || key.Contains("doctype")
            || key.Contains("documentfile")
            || key.Contains("filepath")
            || key.Contains("digitalfile")
            || key.Contains("language")
            || key.Contains("summary")
            || key.Contains("keywords")
            || key.Contains("keyword")
            || key.Contains("setdate")
            || key.Contains("statusvalue")
            || key.Contains("role")
            || key.Contains("organizationname")
            || key.Contains("organizationofficialname");
    }

}

internal sealed class DocumentationFieldMap
{
    private DocumentationFieldMap(
        string? titleIdShort,
        string? documentClassIdShort,
        string? documentClassNameIdShort,
        string? documentClassificationSystemIdShort,
        string? documentVersionIdShort,
        string? fileIdShort,
        string? languageIdShort,
        string? documentIdShort,
        string? isPrimaryDocumentIdShort,
        string? statusValueIdShort,
        string? roleIdShort,
        string? organizationNameIdShort,
        string? organizationOfficialNameIdShort,
        string? setDateIdShort,
        DocumentationOverrideProfile? overrides,
        ConvertOptions options,
        int primaryDocumentIndex)
    {
        TitleIdShort = titleIdShort;
        DocumentClassIdShort = documentClassIdShort;
        DocumentClassNameIdShort = documentClassNameIdShort;
        DocumentClassificationSystemIdShort = documentClassificationSystemIdShort;
        DocumentVersionIdShort = documentVersionIdShort;
        FileIdShort = fileIdShort;
        LanguageIdShort = languageIdShort;
        DocumentIdShort = documentIdShort;
        IsPrimaryDocumentIdShort = isPrimaryDocumentIdShort;
        StatusValueIdShort = statusValueIdShort;
        RoleIdShort = roleIdShort;
        OrganizationNameIdShort = organizationNameIdShort;
        OrganizationOfficialNameIdShort = organizationOfficialNameIdShort;
        SetDateIdShort = setDateIdShort;
        Overrides = overrides;
        _options = options;
        _primaryDocumentIndex = primaryDocumentIndex;
    }

    public string? TitleIdShort { get; }
    public string? DocumentClassIdShort { get; }
    public string? DocumentClassNameIdShort { get; }
    public string? DocumentClassificationSystemIdShort { get; }
    public string? DocumentVersionIdShort { get; }
    public string? FileIdShort { get; }
    public string? LanguageIdShort { get; }
    public string? DocumentIdShort { get; }
    public string? IsPrimaryDocumentIdShort { get; }
    public string? StatusValueIdShort { get; }
    public string? RoleIdShort { get; }
    public string? OrganizationNameIdShort { get; }
    public string? OrganizationOfficialNameIdShort { get; }
    public string? SetDateIdShort { get; }
    private DocumentationOverrideProfile? Overrides { get; }
    private readonly ConvertOptions _options;
    private readonly int _primaryDocumentIndex;
    private readonly Dictionary<int, DocumentationOverrideRule?> _overrideCache = new();

    public static DocumentationFieldMap Create(
        DocumentationProfile profile,
        DocumentationOverrideProfile? overrides,
        ConvertOptions options,
        int primaryDocumentIndex)
    {
        var allTemplates = Flatten(profile.DocumentFields);
        string? Find(params string[] keywords)
        {
            return allTemplates.FirstOrDefault(t =>
                (t.Kind == DocumentationElementKind.Property || t.Kind == DocumentationElementKind.MultiLanguageProperty)
                && keywords.Any(k => NormalizeMatchKey(t.IdShort).Contains(k)))?.IdShort;
        }

        var title = Find("title", "documentname");
        var documentClassId = Find("documentclassid");
        var documentClassName = allTemplates.FirstOrDefault(t =>
            (t.Kind == DocumentationElementKind.Property || t.Kind == DocumentationElementKind.MultiLanguageProperty)
            && MatchesClassName(NormalizeMatchKey(t.IdShort)))?.IdShort;
        var documentClassificationSystem = Find("documentclassificationsystem");
        var documentVersionId = Find("documentversionid");
        var language = Find("language");
        var documentId = allTemplates.FirstOrDefault(t =>
                (t.Kind == DocumentationElementKind.Property || t.Kind == DocumentationElementKind.MultiLanguageProperty)
                && NormalizeMatchKey(t.IdShort) == "documentid")
            ?.IdShort;
        var isPrimaryDocumentId = Find("isprimarydocumentid");
        var statusValue = Find("statusvalue");
        var role = Find("role");
        var organizationName = Find("organizationname");
        var organizationOfficialName = Find("organizationofficialname");
        var setDate = Find("setdate");
        var file = allTemplates.FirstOrDefault(t => t.Kind == DocumentationElementKind.File)?.IdShort;

        return new DocumentationFieldMap(
            title,
            documentClassId,
            documentClassName,
            documentClassificationSystem,
            documentVersionId,
            file,
            language,
            documentId,
            isPrimaryDocumentId,
            statusValue,
            role,
            organizationName,
            organizationOfficialName,
            setDate,
            overrides,
            options,
            primaryDocumentIndex);
    }

    public string? ResolvePropertyValue(DocumentationElementTemplate template, DocumentInput input)
    {
        var idShort = template.IdShort;
        if (!string.IsNullOrWhiteSpace(TitleIdShort) && string.Equals(idShort, TitleIdShort, StringComparison.Ordinal))
        {
            return input.Name;
        }

        if (!string.IsNullOrWhiteSpace(DocumentIdShort) && string.Equals(idShort, DocumentIdShort, StringComparison.Ordinal))
        {
            return ResolveDocumentId(input);
        }

        if (!string.IsNullOrWhiteSpace(IsPrimaryDocumentIdShort) && string.Equals(idShort, IsPrimaryDocumentIdShort, StringComparison.Ordinal))
        {
            return ResolveIsPrimaryDocumentId(input);
        }

        if (!string.IsNullOrWhiteSpace(DocumentClassIdShort) && string.Equals(idShort, DocumentClassIdShort, StringComparison.Ordinal))
        {
            return ResolveDocumentClassId(input);
        }

        if (!string.IsNullOrWhiteSpace(DocumentClassNameIdShort) && string.Equals(idShort, DocumentClassNameIdShort, StringComparison.Ordinal))
        {
            return ResolveDocumentClassName(input);
        }

        if (!string.IsNullOrWhiteSpace(DocumentClassificationSystemIdShort)
            && string.Equals(idShort, DocumentClassificationSystemIdShort, StringComparison.Ordinal))
        {
            return ResolveDocumentClassificationSystem(input);
        }

        if (!string.IsNullOrWhiteSpace(DocumentVersionIdShort) && string.Equals(idShort, DocumentVersionIdShort, StringComparison.Ordinal))
        {
            return ResolveDocumentVersionId(input);
        }

        if (!string.IsNullOrWhiteSpace(LanguageIdShort) && string.Equals(idShort, LanguageIdShort, StringComparison.Ordinal))
        {
            return ResolveLanguage(input);
        }

        if (!string.IsNullOrWhiteSpace(StatusValueIdShort) && string.Equals(idShort, StatusValueIdShort, StringComparison.Ordinal))
        {
            return ResolveStatusValue(input);
        }

        if (!string.IsNullOrWhiteSpace(RoleIdShort) && string.Equals(idShort, RoleIdShort, StringComparison.Ordinal))
        {
            return ResolveRole(input);
        }

        if (!string.IsNullOrWhiteSpace(OrganizationNameIdShort) && string.Equals(idShort, OrganizationNameIdShort, StringComparison.Ordinal))
        {
            return ResolveOrganizationName(input);
        }

        if (!string.IsNullOrWhiteSpace(OrganizationOfficialNameIdShort)
            && string.Equals(idShort, OrganizationOfficialNameIdShort, StringComparison.Ordinal))
        {
            return ResolveOrganizationOfficialName(input);
        }

        if (!string.IsNullOrWhiteSpace(SetDateIdShort) && string.Equals(idShort, SetDateIdShort, StringComparison.Ordinal))
        {
            return ResolveSetDate(input);
        }

        return null;
    }

    public string? ResolveMultiLanguageValue(string idShort, DocumentInput input)
    {
        if (!string.IsNullOrWhiteSpace(TitleIdShort) && string.Equals(idShort, TitleIdShort, StringComparison.Ordinal))
        {
            return input.Name;
        }

        return null;
    }

    public string? ResolveFilePath(DocumentInput input, string basePath)
    {
        if (string.IsNullOrWhiteSpace(FileIdShort))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(input.FilePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(input.FilePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return $"{basePath}{fileName}";
    }

    private static IEnumerable<DocumentationElementTemplate> Flatten(IEnumerable<DocumentationElementTemplate> templates)
    {
        foreach (var template in templates)
        {
            yield return template;
            if (template.Children.Count > 0)
            {
                foreach (var child in Flatten(template.Children))
                {
                    yield return child;
                }
            }
        }
    }

    private static string NormalizeMatchKey(string value)
    {
        var filtered = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return filtered.ToLowerInvariant();
    }

    private static bool MatchesClassName(string key)
    {
        if (key.Contains("documentclassid", StringComparison.Ordinal))
        {
            return false;
        }

        return key.Contains("documentclassname")
            || key.Contains("documentclass")
            || key.Contains("documenttype")
            || key.Contains("doctype");
    }

    private DocumentationOverrideRule? ResolveOverride(DocumentInput input)
    {
        if (Overrides is null)
        {
            return null;
        }

        if (_overrideCache.TryGetValue(input.Index, out var cached))
        {
            return cached;
        }

        var resolved = Overrides.Resolve(input);
        _overrideCache[input.Index] = resolved;
        return resolved;
    }

    private string? ResolveDocumentId(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        if (!string.IsNullOrWhiteSpace(overrideRule?.DocumentId))
        {
            return overrideRule.DocumentId;
        }

        return DocumentIdGenerator.Create(input.Name, input.Type, input.FilePath);
    }

    private string? ResolveIsPrimaryDocumentId(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        if (!string.IsNullOrWhiteSpace(overrideRule?.IsPrimaryDocumentId))
        {
            return overrideRule.IsPrimaryDocumentId;
        }

        var value = GetFieldValue(input, IsPrimaryDocumentIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return ResolveBooleanValue(value);
        }

        if (string.IsNullOrWhiteSpace(input.Name)
            && string.IsNullOrWhiteSpace(input.Type)
            && string.IsNullOrWhiteSpace(input.FilePath))
        {
            return null;
        }

        return input.Index == _primaryDocumentIndex ? "true" : "false";
    }

    private string? ResolveDocumentClassId(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        if (!string.IsNullOrWhiteSpace(overrideRule?.DocumentClassId))
        {
            return overrideRule.DocumentClassId;
        }

        var value = GetFieldValue(input, DocumentClassIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.DocumentClassId;
    }

    private string? ResolveDocumentClassName(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        if (!string.IsNullOrWhiteSpace(overrideRule?.DocumentClassName))
        {
            return overrideRule.DocumentClassName;
        }

        var value = GetFieldValue(input, DocumentClassNameIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.DocumentClassName;
    }

    private string? ResolveDocumentClassificationSystem(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        if (!string.IsNullOrWhiteSpace(overrideRule?.DocumentClassificationSystem))
        {
            return overrideRule.DocumentClassificationSystem;
        }

        var value = GetFieldValue(input, DocumentClassificationSystemIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.DocumentClassificationSystem;
    }

    private string? ResolveDocumentVersionId(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        return !string.IsNullOrWhiteSpace(overrideRule?.DocumentVersionId)
            ? overrideRule.DocumentVersionId
            : null;
    }

    private string? ResolveLanguage(DocumentInput input)
    {
        var overrideRule = ResolveOverride(input);
        if (!string.IsNullOrWhiteSpace(overrideRule?.Language))
        {
            return overrideRule.Language;
        }

        var value = GetFieldValue(input, LanguageIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(input.Name)
            && string.IsNullOrWhiteSpace(input.Type)
            && string.IsNullOrWhiteSpace(input.FilePath)
            ? null
            : "kr";
    }

    private string? ResolveStatusValue(DocumentInput input)
    {
        var value = GetFieldValue(input, StatusValueIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.StatusValue;
    }

    private string? ResolveRole(DocumentInput input)
    {
        var value = GetFieldValue(input, RoleIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.Role;
    }

    private string? ResolveOrganizationName(DocumentInput input)
    {
        var value = GetFieldValue(input, OrganizationNameIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.OrganizationName;
    }

    private string? ResolveOrganizationOfficialName(DocumentInput input)
    {
        var value = GetFieldValue(input, OrganizationOfficialNameIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ResolveDefaults(input)?.OrganizationOfficialName;
    }

    private string? ResolveSetDate(DocumentInput input)
    {
        var value = GetFieldValue(input, SetDateIdShort);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return _options.UseFixedSetDate && !string.IsNullOrWhiteSpace(_options.DocumentDefaultSetDate)
            ? _options.DocumentDefaultSetDate
            : null;
    }

    private static string? ResolveBooleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed ? "true" : "false";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "y" or "yes" => "true",
            "0" or "n" or "no" => "false",
            _ => value
        };
    }

    private static string? GetFieldValue(DocumentInput input, string? idShort)
    {
        if (string.IsNullOrWhiteSpace(idShort))
        {
            return null;
        }

        var key = NormalizeMatchKey(idShort);
        return input.Fields.TryGetValue(key, out var value) ? value : null;
    }

    private DocumentTypeDefaults? ResolveDefaults(DocumentInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)
            && string.IsNullOrWhiteSpace(input.Type)
            && string.IsNullOrWhiteSpace(input.FilePath))
        {
            return null;
        }

        return new DocumentTypeDefaults(
            _options.DocumentDefaultClassId,
            _options.DocumentDefaultClassName,
            _options.DocumentDefaultClassificationSystem,
            _options.DocumentDefaultStatusValue,
            _options.DocumentDefaultRole,
            _options.DocumentDefaultOrganizationName,
            _options.DocumentDefaultOrganizationOfficialName);
    }
}

internal sealed record DocumentTypeDefaults(
    string DocumentClassId,
    string DocumentClassName,
    string DocumentClassificationSystem,
    string StatusValue,
    string Role,
    string OrganizationName,
    string OrganizationOfficialName
);

internal sealed class DocumentInput
{
    public DocumentInput(int index)
    {
        Index = index;
    }

    public int Index { get; }
    public string? CollectionIdShort { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? FilePath { get; set; }
    public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);
}
