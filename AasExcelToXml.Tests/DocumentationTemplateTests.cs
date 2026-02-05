using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class DocumentationTemplateTests
{
    [Fact]
    public void Convert_Aas2_Documentation_Uses_Golden_Field_Schema()
    {
        var diagnostics = new SpecDiagnostics();

        var spec = new AasEnvironmentSpec(
            new List<AasSpec>
            {
                new("SampleAas", "SampleAas", new List<SubmodelSpec>
                {
                    new("Documentation", "Documentation", new List<ElementSpec>
                    {
                        new(string.Empty, "Document_name", "문서명", ElementKind.DocumentationInput, "string", "Sample Document", string.Empty, null, null),
                        new(string.Empty, "Document_type", "문서유형", ElementKind.DocumentationInput, "string", "Catalog", string.Empty, null, null),
                        new(string.Empty, "Document_file_path", "문서경로", ElementKind.DocumentationInput, "string", "/aasx/files/sample.pdf", string.Empty, null, null),
                        new("Cylinder_main", "Document_name", "문서명", ElementKind.DocumentationInput, "string", "Cylinder Spec", string.Empty, null, null),
                        new("Cylinder_main", "Document_file_path", "문서경로", ElementKind.DocumentationInput, "string", "/aasx/files/cylinder.pdf", string.Empty, null, null)
                    })
                })
            });

        var writer = new AasV2XmlWriter(new ConvertOptions { Version = AasVersion.Aas2_0, IncludeAllDocumentation = true }, diagnostics, new DocumentIdGenerator(64879470));
        var document = writer.Write(spec);

        var doc01 = ExtractDocumentElements(document, "Document01");
        var doc01Version = ExtractDocumentVersionElements(document, "Document01", "DocumentVersion01");
        var docSpec = ExtractDocumentElements(document, "Document_spec_cymain");
        var docSpecVersion = ExtractDocumentVersionElements(document, "Document_spec_cymain", "DocumentVersion");

        Assert.Equal(new[]
        {
            "DocumentId",
            "IsPrimaryDocumentId",
            "DocumentClassId",
            "DocumentClassName",
            "DocumentClassificationSystem",
            "DigitalFile",
            "DocumentVersion01"
        }, doc01);

        Assert.Equal(new[]
        {
            "Language01",
            "DocumentVersionId",
            "Title",
            "Summary",
            "KeyWords",
            "SetDate",
            "StatusValue",
            "Role",
            "OrganizationName",
            "OrganizationOfficialName"
        }, doc01Version);

        Assert.Equal(new[]
        {
            "DocumentId",
            "IsPrimary",
            "ClassId",
            "ClassName",
            "ClassificationSystem",
            "DocumentVersion"
        }, docSpec);

        Assert.Equal(new[]
        {
            "Language1",
            "Title",
            "Summary",
            "KeyWords",
            "SetDate",
            "StatusValue",
            "Role",
            "OrganizationName",
            "OrganizationOfficialName",
            "DigitalFile"
        }, docSpecVersion);
    }

    private static IReadOnlyList<string> ExtractDocumentElements(XDocument document, string documentIdShort)
    {
        var collection = FindDocumentCollection(document, documentIdShort);
        if (collection is null)
        {
            return Array.Empty<string>();
        }

        return ExtractValueChildren(collection)
            .Where(child => child.Name.LocalName != "submodelElementCollection")
            .Select(child => GetChildValue(child, "idShort"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static IReadOnlyList<string> ExtractDocumentVersionElements(XDocument document, string documentIdShort, string versionIdShort)
    {
        var collection = FindDocumentCollection(document, documentIdShort);
        if (collection is null)
        {
            return Array.Empty<string>();
        }

        var versionCollection = ExtractValueChildren(collection)
            .FirstOrDefault(child => child.Name.LocalName == "submodelElementCollection"
                                     && string.Equals(GetChildValue(child, "idShort"), versionIdShort, StringComparison.Ordinal));

        if (versionCollection is null)
        {
            return Array.Empty<string>();
        }

        return ExtractValueChildren(versionCollection)
            .Select(child => GetChildValue(child, "idShort"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static XElement? FindDocumentCollection(XDocument document, string documentIdShort)
    {
        var documentation = document.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "submodel"
                                 && string.Equals(GetChildValue(e, "idShort"), "Documentation", StringComparison.Ordinal));

        if (documentation is null)
        {
            return null;
        }

        return documentation.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "submodelElementCollection"
                                 && string.Equals(GetChildValue(e, "idShort"), documentIdShort, StringComparison.Ordinal));
    }

    private static IEnumerable<XElement> ExtractValueChildren(XElement element)
    {
        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
        if (value is null)
        {
            yield break;
        }

        foreach (var child in value.Elements())
        {
            if (child.Name.LocalName == "submodelElement")
            {
                var inner = child.Elements().FirstOrDefault();
                if (inner is not null)
                {
                    yield return inner;
                }

                continue;
            }

            yield return child;
        }
    }

    private static string GetChildValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? string.Empty;
    }
}
