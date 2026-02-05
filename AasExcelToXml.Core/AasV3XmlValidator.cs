using System;
using System.Linq;
using System.Xml.Linq;

namespace AasExcelToXml.Core;

public static class AasV3XmlValidator
{
    public static void Validate(XDocument document, Aas3Profile profile, SpecDiagnostics diagnostics)
    {
        CheckSemanticIds(document, profile, diagnostics);
        CheckEmptyKeys(document, diagnostics);
        CheckEmptyQualifiers(document, diagnostics);
        CheckEmptyCategories(document, diagnostics);
        CheckPropertyValueTypes(document, diagnostics);
        CheckRelationshipReferenceWrapping(document, diagnostics);
    }

    private static void CheckSemanticIds(XDocument document, Aas3Profile profile, SpecDiagnostics diagnostics)
    {
        foreach (var semanticId in document.Descendants().Where(e => e.Name.LocalName == "semanticId"))
        {
            var hasReferenceWrapper = semanticId.Elements().Any(e => e.Name.LocalName == "reference");
            if (hasReferenceWrapper && !profile.Reference.SemanticIdWrapsReference)
            {
                diagnostics.Aas3ValidationIssues.Add("semanticId에 reference 래퍼가 포함되어 있습니다.");
                return;
            }

            var keys = semanticId.Descendants().Where(e => e.Name.LocalName == "key").ToList();
            if (keys.Count == 0)
            {
                diagnostics.Aas3ValidationIssues.Add("semanticId에 key가 없습니다.");
                return;
            }
        }
    }

    private static void CheckEmptyKeys(XDocument document, SpecDiagnostics diagnostics)
    {
        foreach (var keys in document.Descendants().Where(e => e.Name.LocalName == "keys"))
        {
            if (!keys.Elements().Any(e => e.Name.LocalName == "key"))
            {
                diagnostics.Aas3ValidationIssues.Add("keys 요소가 비어 있습니다.");
                return;
            }
        }
    }

    private static void CheckEmptyQualifiers(XDocument document, SpecDiagnostics diagnostics)
    {
        foreach (var qualifiers in document.Descendants().Where(e => e.Name.LocalName == "qualifiers"))
        {
            if (!qualifiers.Elements().Any(e => e.Name.LocalName == "qualifier"))
            {
                diagnostics.Aas3ValidationIssues.Add("qualifiers 요소가 비어 있습니다.");
                return;
            }
        }
    }

    private static void CheckEmptyCategories(XDocument document, SpecDiagnostics diagnostics)
    {
        foreach (var category in document.Descendants().Where(e => e.Name.LocalName == "category"))
        {
            if (string.IsNullOrWhiteSpace(category.Value))
            {
                diagnostics.Aas3ValidationIssues.Add("category 요소가 비어 있습니다.");
                return;
            }
        }
    }

    private static void CheckPropertyValueTypes(XDocument document, SpecDiagnostics diagnostics)
    {
        foreach (var property in document.Descendants().Where(e => e.Name.LocalName == "property"))
        {
            var valueType = property.Elements().FirstOrDefault(e => e.Name.LocalName == "valueType");
            if (valueType is null)
            {
                diagnostics.Aas3ValidationIssues.Add("property에 valueType이 없습니다.");
                return;
            }

            if (string.IsNullOrWhiteSpace(valueType.Value))
            {
                diagnostics.Aas3ValidationIssues.Add("property valueType 값이 비어 있습니다.");
                return;
            }

            var normalized = valueType.Value.Trim();
            if (!normalized.StartsWith("xs:", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("xsd:", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Aas3ValidationIssues.Add($"property valueType이 XSD 타입이 아닙니다: {normalized}");
                return;
            }
        }
    }

    private static void CheckRelationshipReferenceWrapping(XDocument document, SpecDiagnostics diagnostics)
    {
        foreach (var relationship in document.Descendants().Where(e => e.Name.LocalName == "relationshipElement"))
        {
            var first = relationship.Elements().FirstOrDefault(e => e.Name.LocalName == "first");
            var second = relationship.Elements().FirstOrDefault(e => e.Name.LocalName == "second");
            if (first?.Elements().Any(e => e.Name.LocalName == "reference") == true
                || second?.Elements().Any(e => e.Name.LocalName == "reference") == true)
            {
                diagnostics.Aas3ValidationIssues.Add("relationshipElement의 first/second에 reference 래퍼가 포함되어 있습니다.");
                return;
            }
        }
    }
}
