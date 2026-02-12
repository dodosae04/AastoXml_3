using System.Xml.Linq;
using AasExcelToXml.Core;
using ClosedXML.Excel;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class ExternalReferenceIntegrationTests
{
    [Fact]
    public void ReadExternalReferenceRows_ReturnsExpectedCount()
    {
        var path = CreateWorkbook();
        try
        {
            var diagnostics = new SpecDiagnostics();
            var rows = ExcelSpecReader.ReadExternalReferenceRows(path, "ExternalReferenceData", diagnostics);

            Assert.Equal(2, rows.Count);
            Assert.Empty(diagnostics.ExternalReferenceIssues.Where(x => x.Contains("IdShort 누락", StringComparison.Ordinal)));
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void Convert_WithExternalReferenceMultiSheet_MergesConceptDescriptions()
    {
        var inputPath = CreateWorkbookWithMultiExternalSheets();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-external-ref-multi-{Guid.NewGuid():N}.xml");

        try
        {
            var result = Converter.Convert(
                inputPath,
                outputPath,
                "사양시트",
                new ConvertOptions { Version = AasVersion.Aas3_0 },
                inputPath,
                "ExternalReferenceData1;ExternalReferenceData2");

            var doc = XDocument.Load(result.OutputPath);
            XNamespace ns = "https://admin-shell.io/aas/3/0";
            var conceptDescriptions = doc.Descendants(ns + "conceptDescription").ToList();
            Assert.Equal(3, conceptDescriptions.Count);
        }
        finally
        {
            File.Delete(inputPath);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void Convert_WithExternalReferenceWildcard_LoadsOnlySheetsWithHeaders()
    {
        var inputPath = CreateWorkbookWithMultiExternalSheets();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-external-ref-all-{Guid.NewGuid():N}.xml");

        try
        {
            var result = Converter.Convert(
                inputPath,
                outputPath,
                "사양시트",
                new ConvertOptions { Version = AasVersion.Aas3_0 },
                inputPath,
                "*");

            var doc = XDocument.Load(result.OutputPath);
            XNamespace ns = "https://admin-shell.io/aas/3/0";
            var conceptDescriptions = doc.Descendants(ns + "conceptDescription").ToList();
            Assert.Equal(3, conceptDescriptions.Count);
            Assert.DoesNotContain(conceptDescriptions, cd => string.Equals(cd.Element(ns + "idShort")?.Value, "NoiseSheet", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputPath);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void Convert_WithExternalReference_AssignsSemanticIdAndConceptDescriptions()
    {
        var inputPath = CreateWorkbook();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-external-ref-{Guid.NewGuid():N}.xml");

        try
        {
            var result = Converter.Convert(
                inputPath,
                outputPath,
                "사양시트",
                new ConvertOptions { Version = AasVersion.Aas3_0 },
                inputPath,
                "ExternalReferenceData");

            Assert.True(File.Exists(result.OutputPath));

            var doc = XDocument.Load(result.OutputPath);
            XNamespace ns = "https://admin-shell.io/aas/3/0";

            var conceptDescriptions = doc.Descendants(ns + "conceptDescription").ToList();
            Assert.Equal(2, conceptDescriptions.Count);

            foreach (var conceptDescription in conceptDescriptions)
            {
                var description = conceptDescription.Element(ns + "description");
                Assert.NotNull(description);
                Assert.Empty(description!.Elements(ns + "langString"));

                var langStringTextType = description.Elements(ns + "langStringTextType").Single();
                Assert.NotNull(langStringTextType.Element(ns + "language"));
                Assert.NotNull(langStringTextType.Element(ns + "text"));
            }

            var millimetreCd = conceptDescriptions.Single(cd => string.Equals(cd.Element(ns + "idShort")?.Value, "CD_UOM_mm", StringComparison.Ordinal));
            var isCaseOf = millimetreCd.Element(ns + "isCaseOf");
            Assert.NotNull(isCaseOf);
            Assert.Equal("reference", isCaseOf!.Elements().First().Name.LocalName);

            var property = doc.Descendants(ns + "property")
                .First(p => string.Equals(p.Element(ns + "idShort")?.Value, "Payload", StringComparison.Ordinal));
            var key = property.Descendants(ns + "key").FirstOrDefault(k => string.Equals(k.Element(ns + "type")?.Value, "GlobalReference", StringComparison.Ordinal));
            Assert.NotNull(key);
            Assert.False(string.IsNullOrWhiteSpace(key!.Element(ns + "value")?.Value));
        }
        finally
        {
            File.Delete(inputPath);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }


    private static string CreateWorkbookWithMultiExternalSheets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"external-ref-multi-{Guid.NewGuid():N}.xlsx");
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
        main.Cell(2, 8).Value = "CD_UOM_mm";

        void FillHeader(IXLWorksheet ws)
        {
            ws.Cell(1, 1).Value = "IdShort";
            ws.Cell(1, 2).Value = "Category";
            ws.Cell(1, 3).Value = "Description Language";
            ws.Cell(1, 4).Value = "Description";
            ws.Cell(1, 5).Value = "Identifiable ID";
            ws.Cell(1, 6).Value = "isCaseOf";
        }

        var ext1 = wb.AddWorksheet("ExternalReferenceData1");
        FillHeader(ext1);
        ext1.Cell(2, 1).Value = "CD_UOM_mm";
        ext1.Cell(2, 2).Value = "CONSTANT";
        ext1.Cell(2, 3).Value = "en";
        ext1.Cell(2, 4).Value = "millimetre";
        ext1.Cell(2, 5).Value = "urn:cd:mm";

        var ext2 = wb.AddWorksheet("ExternalReferenceData2");
        FillHeader(ext2);
        ext2.Cell(2, 1).Value = "CD_UOM_kg";
        ext2.Cell(2, 2).Value = "CONSTANT";
        ext2.Cell(2, 3).Value = "en";
        ext2.Cell(2, 4).Value = "kilogram";
        ext2.Cell(2, 5).Value = "urn:cd:kg";

        ext2.Cell(3, 1).Value = "CD_UOM_s";
        ext2.Cell(3, 2).Value = "CONSTANT";
        ext2.Cell(3, 3).Value = "en";
        ext2.Cell(3, 4).Value = "second";
        ext2.Cell(3, 5).Value = "urn:cd:s";

        var noise = wb.AddWorksheet("IgnoreMe");
        noise.Cell(1, 1).Value = "something";
        noise.Cell(2, 1).Value = "NoiseSheet";

        wb.SaveAs(path);
        return path;
    }

    private static string CreateWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"external-ref-{Guid.NewGuid():N}.xlsx");
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
        main.Cell(2, 3).Value = "";
        main.Cell(2, 4).Value = "적재량";
        main.Cell(2, 5).Value = "Payload";
        main.Cell(2, 6).Value = "string";
        main.Cell(2, 7).Value = "5";
        main.Cell(2, 8).Value = "CD_UOM_mm";

        var external = wb.AddWorksheet("ExternalReferenceData");
        external.Cell(1, 1).Value = "IdShort";
        external.Cell(1, 2).Value = "Category";
        external.Cell(1, 3).Value = "Description Language";
        external.Cell(1, 4).Value = "Description";
        external.Cell(1, 5).Value = "Identifiable ID";
        external.Cell(1, 6).Value = "isCaseOf";

        external.Cell(2, 1).Value = "CD_UOM_mm";
        external.Cell(2, 2).Value = "CONSTANT";
        external.Cell(2, 3).Value = "en";
        external.Cell(2, 4).Value = "millimetre";
        external.Cell(2, 5).Value = "urn:cd:mm";
        external.Cell(2, 6).Value = "http://data.15926.org/rdl/RDS1357739";

        external.Cell(3, 1).Value = "CD_UOM_kg";
        external.Cell(3, 2).Value = "CONSTANT";
        external.Cell(3, 3).Value = "en";
        external.Cell(3, 4).Value = "kilogram";
        external.Cell(3, 5).Value = "urn:cd:kg";

        wb.SaveAs(path);
        return path;
    }
}
