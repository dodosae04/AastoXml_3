using System;
using System.Xml.Linq;
using AasExcelToXml.Core.IdGeneration;

namespace AasExcelToXml.Core;

// [역할] AasEnvironmentSpec를 AAS 2.0 XML 문서로 직렬화한다.
// [입력] 스펙 모델, 옵션, 진단 객체, 문서 ID 생성기.
// [출력] AAS 2.0 네임스페이스 형식의 XDocument.
// [수정 포인트] relationshipElement, entity, reference 출력 태그 구조를 바꾸려면 BuildSubmodelElements/BuildElement 계열을 수정한다.
public sealed class AasV2XmlWriter
{
    // 샘플이 V2 네임스페이스를 쓰는 경우가 많아서 기본 ns를 잡아둠
    private const string DefaultAasNamespace = "http://www.admin-shell.io/aas/2/0";
    private readonly XNamespace _aasNs;
    private readonly XNamespace _iecNs = "http://www.admin-shell.io/IEC61360/2/0";
    private readonly XNamespace _abacNs = "http://www.admin-shell.io/aas/abac/2/0";
    private readonly XNamespace _xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
    private readonly ConvertOptions _options;
    private readonly SpecDiagnostics _diagnostics;
    private readonly IIdProvider _idProvider;
    private readonly DocumentIdGenerator _documentIdGenerator;
    private HashSet<string> _conceptDescriptionIds = new(StringComparer.Ordinal);

    public AasV2XmlWriter(ConvertOptions options, SpecDiagnostics diagnostics, DocumentIdGenerator documentIdGenerator)
    {
        _aasNs = options.NamespaceOverride ?? DefaultAasNamespace;
        _options = options;
        _diagnostics = diagnostics;
        _idProvider = IdProviderFactory.Create(options);
        _documentIdGenerator = documentIdGenerator;
    }

    /// <summary>
    /// 중간 스펙을 AAS 2.0 XML로 직렬화한다.
    /// </summary>
    /// <param name="spec">SpecGrouper에서 생성된 환경 스펙.</param>
    /// <returns>AAS 2.0 표준 구조를 갖는 XML 문서.</returns>
    /// <remarks>
    /// Relationship idShort 값은 SpecGrouper에서 전달된 값을 그대로 사용한다.
    /// 즉, 이름 정책 변경은 Writer가 아닌 SpecGrouper에서 수정해야 회귀를 막을 수 있다.
    /// </remarks>
    public XDocument Write(AasEnvironmentSpec spec)
    {
        _conceptDescriptionIds = (spec.ConceptDescriptions ?? Enumerable.Empty<ConceptDescriptionSpec>())
            .Where(cd => !string.IsNullOrWhiteSpace(cd.Id))
            .Select(cd => cd.Id.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var root = new XElement(_aasNs + "aasenv",
            new XAttribute(XNamespace.Xmlns + "aas", _aasNs),
            new XAttribute(XNamespace.Xmlns + "xsi", _xsiNs),
            new XAttribute(XNamespace.Xmlns + "IEC", _iecNs),
            new XAttribute(XNamespace.Xmlns + "abac", _abacNs),
            new XAttribute(_xsiNs + "schemaLocation", "http://www.admin-shell.io/aas/2/0 AAS.xsd http://www.admin-shell.io/IEC61360/2/0 IEC61360.xsd")
        );

        var shellsElement = new XElement(_aasNs + "assetAdministrationShells");
        var assetsElement = new XElement(_aasNs + "assets");
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

        var referenceIndex = ReferenceIndex.Build(spec, submodelIdMap);
        var documentationProfile = DocumentationProfileLoader.Load(_options, _diagnostics);
        var documentationBuilder = new DocumentationSkeletonBuilderV2(_aasNs, _options, _diagnostics, documentationProfile, _documentIdGenerator);

        foreach (var aas in spec.Assets)
        {
            var aasId = _idProvider.GetShellId(aas.IdShort);
            var assetId = assetIdMap[aas.IdShort];

            shellsElement.Add(
                new XElement(_aasNs + "assetAdministrationShell",
                    new XElement(_aasNs + "idShort", aas.IdShort),
                    new XElement(_aasNs + "category", "CONSTANT"),
                    CreateDescription(aas.Name),
                    CreateIdentification(aasId),
                    new XElement(_aasNs + "conceptDictionaries"),
                    new XElement(_aasNs + "assetRef",
                        new XElement(_aasNs + "keys",
                            CreateKey("Asset", assetId, local: true)
                        )
                    ),
                    new XElement(_aasNs + "submodelRefs",
                        aas.Submodels.Select(sm =>
                            new XElement(_aasNs + "submodelRef",
                                new XElement(_aasNs + "keys",
                                    CreateKey("Submodel", submodelIdMap[$"{aas.IdShort}::{sm.IdShort}"], local: true)
                                )
                            )
                        )
                    )
                )
            );

            assetsElement.Add(
                new XElement(_aasNs + "asset",
                    new XElement(_aasNs + "idShort", aas.IdShort),
                    new XElement(_aasNs + "category", "CONSTANT"),
                    CreateDescription(aas.Name),
                    CreateIdentification(assetId),
                    new XElement(_aasNs + "kind", "Instance"),
                    new XElement(_aasNs + "assetIdentificationModelRef",
                        new XElement(_aasNs + "keys")
                    ),
                    new XElement(_aasNs + "billOfMaterialRef",
                        new XElement(_aasNs + "keys")
                    )
                )
            );

            foreach (var submodel in aas.Submodels)
            {
                var submodelId = submodelIdMap[$"{aas.IdShort}::{submodel.IdShort}"];
                var submodelElements = string.Equals(submodel.IdShort, "Documentation", StringComparison.Ordinal)
                    ? documentationBuilder.Build(submodel, aas.IdShort)
                    : BuildSubmodelElements(aas.IdShort, submodel.IdShort, submodel.Elements, referenceIndex);
                var submodelElement = new XElement(_aasNs + "submodel",
                    new XElement(_aasNs + "idShort", submodel.IdShort),
                    CreateCategoryElement(submodel.Category),
                    CreateDescription(submodel.Name),
                    CreateIdentification(submodelId),
                    new XElement(_aasNs + "kind", "Instance"),
                    CreateSemanticId(),
                    CreateQualifiers(),
                    new XElement(_aasNs + "submodelElements",
                        submodelElements
                    )
                );

                submodelsElement.Add(submodelElement);
            }
        }

        foreach (var conceptDescription in spec.ConceptDescriptions ?? Enumerable.Empty<ConceptDescriptionSpec>())
        {
            conceptDescriptionsElement.Add(BuildConceptDescription(conceptDescription));
        }

        root.Add(shellsElement);
        root.Add(assetsElement);
        root.Add(submodelsElement);
        root.Add(conceptDescriptionsElement);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root
        );
    }

    private IEnumerable<XElement> BuildSubmodelElements(string aasIdShort, string submodelIdShort, List<ElementSpec> elements, ReferenceIndex referenceIndex)
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
                    yield return WrapSubmodelElement(BuildSubmodelCollection(aasIdShort, submodelIdShort, priority, elementIdShorts, referenceIndex, Array.Empty<ElementSpec>(), null));
                    continue;
                }

                yield return WrapSubmodelElement(BuildSubmodelCollection(aasIdShort, submodelIdShort, node.IdShort, elementIdShorts, referenceIndex, node.Elements, node.Children.Values));
                topNodes.Remove(node);
            }
        }

        foreach (var element in directElements)
        {
            yield return WrapSubmodelElement(BuildElement(aasIdShort, submodelIdShort, element, elementIdShorts, referenceIndex));
        }

        foreach (var node in topNodes)
        {
            yield return WrapSubmodelElement(BuildSubmodelCollection(aasIdShort, submodelIdShort, node.IdShort, elementIdShorts, referenceIndex, node.Elements, node.Children.Values));
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
        string aasIdShort,
        string submodelIdShort,
        string collectionIdShort,
        HashSet<string> elementIdShorts,
        ReferenceIndex referenceIndex,
        IEnumerable<ElementSpec> elements,
        IEnumerable<CollectionNode>? children)
    {
        var collectionCategory = elements.Select(e => e.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

        var valueElements = elements
            .Select(e => WrapSubmodelElement(BuildElement(aasIdShort, submodelIdShort, e, elementIdShorts, referenceIndex)))
            .ToList();

        foreach (var child in children ?? Enumerable.Empty<CollectionNode>())
        {
            valueElements.Add(WrapSubmodelElement(BuildSubmodelCollection(aasIdShort, submodelIdShort, child.IdShort, elementIdShorts, referenceIndex, child.Elements, child.Children.Values)));
        }

        return new XElement(_aasNs + "submodelElementCollection",
            new XElement(_aasNs + "idShort", collectionIdShort),
            CreateCategoryElement(collectionCategory),
            new XElement(_aasNs + "kind", "Instance"),
            new XElement(_aasNs + "ordered", "false"),
            new XElement(_aasNs + "allowDuplicates", "false"),
            CreateDescription(collectionIdShort),
            CreateSemanticId(),
            CreateQualifiers(),
            new XElement(_aasNs + "value", valueElements)
        );
    }

    private sealed class CollectionNode
    {
        public CollectionNode(string idShort, string pathKey)
        {
            IdShort = idShort;
        }

        public string IdShort { get; }
        public Dictionary<string, CollectionNode> Children { get; } = new(StringComparer.Ordinal);
        public List<ElementSpec> Elements { get; } = new();
    }

    private XElement BuildElement(string aasIdShort, string submodelIdShort, ElementSpec element, HashSet<string> elementIdShorts, ReferenceIndex referenceIndex)
    {
        return element.Kind switch
        {
            ElementKind.Property => BuildProperty(element),
            ElementKind.Entity => BuildEntity(element),
            ElementKind.Relationship => BuildRelationship(element, elementIdShorts),
            ElementKind.ReferenceElement => BuildReferenceElement(aasIdShort, submodelIdShort, element, referenceIndex),
            _ => BuildProperty(element)
        };
    }

    private XElement BuildProperty(ElementSpec element)
    {
        return new XElement(_aasNs + "property",
            new XElement(_aasNs + "idShort", element.IdShort),
            CreateCategoryElement(element.Category),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(element.DisplayNameKo),
            CreateSemanticId(element.SemanticId),
            CreateQualifiers(),
            new XElement(_aasNs + "valueType", element.ValueType),
            new XElement(_aasNs + "value", element.Value),
            new XElement(_aasNs + "valueId",
                new XElement(_aasNs + "keys")
            )
        );
    }

    private XElement BuildReferenceElement(string aasIdShort, string submodelIdShort, ElementSpec element, ReferenceIndex referenceIndex)
    {
        var targetProperty = element.IdShort;
        string? submodelIri = null;
        string? propertyIdShort = null;

        if (element.ResolvedReference is not null)
        {
            submodelIri = _idProvider.GetSubmodelId(element.ResolvedReference.TargetAasIdShort, element.ResolvedReference.TargetSubmodelIdShort);
            propertyIdShort = element.ResolvedReference.TargetElementIdShort;
        }
        else
        {
            var targetAas = string.IsNullOrWhiteSpace(element.ReferenceTarget) ? aasIdShort : element.ReferenceTarget;
            var resolved = referenceIndex.ResolvePropertyReference(aasIdShort, submodelIdShort, targetAas, targetProperty);
            if (!resolved.IsResolved)
            {
                _diagnostics.MissingRelationshipReferences.Add($"ReferenceElement 대상 미확인: AAS={aasIdShort}, Submodel={submodelIdShort}, TargetAAS={targetAas}, Property={targetProperty}");
            }

            submodelIri = resolved.SubmodelIri ?? referenceIndex.GetFallbackSubmodelIri(aasIdShort, submodelIdShort);
            propertyIdShort = resolved.PropertyIdShort ?? targetProperty;
        }

        return new XElement(_aasNs + "referenceElement",
            new XElement(_aasNs + "idShort", element.IdShort),
            CreateCategoryElement(element.Category),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(element.DisplayNameKo),
            CreateSemanticId(element.SemanticId),
            CreateQualifiers(),
            new XElement(_aasNs + "value",
                new XElement(_aasNs + "keys",
                    CreateKey("Submodel", submodelIri ?? string.Empty, local: true, idType: "IRI"),
                    CreateKey("Property", propertyIdShort ?? targetProperty, local: true, idType: "IdShort")
                )
            )
        );
    }

    private XElement BuildEntity(ElementSpec element)
    {
        return new XElement(_aasNs + "entity",
            new XElement(_aasNs + "idShort", element.IdShort),
            CreateCategoryElement(element.Category),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(element.DisplayNameKo),
            CreateSemanticId(element.SemanticId),
            CreateQualifiers(),
            new XElement(_aasNs + "entityType", "SelfManagedEntity"),
            element.ReferenceTarget is null
                ? null
                : new XElement(_aasNs + "assetRef",
                    new XElement(_aasNs + "keys",
                        CreateKey("Asset", _idProvider.GetAssetId(element.ReferenceTarget), local: true)
                    )
                )
        );
    }

    private XElement BuildRelationship(ElementSpec element, HashSet<string> elementIdShorts)
    {
        var relationship = element.Relationship;
        var first = relationship?.First ?? string.Empty;
        var second = relationship?.Second ?? string.Empty;
        // 환경 내부 요소를 참조하는 경우 local=true로 설정한다.
        var firstLocal = IsIdShortReference(first) || elementIdShorts.Contains(first);
        var secondLocal = IsIdShortReference(second) || elementIdShorts.Contains(second);
        return new XElement(_aasNs + "relationshipElement",
            new XElement(_aasNs + "idShort", element.IdShort),
            CreateCategoryElement(element.Category),
            new XElement(_aasNs + "kind", "Instance"),
            CreateDescription(element.DisplayNameKo),
            CreateSemanticId(element.SemanticId),
            CreateQualifiers(),
            new XElement(_aasNs + "first",
                new XElement(_aasNs + "keys",
                    CreateKey("SubmodelElement", first, local: firstLocal)
                )
            ),
            new XElement(_aasNs + "second",
                new XElement(_aasNs + "keys",
                    CreateKey("SubmodelElement", second, local: secondLocal)
                )
            )
        );
    }


    private XElement? CreateCategoryElement(string? category)
    {
        var resolved = string.IsNullOrWhiteSpace(category)
            ? (_options.FillMissingCategoryWithConstant ? _options.MissingCategoryConstant : null)
            : category;

        return string.IsNullOrWhiteSpace(resolved) ? null : new XElement(_aasNs + "category", resolved);
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

    private XElement CreateIdentification(string id)
    {
        return new XElement(_aasNs + "identification",
            new XAttribute("idType", "IRI"),
            id
        );
    }

    private XElement CreateSemanticId(string? semanticId = null)
    {
        var keys = new XElement(_aasNs + "keys");
        if (!string.IsNullOrWhiteSpace(semanticId))
        {
            var isLocalConceptDescription = _conceptDescriptionIds.Contains(semanticId.Trim());
            keys.Add(isLocalConceptDescription
                ? CreateKey("ConceptDescription", semanticId, local: true, idType: "IRI")
                : CreateKey("GlobalReference", semanticId, local: false, idType: "IRI"));
        }

        return new XElement(_aasNs + "semanticId", keys);
    }

    private XElement? CreateDescription(List<LangStringSpec> description)
    {
        if (description.Count == 0)
        {
            return null;
        }

        var element = new XElement(_aasNs + "description");
        foreach (var item in description)
        {
            element.Add(new XElement(_aasNs + "langString",
                new XAttribute("lang", item.Language),
                item.Text));
        }

        return element;
    }

    private XElement BuildConceptDescription(ConceptDescriptionSpec conceptDescription)
    {
        var element = new XElement(_aasNs + "conceptDescription",
            new XElement(_aasNs + "idShort", conceptDescription.IdShort),
            CreateCategoryElement(conceptDescription.Category),
            CreateDescription(conceptDescription.Description),
            CreateIdentification(conceptDescription.Id));

        if (!string.IsNullOrWhiteSpace(conceptDescription.IsCaseOf))
        {
            element.Add(new XElement(_aasNs + "isCaseOf",
                new XElement(_aasNs + "keys",
                    CreateKey("GlobalReference", conceptDescription.IsCaseOf, local: false, idType: "IRI"))));
        }

        return element;
    }

    private XElement CreateQualifiers()
    {
        return new XElement(_aasNs + "qualifier");
    }

    private XElement CreateKey(string type, string value, bool local, string? idType = null)
    {
        // AAS V2 샘플 형식에 맞춰 key는 속성 + inner text로 표현한다.
        // 환경 내부 참조(assets/submodels/submodelElements 등)는 local=true로,
        // 외부 개념/표준(semanticId 등)은 local=false로 명시한다.
        var resolvedIdType = idType ?? ResolveIdType(value);

        return new XElement(_aasNs + "key",
            new XAttribute("type", type),
            new XAttribute("local", local ? "true" : "false"),
            new XAttribute("idType", resolvedIdType),
            value
        );
    }

    private static string ResolveIdType(string value)
    {
        // urn:uuid / http(s):// 형태는 IRI로, 그 외는 IdShort로 간주한다.
        if (value.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "IRI";
        }

        return "IdShort";
    }

    private static bool IsIdShortReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(ResolveIdType(value), "IdShort", StringComparison.OrdinalIgnoreCase);
    }

    private XElement WrapSubmodelElement(XElement element)
    {
        return new XElement(_aasNs + "submodelElement", element);
    }

}
