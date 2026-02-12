using System;
using System.Xml.Linq;
using AasExcelToXml.Core.IdGeneration;

namespace AasExcelToXml.Core;

// [역할] AasEnvironmentSpec를 AAS 3.0 XML 문서로 직렬화한다.
// [입력] 그룹핑 완료된 스펙, 변환 옵션, 진단 객체, 문서 ID 생성기.
// [출력] AAS 3.0 스키마 구조를 따르는 XDocument.
// [수정 포인트] Relationship/Entity/Collection 출력 구조를 바꾸려면 BuildElement 계열을 수정한다.
public sealed class AasV3XmlWriter
{
    private const string DefaultAasNamespace = "https://admin-shell.io/aas/3/0";
    private readonly XNamespace _aasNs;
    private readonly XNamespace _xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
    private readonly ConvertOptions _options;
    private readonly SpecDiagnostics _diagnostics;
    private readonly Aas3Profile _profile;
    private readonly DocumentationProfile _documentationProfile;
    private readonly DocumentationSkeletonBuilderV3 _documentationBuilder;
    private readonly Aas3ElementOrderer _orderer;
    private readonly IIdProvider _idProvider;
    private readonly SubmodelSkeletonProfile? _submodelSkeletonProfile;

    public AasV3XmlWriter(ConvertOptions options, SpecDiagnostics diagnostics, DocumentIdGenerator documentIdGenerator)
    {
        _options = options;
        _diagnostics = diagnostics;
        _aasNs = options.NamespaceOverride ?? DefaultAasNamespace;
        _profile = Aas3ProfileLoader.Load(options, diagnostics);
        _documentationProfile = DocumentationProfileLoader.LoadV3(options, diagnostics);
        _documentationBuilder = new DocumentationSkeletonBuilderV3(_aasNs, options, diagnostics, _documentationProfile, _profile, documentIdGenerator);
        _orderer = new Aas3ElementOrderer(_aasNs, _profile);
        _idProvider = IdProviderFactory.Create(options);
        _submodelSkeletonProfile = SubmodelSkeletonLoader.Load(diagnostics);
    }

    /// <summary>
    /// 중간 스펙을 AAS 3.0 환경 XML로 변환한다.
    /// </summary>
    /// <param name="spec">SpecGrouper 결과 스펙.</param>
    /// <returns>저장 가능한 AAS 3.0 XML 문서.</returns>
    /// <remarks>
    /// Relationship의 idShort는 SpecGrouper에서 확정된 값을 그대로 사용한다.
    /// 따라서 이름 합성 규칙을 바꾸려면 Writer가 아니라 SpecGrouper를 수정해야 한다.
    /// </remarks>
    public XDocument Write(AasEnvironmentSpec spec)
    {
        var root = new XElement(_aasNs + "environment",
            // AASX Package Explorer는 prefix가 있는 AAS3 환경을 제대로 읽지 못하므로 기본 네임스페이스로 출력한다.
            new XAttribute(XNamespace.Xmlns + "xsi", _xsiNs)
        );

        var shellsElement = new XElement(_aasNs + "assetAdministrationShells");
        var submodelsElement = new XElement(_aasNs + "submodels");
        var conceptDescriptionsElement = new XElement(_aasNs + "conceptDescriptions");

        var assetIdMap = spec.Assets.ToDictionary(a => a.IdShort, a => _idProvider.GetAssetId(a.IdShort), StringComparer.Ordinal);
        var submodelIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var aas in spec.Assets)
        {
            foreach (var submodel in aas.Submodels)
            {
                var key = $"{aas.IdShort}::{submodel.IdShort}";
                submodelIdMap[key] = _idProvider.GetSubmodelId(aas.IdShort, submodel.IdShort);
            }
        }

        foreach (var aas in spec.Assets)
        {
            var aasId = _idProvider.GetShellId(aas.IdShort);
            var assetId = assetIdMap[aas.IdShort];

            var assetInfo = _orderer.BuildElement("assetInformation", new[]
            {
                new Aas3ChildElement("assetKind", new XElement(_aasNs + "assetKind", "Instance")),
                new Aas3ChildElement("globalAssetId", new XElement(_aasNs + "globalAssetId", assetId))
            });

            var shellChildren = new List<Aas3ChildElement>
            {
                new("idShort", new XElement(_aasNs + "idShort", aas.IdShort)),
                new("id", CreateIdElement(aasId)),
                new("description", CreateDescription(aas.Name)),
                new("assetInformation", assetInfo),
                new("submodels", BuildShellSubmodels(aas.IdShort, aas.Submodels, submodelIdMap))
            };

            shellsElement.Add(_orderer.BuildElement("assetAdministrationShell", shellChildren));

            foreach (var submodel in aas.Submodels)
            {
                var submodelId = submodelIdMap[$"{aas.IdShort}::{submodel.IdShort}"];
                SubmodelSkeleton? skeleton = null;
                if (_submodelSkeletonProfile?.TryGet(submodel.IdShort, out var resolvedSkeleton) == true)
                {
                    skeleton = resolvedSkeleton;
                }
                var submodelElements = string.Equals(submodel.IdShort, "Documentation", StringComparison.Ordinal)
                    ? _documentationBuilder.Build(submodel, aas.IdShort)
                    : BuildSubmodelElements(submodel.IdShort, submodelId, submodel.Elements, skeleton);

                var kindElement = string.IsNullOrWhiteSpace(skeleton?.Kind)
                    ? null
                    : new XElement(_aasNs + "kind", skeleton!.Kind);

                var submodelChildren = new List<Aas3ChildElement>
                {
                    new("idShort", new XElement(_aasNs + "idShort", submodel.IdShort)),
                    new("category", CreateCategoryElement(ResolveSubmodelCategory(submodel, skeleton))),
                    new("id", CreateIdElement(submodelId)),
                    new("description", CreateDescription(submodel.Name)),
                    new("kind", kindElement),
                    new("semanticId", CreateSemanticId(skeleton?.SemanticId)),
                    new("qualifiers", null),
                    new("submodelElements", new XElement(_aasNs + "submodelElements", submodelElements))
                };

                submodelsElement.Add(_orderer.BuildElement("submodel", submodelChildren));
            }
        }

        foreach (var conceptDescription in spec.ConceptDescriptions ?? Enumerable.Empty<ConceptDescriptionSpec>())
        {
            conceptDescriptionsElement.Add(BuildConceptDescription(conceptDescription));
        }

        var environmentChildren = new List<Aas3ChildElement>
        {
            new("assetAdministrationShells", shellsElement),
            new("submodels", submodelsElement),
            new("conceptDescriptions", conceptDescriptionsElement)
        };

        root.Add(_orderer.OrderChildren("environment", environmentChildren));

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root
        );

        AasV3XmlValidator.Validate(document, _profile, _diagnostics);
        return document;
    }

    private IEnumerable<XElement> BuildSubmodelElements(string submodelIdShort, string submodelId, List<ElementSpec> elements, SubmodelSkeleton? skeleton)
    {
        var filteredElements = elements.Where(e => e.Kind != ElementKind.DocumentationInput).ToList();
        var elementIdShorts = new HashSet<string>(filteredElements.Select(e => e.IdShort), StringComparer.Ordinal);
        var directElements = filteredElements.Where(e => string.IsNullOrWhiteSpace(e.Collection)).ToList();
        var topNodes = BuildCollectionTree(filteredElements.Where(e => !string.IsNullOrWhiteSpace(e.Collection)));

        if (string.Equals(submodelIdShort, "Assembly_information", StringComparison.Ordinal))
        {
            foreach (var priority in new[] { "PlanFiles", "PlanEntities" })
            {
                var node = topNodes.FirstOrDefault(candidate => string.Equals(candidate.IdShort, priority, StringComparison.Ordinal));
                if (node is null)
                {
                    yield return BuildSubmodelCollection(priority, priority, submodelId, elementIdShorts, Array.Empty<ElementSpec>(), skeleton);
                    continue;
                }

                yield return BuildSubmodelCollection(node.IdShort, node.PathKey, submodelId, elementIdShorts, node.Elements, skeleton, node.Children.Values);
                topNodes.Remove(node);
            }
        }

        foreach (var element in directElements)
        {
            yield return BuildElement(submodelId, element, elementIdShorts);
        }

        foreach (var node in topNodes)
        {
            yield return BuildSubmodelCollection(node.IdShort, node.PathKey, submodelId, elementIdShorts, node.Elements, skeleton, node.Children.Values);
        }
    }

    private List<CollectionNode> BuildCollectionTree(IEnumerable<ElementSpec> elements)
    {
        var topNodes = new List<CollectionNode>();
        foreach (var element in elements)
        {
            var segments = CollectionPathParser.ParseCanonical(element.Collection);
            if (segments.Count == 0)
            {
                continue;
            }

            var currentPath = new List<string>();
            CollectionNode? parent = null;
            foreach (var segment in segments)
            {
                currentPath.Add(segment);
                var pathKey = string.Join("/", currentPath);
                CollectionNode node;
                if (parent is null)
                {
                    node = topNodes.FirstOrDefault(n => string.Equals(n.IdShort, segment, StringComparison.Ordinal)) ?? new CollectionNode(segment, pathKey);
                    if (!topNodes.Contains(node)) topNodes.Add(node);
                }
                else
                {
                    if (!parent.Children.TryGetValue(segment, out node!))
                    {
                        node = new CollectionNode(segment, pathKey);
                        parent.Children[segment] = node;
                    }
                }

                parent = node;
            }

            parent!.Elements.Add(element);
        }

        return topNodes;
    }

    private XElement BuildSubmodelCollection(
        string collectionIdShort,
        string pathKey,
        string submodelId,
        HashSet<string> elementIdShorts,
        IEnumerable<ElementSpec> elements,
        SubmodelSkeleton? skeleton,
        IEnumerable<CollectionNode>? children = null)
    {
        var elementList = elements.Select(e => BuildElement(submodelId, e, elementIdShorts)).ToList();
        var childNodes = children?.ToList() ?? new List<CollectionNode>();
        foreach (var child in childNodes)
        {
            elementList.Add(BuildSubmodelCollection(child.IdShort, child.PathKey, submodelId, elementIdShorts, child.Elements, skeleton, child.Children.Values));
        }

        var collectionSkeleton = ResolveCollectionSkeleton(skeleton, pathKey, collectionIdShort);
        if (elementList.Count == 0 && collectionSkeleton?.Placeholders.Count > 0)
        {
            elementList.AddRange(collectionSkeleton.Placeholders.Select(BuildPlaceholderElement));
        }

        var valueElement = elementList.Count > 0
            ? new XElement(_aasNs + "value", elementList)
            : null;

        var descriptionElement = collectionSkeleton?.IncludeDescription == false ? null : CreateDescription(collectionIdShort);
        var childrenElements = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", collectionIdShort)),
            new("category", CreateCategoryElement(ResolveCollectionCategory(elements, collectionSkeleton))),
            new("description", descriptionElement),
            new("semanticId", CreateSemanticId(collectionSkeleton?.SemanticId)),
            new("qualifiers", null),
            new("value", valueElement)
        };

        return _orderer.BuildElement("submodelElementCollection", childrenElements);
    }

    private static SubmodelSkeletonCollection? ResolveCollectionSkeleton(SubmodelSkeleton? skeleton, string pathKey, string topLevel)
    {
        if (skeleton is null)
        {
            return null;
        }

        if (skeleton.Collections.TryGetValue(pathKey, out var byPath))
        {
            return byPath;
        }

        return skeleton.Collections.TryGetValue(topLevel, out var byTop) ? byTop : null;
    }

    private sealed class CollectionNode
    {
        public CollectionNode(string idShort, string pathKey)
        {
            IdShort = idShort;
            PathKey = pathKey;
        }

        public string IdShort { get; }
        public string PathKey { get; }
        public Dictionary<string, CollectionNode> Children { get; } = new(StringComparer.Ordinal);
        public List<ElementSpec> Elements { get; } = new();
    }

    private XElement? CreateSemanticId(SubmodelSkeletonReference? reference)
    {
        if (reference is null || reference.Keys.Count == 0)
        {
            return null;
        }

        var referenceSpec = new Aas3ReferenceSpec(
            string.IsNullOrWhiteSpace(reference.Type) ? "ExternalReference" : reference.Type,
            reference.Keys.Select(key => new Aas3ReferenceKey(key.Type, key.Value, key.Local, key.IdType)).ToList());

        return _profile.Reference.SemanticIdWrapsReference
            ? new XElement(_aasNs + "semanticId", BuildReference(referenceSpec))
            : new XElement(_aasNs + "semanticId", BuildReferenceContent(referenceSpec));
    }

    private static string? ResolveSubmodelCategory(SubmodelSpec submodel, SubmodelSkeleton? skeleton)
    {
        return !string.IsNullOrWhiteSpace(submodel.Category)
            ? submodel.Category
            : skeleton?.Category;
    }

    private static string? ResolveCollectionCategory(IEnumerable<ElementSpec> elements, SubmodelSkeletonCollection? skeleton)
    {
        if (!string.IsNullOrWhiteSpace(skeleton?.Category))
        {
            return skeleton.Category;
        }

        return elements.Select(e => e.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    private XElement BuildPlaceholderElement(SubmodelSkeletonPlaceholder placeholder)
    {
        var kind = placeholder.Kind?.Trim().ToLowerInvariant();
        return kind switch
        {
            "file" => BuildPlaceholderFile(placeholder),
            "property" => BuildPlaceholderProperty(placeholder),
            _ => BuildPlaceholderFile(placeholder)
        };
    }

    private XElement BuildPlaceholderFile(SubmodelSkeletonPlaceholder placeholder)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", placeholder.IdShort)),
            new("category", string.IsNullOrWhiteSpace(placeholder.Category) ? null : new XElement(_aasNs + "category", placeholder.Category)),
            new("description", null),
            new("semanticId", CreateSemanticId(placeholder.SemanticId)),
            new("qualifiers", null),
            new("value", new XElement(_aasNs + "value", placeholder.Value ?? string.Empty)),
            new("contentType", new XElement(_aasNs + "contentType", placeholder.ContentType ?? string.Empty))
        };

        return _orderer.BuildElement("file", children);
    }

    private XElement BuildPlaceholderProperty(SubmodelSkeletonPlaceholder placeholder)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", placeholder.IdShort)),
            new("category", string.IsNullOrWhiteSpace(placeholder.Category) ? null : new XElement(_aasNs + "category", placeholder.Category)),
            new("description", null),
            new("semanticId", CreateSemanticId(placeholder.SemanticId)),
            new("qualifiers", null),
            new("valueType", new XElement(_aasNs + "valueType", "xs:string")),
            new("value", new XElement(_aasNs + "value", placeholder.Value ?? string.Empty))
        };

        return _orderer.BuildElement("property", children);
    }

    private XElement BuildElement(string submodelId, ElementSpec element, HashSet<string> elementIdShorts)
    {
        return element.Kind switch
        {
            ElementKind.Property => BuildProperty(element),
            ElementKind.Entity => BuildEntity(element),
            ElementKind.Relationship => BuildRelationship(submodelId, element, elementIdShorts),
            ElementKind.ReferenceElement => BuildReferenceElement(submodelId, element, elementIdShorts),
            _ => BuildProperty(element)
        };
    }

    private XElement BuildProperty(ElementSpec element)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", element.IdShort)),
            new("category", CreateCategoryElement(element.Category)),
            new("description", CreateDescription(element.DisplayNameKo)),
            new("semanticId", CreateElementSemanticId(element.SemanticId)),
            new("qualifiers", null),
            new("valueType", new XElement(_aasNs + "valueType", ValueTypeMapper.ResolveAas3ValueType(element.ValueType))),
            new("value", new XElement(_aasNs + "value", element.Value))
        };

        return _orderer.BuildElement("property", children);
    }

    private XElement BuildReferenceElement(string submodelId, ElementSpec element, HashSet<string> elementIdShorts)
    {
        var keys = BuildReferenceElementKeys(submodelId, element, elementIdShorts);
        var referenceSpec = new Aas3ReferenceSpec("ModelReference", keys);

        var value = _profile.Reference.ReferenceElementValueWrapsReference
            ? new XElement(_aasNs + "value", BuildReference(referenceSpec))
            : new XElement(_aasNs + "value", BuildReferenceContent(referenceSpec));

        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", element.IdShort)),
            new("category", CreateCategoryElement(element.Category)),
            new("description", CreateDescription(element.DisplayNameKo)),
            new("semanticId", CreateElementSemanticId(element.SemanticId)),
            new("qualifiers", null),
            new("value", value)
        };

        return _orderer.BuildElement("referenceElement", children);
    }

    private List<Aas3ReferenceKey> BuildReferenceElementKeys(string submodelId, ElementSpec element, HashSet<string> elementIdShorts)
    {
        var keyTypes = _profile.ReferenceElementKeyTypes.Count > 0
            ? _profile.ReferenceElementKeyTypes
            : new List<string> { "SubmodelElement" };

        if (element.ResolvedReference is not null)
        {
            var targetSubmodelId = _idProvider.GetSubmodelId(element.ResolvedReference.TargetAasIdShort, element.ResolvedReference.TargetSubmodelIdShort);
            return new List<Aas3ReferenceKey>
            {
                new("Submodel", targetSubmodelId, true, "IRI"),
                new(element.ResolvedReference.KeyType, element.ResolvedReference.TargetElementIdShort, true, "IdShort")
            };
        }

        var keys = new List<Aas3ReferenceKey>();
        foreach (var keyType in keyTypes)
        {
            switch (keyType)
            {
                case "Submodel":
                    keys.Add(new Aas3ReferenceKey("Submodel", submodelId, true, "IRI"));
                    break;
                case "Property":
                    keys.Add(new Aas3ReferenceKey("Property", ResolveReferenceTarget(element.ReferenceTarget ?? element.IdShort, elementIdShorts), true, "IdShort"));
                    break;
                default:
                    keys.Add(new Aas3ReferenceKey(keyType, ResolveReferenceTarget(element.ReferenceTarget ?? element.IdShort, elementIdShorts), true, "IdShort"));
                    break;
            }
        }

        return keys;
    }

    private XElement BuildEntity(ElementSpec element)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", element.IdShort)),
            new("category", CreateCategoryElement(element.Category)),
            new("description", CreateDescription(element.DisplayNameKo)),
            new("semanticId", CreateElementSemanticId(element.SemanticId)),
            new("qualifiers", null),
            new("entityType", new XElement(_aasNs + "entityType", "SelfManagedEntity")),
            new("globalAssetId", element.ReferenceTarget is null ? null : new XElement(_aasNs + "globalAssetId", _idProvider.GetAssetId(element.ReferenceTarget)))
        };

        return _orderer.BuildElement("entity", children);
    }

    private XElement BuildRelationship(string submodelId, ElementSpec element, HashSet<string> elementIdShorts)
    {
        var relationship = element.Relationship;
        var first = relationship?.First ?? string.Empty;
        var second = relationship?.Second ?? string.Empty;

        var firstEntityIdShort = ResolveReferenceTarget(first, elementIdShorts);
        var secondEntityIdShort = ResolveReferenceTarget(second, elementIdShorts);
        var firstReference = new Aas3ReferenceSpec("ModelReference", new List<Aas3ReferenceKey>
        {
            new("Submodel", submodelId, true, "IRI"),
            new("Entity", firstEntityIdShort, true, "IdShort")
        });
        var secondReference = new Aas3ReferenceSpec("ModelReference", new List<Aas3ReferenceKey>
        {
            new("Submodel", submodelId, true, "IRI"),
            new("Entity", secondEntityIdShort, true, "IdShort")
        });

        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", element.IdShort)),
            new("category", CreateCategoryElement(element.Category)),
            new("description", CreateDescription(element.DisplayNameKo)),
            new("semanticId", CreateElementSemanticId(element.SemanticId)),
            new("qualifiers", null),
            new("first", new XElement(_aasNs + "first", BuildReferenceContent(firstReference))),
            new("second", new XElement(_aasNs + "second", BuildReferenceContent(secondReference)))
        };

        return _orderer.BuildElement("relationshipElement", children);
    }


    private XElement BuildConceptDescription(ConceptDescriptionSpec conceptDescription)
    {
        var children = new List<Aas3ChildElement>
        {
            new("idShort", new XElement(_aasNs + "idShort", conceptDescription.IdShort)),
            new("category", CreateCategoryElement(conceptDescription.Category)),
            new("description", CreateConceptDescriptionDescription(conceptDescription.Description)),
            new("id", new XElement(_aasNs + "id", conceptDescription.Id)),
            new("isCaseOf", CreateConceptDescriptionIsCaseOf(conceptDescription.IsCaseOf))
        };

        return _orderer.BuildElement("conceptDescription", children);
    }

    private XElement? CreateElementSemanticId(string? semanticId)
    {
        if (string.IsNullOrWhiteSpace(semanticId))
        {
            return null;
        }

        var referenceSpec = new Aas3ReferenceSpec("ExternalReference", new List<Aas3ReferenceKey>
        {
            new("GlobalReference", semanticId, false, "IRI")
        });

        return _profile.Reference.SemanticIdWrapsReference
            ? new XElement(_aasNs + "semanticId", BuildReference(referenceSpec))
            : new XElement(_aasNs + "semanticId", BuildReferenceContent(referenceSpec));
    }

    private XElement? CreateConceptDescriptionDescription(List<LangStringSpec> description)
    {
        if (description.Count == 0)
        {
            return null;
        }

        var validItems = description
            .Where(item => !string.IsNullOrWhiteSpace(item.Language))
            .ToList();

        if (validItems.Count == 0)
        {
            return null;
        }

        var element = new XElement(_aasNs + "description");
        foreach (var item in validItems)
        {
            element.Add(new XElement(_aasNs + "langStringTextType",
                new XElement(_aasNs + "language", item.Language),
                new XElement(_aasNs + "text", item.Text ?? string.Empty)));
        }

        return element;
    }

    private XElement? CreateConceptDescriptionIsCaseOf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var referenceSpec = new Aas3ReferenceSpec("ExternalReference", new List<Aas3ReferenceKey>
        {
            new("GlobalReference", value, false, "IRI")
        });

        return new XElement(_aasNs + "isCaseOf", BuildReference(referenceSpec));
    }

    private XElement? CreateCategoryElement(string? category)
    {
        var resolved = string.IsNullOrWhiteSpace(category)
            ? (_options.FillMissingCategoryWithConstant ? _options.MissingCategoryConstant : null)
            : category;

        return string.IsNullOrWhiteSpace(resolved)
            ? null
            : new XElement(_aasNs + "category", resolved);
    }

    private XElement CreateIdElement(string id)
    {
        return new XElement(_aasNs + "id", id);
    }

    private XElement CreateDescription(string name)
    {
        if (string.Equals(_profile.Description.Mode, "LangString", StringComparison.OrdinalIgnoreCase))
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

        var langStringTextType = new XElement(_aasNs + "langStringTextType",
            new XElement(_aasNs + "language", "en"),
            new XElement(_aasNs + "text", string.Empty));

        var descriptionElement = new XElement(_aasNs + "description", langStringTextType);
        if (_options.IncludeKoreanDescription)
        {
            descriptionElement.Add(new XElement(_aasNs + "langStringTextType",
                new XElement(_aasNs + "language", "ko"),
                new XElement(_aasNs + "text", name)));
        }

        return descriptionElement;
    }

    private XElement BuildShellSubmodels(string aasIdShort, IEnumerable<SubmodelSpec> submodels, Dictionary<string, string> submodelIdMap)
    {
        var element = new XElement(_aasNs + "submodels");

        foreach (var submodel in submodels)
        {
            var submodelId = submodelIdMap[$"{aasIdShort}::{submodel.IdShort}"];
            var referenceSpec = new Aas3ReferenceSpec("ModelReference", new List<Aas3ReferenceKey>
            {
                new("Submodel", submodelId, true, "IRI")
            });

            element.Add(_profile.Reference.SubmodelReferenceWrapsReference
                ? BuildReference(referenceSpec)
                : BuildReferenceContent(referenceSpec));
        }

        return element;
    }

    private XElement BuildReference(Aas3ReferenceSpec spec)
    {
        var reference = new XElement(_aasNs + "reference");

        if (_profile.Reference.ReferenceTypeMode == Aas3ReferenceTypeMode.Attribute)
        {
            reference.SetAttributeValue("type", spec.Type);
        }
        else if (_profile.Reference.ReferenceTypeMode == Aas3ReferenceTypeMode.Element)
        {
            reference.Add(new XElement(_aasNs + "type", spec.Type));
        }

        reference.Add(new XElement(_aasNs + "keys", spec.Keys.Select(CreateKey)));

        var order = _profile.ElementOrders.TryGetValue("reference", out var elementOrder) && elementOrder.Count > 0
            ? elementOrder
            : _profile.Reference.ReferenceChildOrder;
        if (order.Count > 0)
        {
            var map = new List<Aas3ChildElement>
            {
                new("type", reference.Elements().FirstOrDefault(e => e.Name.LocalName == "type")),
                new("keys", reference.Elements().FirstOrDefault(e => e.Name.LocalName == "keys"))
            };

            var ordered = order.SelectMany(name => map.Where(child => child.Name == name && child.Element is not null))
                .Select(child => child.Element!);
            reference.ReplaceNodes(ordered);
        }

        return reference;
    }

    private IEnumerable<XElement> BuildReferenceContent(Aas3ReferenceSpec spec)
    {
        if (_profile.Reference.ReferenceTypeMode == Aas3ReferenceTypeMode.Element
            || _profile.Reference.ReferenceTypeMode == Aas3ReferenceTypeMode.Attribute)
        {
            yield return new XElement(_aasNs + "type", spec.Type);
        }

        yield return new XElement(_aasNs + "keys", spec.Keys.Select(CreateKey));
    }

    private XElement CreateKey(Aas3ReferenceKey key)
    {
        var element = new XElement(_aasNs + "key");

        if (string.Equals(_profile.Reference.Key.Mode, "Attribute", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in _profile.Reference.Key.AttributeNames)
            {
                element.SetAttributeValue(name, ResolveKeyAttributeValue(name, key));
            }

            return element;
        }

        var childNames = _profile.Reference.Key.ChildElementNames.Count > 0
            ? _profile.Reference.Key.ChildElementNames
            : new List<string> { "type", "value" };
        foreach (var name in childNames)
        {
            element.Add(new XElement(_aasNs + name, ResolveKeyElementValue(name, key)));
        }

        return element;
    }

    private static string ResolveKeyAttributeValue(string name, Aas3ReferenceKey key)
    {
        return name switch
        {
            "type" => key.Type,
            "local" => key.Local ? "true" : "false",
            "idType" => key.IdType ?? string.Empty,
            "value" => key.Value,
            _ => string.Empty
        };
    }

    private static string ResolveKeyElementValue(string name, Aas3ReferenceKey key)
    {
        return name switch
        {
            "type" => key.Type,
            "local" => key.Local ? "true" : "false",
            "idType" => key.IdType ?? string.Empty,
            "value" => key.Value,
            _ => string.Empty
        };
    }

    private string ResolveReferenceTarget(string value, HashSet<string> elementIdShorts)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return elementIdShorts.Contains(value) ? value : value;
    }


}

internal sealed record Aas3ReferenceSpec(string Type, List<Aas3ReferenceKey> Keys);

internal sealed record Aas3ReferenceKey(string Type, string Value, bool Local, string? IdType);
