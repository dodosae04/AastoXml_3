using System.IO;
using System.Xml.Linq;

namespace AasExcelToXml.Core;

public sealed class DocumentationSkeletonBuilderV3
{
    private readonly XNamespace _aasNs;
    private readonly ConvertOptions _options;
    private readonly SpecDiagnostics _diagnostics;
    private readonly DocumentationProfile _profile;
    private readonly Aas3Profile _aas3Profile;
    private readonly Aas3ElementOrderer _orderer;
    private readonly DocumentIdGenerator _documentIdGenerator;

    public DocumentationSkeletonBuilderV3(
        XNamespace aasNs,
        ConvertOptions options,
        SpecDiagnostics diagnostics,
        DocumentationProfile profile,
        Aas3Profile aas3Profile,
        DocumentIdGenerator documentIdGenerator)
    {
        _aasNs = aasNs;
        _options = options;
        _diagnostics = diagnostics;
        _profile = profile;
        _aas3Profile = aas3Profile;
        _orderer = new Aas3ElementOrderer(aasNs, aas3Profile);
        _documentIdGenerator = documentIdGenerator;
    }

    public IEnumerable<XElement> Build(SubmodelSpec submodel, string aasIdShort)
    {
        // AAS3 Documentation은 정답 스켈레톤 프로파일 구조를 그대로 유지하고, 엑셀 값만 치환한다.
        var isAirbalanceAas = DocumentationIdShortMapper.IsAirbalanceAas(aasIdShort);
        var documentInputs = ExtractDocumentInputs(submodel.Elements);
        if (documentInputs.Count == 0)
        {
            documentInputs.Add(new DocumentInput(1));
        }

        var overrides = DocumentationOverrideLoader.Load(_options, _diagnostics);
        var primaryInput = SelectPrimaryDocumentInput(documentInputs, overrides);
        if (!_options.IncludeAllDocumentation && documentInputs.Count > 1)
        {
            documentInputs = new List<DocumentInput> { primaryInput };
        }

        var orderedInputs = documentInputs
            .Select((input, index) => new
            {
                Input = input,
                IdShort = DocumentationIdShortMapper.ResolveDocumentCollectionIdShort(
                    input.CollectionIdShort,
                    index + 1,
                    aasIdShort,
                    _profile.DocumentCollectionIdShortPattern)
            })
            
            .ThenBy(item => item.Input.Index)
            .ToList();

        var list = new List<XElement>();
        foreach (var item in orderedInputs)
        {
            var element = isAirbalanceAas
                ? BuildAirbalanceDocumentCollection(item.IdShort, item.Input, overrides, primaryInput)
                : BuildDocumentSpecCollection(item.IdShort, item.Input, overrides, primaryInput);
            list.Add(element);
        }

        return list;
    }

    private XElement BuildTemplateCollection(string idShort, List<DocumentationElementTemplate> templates, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var category = _profile.DocumentCollectionCategory ?? "PARAMETER";

        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", string.IsNullOrWhiteSpace(category) ? null : new XElement(_aasNs + "category", category)),
            new("description", CreateDescription(idShort)),
            new("semanticId", CreateSemanticId(_profile.DocumentCollectionSemanticId)),
            new("qualifier", _profile.DocumentCollectionHasQualifier ? CreateQualifier() : null),
            new("value", new XElement(_aasNs + "value", templates.Select(template => BuildTemplateElement(template, input, fieldMap))))
        };

        return _orderer.BuildElement("submodelElementCollection", children);
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
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", template.IdShort)),
            new("category", string.IsNullOrWhiteSpace(template.Category) ? null : new XElement(_aasNs + "category", template.Category)),
            new("description", CreateDescription(template.IdShort)),
            new("semanticId", CreateSemanticId(template.SemanticId)),
            new("qualifier", template.HasQualifier ? CreateQualifier() : null),
            new("valueType", CreateValueTypeElement(template)),
            new("value", new XElement(_aasNs + "value", value ?? template.DefaultValue ?? string.Empty)),
            new("valueId", template.ValueId is null ? null : CreateValueId(template.ValueId))
        };

        return _orderer.BuildElement("property", children);
    }

    private XElement BuildFile(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var resolvedPath = fieldMap.ResolveFilePath(input, _profile.FilePattern?.BasePath ?? "/aasx/files/");
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", template.IdShort)),
            new("category", string.IsNullOrWhiteSpace(template.Category) ? null : new XElement(_aasNs + "category", template.Category)),
            new("description", CreateDescription(template.IdShort)),
            new("semanticId", CreateSemanticId(template.SemanticId)),
            new("qualifier", template.HasQualifier ? CreateQualifier() : null),
            new("value", new XElement(_aasNs + "value", resolvedPath ?? template.DefaultValue ?? string.Empty)),
            new("contentType", string.IsNullOrWhiteSpace(template.MimeType) ? null : new XElement(_aasNs + "contentType", template.MimeType))
        };

        return _orderer.BuildElement("file", children);
    }

    private XElement BuildCollection(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", template.IdShort)),
            new("category", string.IsNullOrWhiteSpace(template.Category) ? null : new XElement(_aasNs + "category", template.Category)),
            new("description", CreateDescription(template.IdShort)),
            new("semanticId", CreateSemanticId(template.SemanticId)),
            new("qualifier", template.HasQualifier ? CreateQualifier() : null),
            new("value", new XElement(_aasNs + "value", template.Children.Select(child => BuildTemplateElement(child, input, fieldMap))))
        };

        return _orderer.BuildElement("submodelElementCollection", children);
    }

    private XElement BuildMultiLanguageProperty(DocumentationElementTemplate template, DocumentInput input, DocumentationFieldMap fieldMap)
    {
        var resolved = fieldMap.ResolveMultiLanguageValue(template.IdShort, input);
        var langStrings = template.LangStrings.Count > 0
            ? template.LangStrings
            : new List<DocumentationLangString> { new("EN", string.Empty) };

        var valueElement = new XElement(_aasNs + "value",
            langStrings.Select(lang => CreateLangStringElement(lang.Lang, resolved ?? lang.Value ?? string.Empty)));

        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", template.IdShort)),
            new("category", string.IsNullOrWhiteSpace(template.Category) ? null : new XElement(_aasNs + "category", template.Category)),
            new("description", CreateDescription(template.IdShort)),
            new("semanticId", CreateSemanticId(template.SemanticId)),
            new("qualifier", template.HasQualifier ? CreateQualifier() : null),
            new("value", valueElement)
        };

        return _orderer.BuildElement("multiLanguageProperty", children);
    }

    private XElement CreateDescription(string name)
    {
        if (string.Equals(_aas3Profile.Description.Mode, "LangString", StringComparison.OrdinalIgnoreCase))
        {
            var description = new XElement(_aasNs + "description",
                new XElement(_aasNs + "langString",
                    new XAttribute("lang", "en"),
                    string.Empty));

            if (_options.IncludeKoreanDescription)
            {
                description.Add(new XElement(_aasNs + "langString",
                    new XAttribute("lang", "ko"),
                    name));
            }

            return description;
        }

        var descriptionElement = new XElement(_aasNs + "description",
            new XElement(_aasNs + "langStringTextType",
                new XElement(_aasNs + "language", "en"),
                new XElement(_aasNs + "text", string.Empty)));

        if (_options.IncludeKoreanDescription)
        {
            descriptionElement.Add(new XElement(_aasNs + "langStringTextType",
                new XElement(_aasNs + "language", "ko"),
                new XElement(_aasNs + "text", name)));
        }

        return descriptionElement;
    }

    private XElement? CreateSemanticId(DocumentationReference? reference)
    {
        if (reference is null || reference.Keys.Count == 0)
        {
            return null;
        }

        var referenceElement = CreateReference(reference);
        return _aas3Profile.Reference.SemanticIdWrapsReference
            ? new XElement(_aasNs + "semanticId", referenceElement)
            : new XElement(_aasNs + "semanticId", CreateReferenceContent(reference));
    }

    private XElement CreateValueId(DocumentationReference reference)
    {
        var referenceElement = CreateReference(reference);
        return _aas3Profile.Reference.SemanticIdWrapsReference
            ? new XElement(_aasNs + "valueId", referenceElement)
            : new XElement(_aasNs + "valueId", CreateReferenceContent(reference));
    }

    private XElement CreateReference(DocumentationReference reference)
    {
        var referenceElement = new XElement(_aasNs + "reference");
        var referenceType = string.IsNullOrWhiteSpace(reference.Type) ? "ModelReference" : reference.Type;
        if (_aas3Profile.Reference.ReferenceTypeMode == Aas3ReferenceTypeMode.Attribute)
        {
            referenceElement.SetAttributeValue("type", referenceType);
        }
        else if (_aas3Profile.Reference.ReferenceTypeMode == Aas3ReferenceTypeMode.Element)
        {
            referenceElement.Add(new XElement(_aasNs + "type", referenceType));
        }

        referenceElement.Add(new XElement(_aasNs + "keys", reference.Keys.Select(CreateKey)));
        return referenceElement;
    }

    private IEnumerable<XElement> CreateReferenceContent(DocumentationReference reference)
    {
        var referenceType = string.IsNullOrWhiteSpace(reference.Type) ? "ModelReference" : reference.Type;
        if (_aas3Profile.Reference.ReferenceTypeMode != Aas3ReferenceTypeMode.None)
        {
            yield return new XElement(_aasNs + "type", referenceType);
        }

        yield return new XElement(_aasNs + "keys", reference.Keys.Select(CreateKey));
    }

    private XElement CreateKey(DocumentationKey key)
    {
        var keyElement = new XElement(_aasNs + "key");
        if (string.Equals(_aas3Profile.Reference.Key.Mode, "Attribute", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in _aas3Profile.Reference.Key.AttributeNames)
            {
                keyElement.SetAttributeValue(name, ResolveKeyAttributeValue(name, key));
            }
        }
        else
        {
            var childNames = _aas3Profile.Reference.Key.ChildElementNames.Count > 0
                ? _aas3Profile.Reference.Key.ChildElementNames
                : new List<string> { "type", "value" };
            foreach (var name in childNames)
            {
                keyElement.Add(new XElement(_aasNs + name, ResolveKeyElementValue(name, key)));
            }
        }

        return keyElement;
    }

    private static string ResolveKeyAttributeValue(string name, DocumentationKey key)
    {
        return name switch
        {
            "type" => key.Type,
            "local" => key.Local ? "true" : "false",
            "idType" => key.IdType,
            "value" => key.Value,
            _ => string.Empty
        };
    }

    private static string ResolveKeyElementValue(string name, DocumentationKey key)
    {
        return name switch
        {
            "type" => key.Type,
            "local" => key.Local ? "true" : "false",
            "idType" => key.IdType,
            "value" => key.Value,
            _ => string.Empty
        };
    }

    private XElement? CreateQualifier()
    {
        return null;
    }

    private XElement? CreateValueTypeElement(DocumentationElementTemplate template)
    {
        var rawValueType = template.ValueType;
        if (template.UseEmptyValueType && string.IsNullOrWhiteSpace(rawValueType))
        {
            var fallback = ValueTypeMapper.ResolveAas3ValueType(string.Empty);
            return new XElement(_aasNs + "valueType", fallback);
        }

        var resolved = ValueTypeMapper.ResolveAas3ValueType(rawValueType);
        return string.IsNullOrWhiteSpace(resolved) ? null : new XElement(_aasNs + "valueType", resolved);
    }

    private XElement CreateLangStringElement(string lang, string value)
    {
        if (string.Equals(_aas3Profile.MultiLanguageValue.Mode, "LangString", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(value)
                ? new XElement(_aasNs + "langString", new XAttribute("lang", lang))
                : new XElement(_aasNs + "langString", new XAttribute("lang", lang), value);
        }

        return new XElement(_aasNs + "langStringTextType",
            new XElement(_aasNs + "language", lang),
            new XElement(_aasNs + "text", value));
    }

    private XElement? CreateBooleanElement(string name, bool value)
    {
        var elementName = _aasNs + name;
        var order = _aas3Profile.ElementOrders.TryGetValue("submodelElementCollection", out var orderList) ? orderList : null;
        if (order is not null && !order.Contains(name))
        {
            return null;
        }

        return new XElement(elementName, value ? "true" : "false");
    }

    private List<DocumentInput> ExtractDocumentInputs(List<ElementSpec> elements)
    {
        var groups = elements
            .Where(IsDocumentationInputElement)
            .Where(e => !string.IsNullOrWhiteSpace(e.Collection)).GroupBy(e => e.Collection)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

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

        return inputs.OrderBy(input => input.Index).First();
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

    private XElement BuildAirbalanceDocumentCollection(string idShort, DocumentInput input, DocumentationOverrideProfile? overrides, DocumentInput primaryInput)
    {
        var overrideRule = overrides?.Resolve(input);
        var documentId = _documentIdGenerator.NextId();
        var isPrimary = ResolveIsPrimaryValue(input, overrides, primaryInput);

        var classId = overrideRule?.DocumentClassId ?? ResolveFieldValue(input, "classid", "documentclassid");
        var className = overrideRule?.DocumentClassName ?? ResolveFieldValue(input, "classname", "documentclassname", "documentclass");
        var classificationSystem = overrideRule?.DocumentClassificationSystem ?? ResolveFieldValue(input, "classificationsystem", "documentclassificationsystem");
        var language = overrideRule?.Language ?? ResolveFieldValue(input, "language");
        var documentVersionId = overrideRule?.DocumentVersionId ?? ResolveFieldValue(input, "documentversionid");
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
        documentVersionId ??= _options.DocumentDefaultVersionId;
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
            BuildVdiProperty("Language01", language, "xs:string", DocumentationSemanticUris.DocumentVersionLanguage),
            BuildVdiProperty("DocumentVersionId", documentVersionId, "xs:string", DocumentationSemanticUris.DocumentVersionId),
            BuildVdiMultiLanguageProperty("Title", title, DocumentationSemanticUris.Title, "EN"),
            BuildVdiMultiLanguageProperty("Summary", summary, DocumentationSemanticUris.Summary, "kr"),
            BuildVdiMultiLanguageProperty("KeyWords", keyWords, DocumentationSemanticUris.KeyWords, "kr"),
            BuildVdiProperty("SetDate", setDate, "xs:string", DocumentationSemanticUris.SetDate),
            BuildVdiProperty("StatusValue", statusValue, "xs:string", DocumentationSemanticUris.StatusValue),
            BuildVdiProperty("Role", role, "xs:string", DocumentationSemanticUris.Role),
            BuildVdiProperty("OrganizationName", organizationName, "xs:string", DocumentationSemanticUris.OrganizationName),
            BuildVdiProperty("OrganizationOfficialName", organizationOfficialName, "xs:string", DocumentationSemanticUris.OrganizationOfficialName),
            BuildVdiFile("DigitalFile", input)
        };

        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", new XElement(_aasNs + "category", "CONSTANT")),
            new("semanticId", CreateVdi2770SemanticId(DocumentationSemanticUris.Document)),
            new("value", new XElement(_aasNs + "value",
                BuildVdiProperty("DocumentId", documentId, "xs:string", DocumentationSemanticUris.DocumentId),
                BuildVdiProperty("IsPrimaryDocumentId", isPrimary, "xs:boolean", DocumentationSemanticUris.IsPrimaryDocumentId),
                BuildVdiProperty("DocumentClassId", classId, "xs:string", DocumentationSemanticUris.DocumentClassId),
                BuildVdiProperty("DocumentClassName", className, "xs:string", DocumentationSemanticUris.DocumentClassName),
                BuildVdiProperty("DocumentClassificationSystem", classificationSystem, "xs:string", DocumentationSemanticUris.DocumentClassificationSystem),
                BuildVdiDocumentVersionCollection("DocumentVersion01", versionChildren)))
        };

        return _orderer.BuildElement("submodelElementCollection", children);
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
        var documentVersionId = overrideRule?.DocumentVersionId ?? ResolveFieldValue(input, "documentversionid");
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
        documentVersionId ??= _options.DocumentDefaultVersionId;
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
            BuildVdiProperty("Language01", language, "xs:string", DocumentationSemanticUris.DocumentVersionLanguage),
            BuildVdiProperty("DocumentVersionId", documentVersionId, "xs:string", DocumentationSemanticUris.DocumentVersionId),
            BuildVdiMultiLanguageProperty("Title", title, DocumentationSemanticUris.Title, "EN"),
            BuildVdiMultiLanguageProperty("Summary", summary, DocumentationSemanticUris.Summary, "kr"),
            BuildVdiMultiLanguageProperty("KeyWords", keyWords, DocumentationSemanticUris.KeyWords, "kr"),
            BuildVdiProperty("SetDate", setDate, "xs:string", DocumentationSemanticUris.SetDate),
            BuildVdiProperty("StatusValue", statusValue, "xs:string", DocumentationSemanticUris.StatusValue),
            BuildVdiProperty("Role", role, "xs:string", DocumentationSemanticUris.Role),
            BuildVdiProperty("OrganizationName", organizationName, "xs:string", DocumentationSemanticUris.OrganizationName),
            BuildVdiProperty("OrganizationOfficialName", organizationOfficialName, "xs:string", DocumentationSemanticUris.OrganizationOfficialName),
            BuildVdiFile("DigitalFile", input)
        };

        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", new XElement(_aasNs + "category", "CONSTANT")),
            new("semanticId", CreateVdi2770SemanticId(DocumentationSemanticUris.Document)),
            new("value", new XElement(_aasNs + "value",
                BuildVdiProperty("DocumentId", documentId, "xs:string", DocumentationSemanticUris.DocumentId),
                BuildVdiProperty("IsPrimaryDocumentId", isPrimary, "xs:boolean", DocumentationSemanticUris.IsPrimaryDocumentId),
                BuildVdiProperty("DocumentClassId", classId, "xs:string", DocumentationSemanticUris.DocumentClassId),
                BuildVdiProperty("DocumentClassName", className, "xs:string", DocumentationSemanticUris.DocumentClassName),
                BuildVdiProperty("DocumentClassificationSystem", classificationSystem, "xs:string", DocumentationSemanticUris.DocumentClassificationSystem),
                BuildVdiDocumentVersionCollection("DocumentVersion01", versionChildren)))
        };

        return _orderer.BuildElement("submodelElementCollection", children);
    }

    private XElement BuildVdiDocumentVersionCollection(string idShort, IEnumerable<XElement> childrenElements)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", new XElement(_aasNs + "category", "CONSTANT")),
            new("semanticId", CreateVdi2770SemanticId(DocumentationSemanticUris.DocumentVersion)),
            new("value", new XElement(_aasNs + "value", childrenElements))
        };

        return _orderer.BuildElement("submodelElementCollection", children);
    }

    private XElement BuildVdiProperty(string idShort, string? value, string valueType, string semanticSuffix)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", new XElement(_aasNs + "category", "CONSTANT")),
            new("semanticId", CreateVdi2770SemanticId(semanticSuffix)),
            new("valueType", new XElement(_aasNs + "valueType", valueType)),
            new("value", new XElement(_aasNs + "value", value ?? string.Empty))
        };

        return _orderer.BuildElement("property", children);
    }

    private XElement BuildVdiFile(string idShort, DocumentInput input)
    {
        var resolvedPath = ResolveDigitalFilePath(input);
        var contentType = ResolveContentType(resolvedPath ?? input.FilePath);
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", new XElement(_aasNs + "category", "CONSTANT")),
            new("semanticId", CreateVdi2770SemanticId(DocumentationSemanticUris.DigitalFile)),
            new("value", new XElement(_aasNs + "value", resolvedPath ?? string.Empty)),
            new("contentType", string.IsNullOrWhiteSpace(contentType) ? null : new XElement(_aasNs + "contentType", contentType))
        };

        return _orderer.BuildElement("file", children);
    }

    private XElement BuildVdiMultiLanguageProperty(string idShort, string? value, string semanticSuffix, string lang)
    {
        var valueElement = new XElement(_aasNs + "value",
            CreateLangStringElement(lang, value ?? string.Empty));
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", idShort)),
            new("category", new XElement(_aasNs + "category", "CONSTANT")),
            new("semanticId", CreateVdi2770SemanticId(semanticSuffix)),
            new("value", valueElement)
        };

        return _orderer.BuildElement("multiLanguageProperty", children);
    }

    private XElement CreateVdi2770SemanticId(string uri)
    {
        var reference = new DocumentationReference
        {
            Type = "ExternalReference",
            Keys = new List<DocumentationKey>
            {
                new DocumentationKey("GlobalReference", false, "IRI", uri)
            }
        };

        return CreateSemanticId(reference) ?? new XElement(_aasNs + "semanticId");
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

    private string? ResolveDigitalFilePath(DocumentInput input)
    {
        var fileName = string.IsNullOrWhiteSpace(input.FilePath)
            ? null
            : Path.GetFileName(input.FilePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            if (string.IsNullOrWhiteSpace(_options.InputFileName))
            {
                return null;
            }

            fileName = $"{_options.InputFileName}.pdf";
        }

        var basePath = _profile.FilePattern?.BasePath ?? "/aasx/files/";
        return $"{basePath}{fileName}";
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
}
