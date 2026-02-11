using System.Text.RegularExpressions;
using System.Xml.Linq;
using AasExcelToXml.Core;
using ClosedXML.Excel;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class ConceptDescriptionIdPolicyTests
{
    [Theory]
    [InlineData(AasVersion.Aas2_0)]
    [InlineData(AasVersion.Aas3_0)]
    public void ExampleIri_DeterministicMode_UsesCdPathWithStableSuffix(AasVersion version)
    {
        var inputPath = CreateWorkbookWithExternalReference("(자동생성)");
        var outputPath1 = Path.Combine(Path.GetTempPath(), $"aas-cd-det-1-{Guid.NewGuid():N}.xml");
        var outputPath2 = Path.Combine(Path.GetTempPath(), $"aas-cd-det-2-{Guid.NewGuid():N}.xml");

        try
        {
            var options = new ConvertOptions
            {
                Version = version,
                IdScheme = IdScheme.ExampleIri,
                BaseIri = "https://example.com/ids/",
                ExampleIriDigitsMode = ExampleIriDigitsMode.DeterministicHash
            };

            Converter.Convert(inputPath, outputPath1, "사양시트", options, inputPath, "ExternalReferenceData");
            Converter.Convert(inputPath, outputPath2, "사양시트", options, inputPath, "ExternalReferenceData");

            var id1 = ReadConceptDescriptionId(outputPath1, "CD_TEST");
            var id2 = ReadConceptDescriptionId(outputPath2, "CD_TEST");

            Assert.Equal(id1, id2);
            Assert.StartsWith("https://example.com/ids/cd/", id1, StringComparison.Ordinal);
            Assert.Matches(new Regex(@"^https://example\.com/ids/cd/\d{4}_\d{4}_\d{4}_\d{4}$"), id1);
            Assert.Equal(id1, ReadPropertySemanticId(outputPath1, "Payload"));
        }
        finally
        {
            DeleteIfExists(inputPath);
            DeleteIfExists(outputPath1);
            DeleteIfExists(outputPath2);
        }
    }

    [Fact]
    public void ExampleIri_RandomMode_GeneratesDifferentConceptDescriptionIdsAcrossRuns()
    {
        var inputPath = CreateWorkbookWithExternalReference(string.Empty);
        var outputPath1 = Path.Combine(Path.GetTempPath(), $"aas-cd-rand-1-{Guid.NewGuid():N}.xml");
        var outputPath2 = Path.Combine(Path.GetTempPath(), $"aas-cd-rand-2-{Guid.NewGuid():N}.xml");

        try
        {
            var options = new ConvertOptions
            {
                Version = AasVersion.Aas3_0,
                IdScheme = IdScheme.ExampleIri,
                BaseIri = "https://example.com/ids",
                ExampleIriDigitsMode = ExampleIriDigitsMode.RandomSecure
            };

            Converter.Convert(inputPath, outputPath1, "사양시트", options, inputPath, "ExternalReferenceData");
            Converter.Convert(inputPath, outputPath2, "사양시트", options, inputPath, "ExternalReferenceData");

            var id1 = ReadConceptDescriptionId(outputPath1, "CD_TEST");
            var id2 = ReadConceptDescriptionId(outputPath2, "CD_TEST");

            Assert.NotEqual(id1, id2);
            Assert.StartsWith("https://example.com/ids/cd/", id1, StringComparison.Ordinal);
            Assert.StartsWith("https://example.com/ids/cd/", id2, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(inputPath);
            DeleteIfExists(outputPath1);
            DeleteIfExists(outputPath2);
        }
    }

    [Theory]
    [InlineData(AasVersion.Aas2_0)]
    [InlineData(AasVersion.Aas3_0)]
    public void ExplicitIdentifiableId_IsPreserved(AasVersion version)
    {
        const string explicitId = "urn:example:external:cd:test";
        var inputPath = CreateWorkbookWithExternalReference(explicitId);
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-cd-explicit-{Guid.NewGuid():N}.xml");

        try
        {
            var options = new ConvertOptions
            {
                Version = version,
                IdScheme = IdScheme.ExampleIri,
                BaseIri = "https://example.com/ids",
                ExampleIriDigitsMode = ExampleIriDigitsMode.DeterministicHash
            };

            Converter.Convert(inputPath, outputPath, "사양시트", options, inputPath, "ExternalReferenceData");

            Assert.Equal(explicitId, ReadConceptDescriptionId(outputPath, "CD_TEST"));
            Assert.Equal(explicitId, ReadPropertySemanticId(outputPath, "Payload"));
        }
        finally
        {
            DeleteIfExists(inputPath);
            DeleteIfExists(outputPath);
        }
    }

    private static string ReadConceptDescriptionId(string xmlPath, string idShort)
    {
        var doc = XDocument.Load(xmlPath);
        var concept = doc.Descendants().First(e =>
            e.Name.LocalName == "conceptDescription" &&
            string.Equals(e.Elements().FirstOrDefault(c => c.Name.LocalName == "idShort")?.Value, idShort, StringComparison.Ordinal));

        var idElement = concept.Elements().FirstOrDefault(e => e.Name.LocalName == "id");
        if (idElement is not null)
        {
            return idElement.Value;
        }

        var identification = concept.Elements().First(e => e.Name.LocalName == "identification");
        return identification.Attribute("id")?.Value
               ?? identification.Value;
    }

    private static string ReadPropertySemanticId(string xmlPath, string propertyIdShort)
    {
        var doc = XDocument.Load(xmlPath);
        var property = doc.Descendants().First(e =>
            e.Name.LocalName == "property" &&
            string.Equals(e.Elements().FirstOrDefault(c => c.Name.LocalName == "idShort")?.Value, propertyIdShort, StringComparison.Ordinal));

        return property
            .Descendants()
            .First(e => e.Name.LocalName == "key")
            .Elements()
            .First(e => e.Name.LocalName == "value")
            .Value;
    }

    private static string CreateWorkbookWithExternalReference(string identifiableId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cd-id-policy-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();

        var main = wb.AddWorksheet("사양시트");
        main.Cell(1, 1).Value = "Asset";
        main.Cell(1, 2).Value = "Submodel";
        main.Cell(1, 3).Value = "SubmodelCollection";
        main.Cell(1, 4).Value = "Property_Kor";
        main.Cell(1, 5).Value = "Property_Eng";
        main.Cell(1, 6).Value = "Property type";
        main.Cell(1, 7).Value = "Value";
        main.Cell(1, 8).Value = "Reference_data";

        main.Cell(2, 1).Value = "Robot";
        main.Cell(2, 2).Value = "Operational Information";
        main.Cell(2, 4).Value = "적재량";
        main.Cell(2, 5).Value = "Payload";
        main.Cell(2, 6).Value = "string";
        main.Cell(2, 7).Value = "5";
        main.Cell(2, 8).Value = "CD_TEST";

        var external = wb.AddWorksheet("ExternalReferenceData");
        external.Cell(1, 1).Value = "IdShort";
        external.Cell(1, 2).Value = "Category";
        external.Cell(1, 3).Value = "Description Language";
        external.Cell(1, 4).Value = "Description";
        external.Cell(1, 5).Value = "Identifiable ID";

        external.Cell(2, 1).Value = "CD_TEST";
        external.Cell(2, 2).Value = "CONSTANT";
        external.Cell(2, 3).Value = "en";
        external.Cell(2, 4).Value = "test";
        external.Cell(2, 5).Value = identifiableId;

        wb.SaveAs(path);
        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
