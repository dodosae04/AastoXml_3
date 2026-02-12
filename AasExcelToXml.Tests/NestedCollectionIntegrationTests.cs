using System.Xml.Linq;
using AasExcelToXml.Core;
using ClosedXML.Excel;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class NestedCollectionIntegrationTests
{
    [Fact]
    public void Convert_WithNestedCollectionPath_CreatesNestedCollections_Aas3()
    {
        var inputPath = CreateCollectionWorkbook(useSplitColumns: false);
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-collection-v3-{Guid.NewGuid():N}.xml");

        try
        {
            Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions { Version = AasVersion.Aas3_0 });

            var doc = XDocument.Load(outputPath);
            XNamespace ns = "https://admin-shell.io/aas/3/0";
            var specCollection = doc.Descendants(ns + "submodelElementCollection")
                .FirstOrDefault(x => string.Equals(x.Element(ns + "idShort")?.Value, "Spec", StringComparison.Ordinal));
            Assert.NotNull(specCollection);

            var dimCollection = specCollection!.Descendants(ns + "submodelElementCollection")
                .FirstOrDefault(x => string.Equals(x.Element(ns + "idShort")?.Value, "Dim", StringComparison.Ordinal));
            Assert.NotNull(dimCollection);
            Assert.Contains(dimCollection!.Descendants(ns + "property"), p => string.Equals(p.Element(ns + "idShort")?.Value, "Mass", StringComparison.Ordinal));
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
    public void Convert_WithNestedCollectionPath_CreatesNestedCollections_Aas2()
    {
        var inputPath = CreateCollectionWorkbook(useSplitColumns: false);
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-collection-v2-{Guid.NewGuid():N}.xml");

        try
        {
            Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions { Version = AasVersion.Aas2_0 });

            var doc = XDocument.Load(outputPath);
            XNamespace ns = "http://www.admin-shell.io/aas/2/0";
            var specCollection = doc.Descendants(ns + "submodelElementCollection")
                .FirstOrDefault(x => string.Equals(x.Element(ns + "idShort")?.Value, "Spec", StringComparison.Ordinal));
            Assert.NotNull(specCollection);

            var dimCollection = specCollection!.Descendants(ns + "submodelElementCollection")
                .FirstOrDefault(x => string.Equals(x.Element(ns + "idShort")?.Value, "Dim", StringComparison.Ordinal));
            Assert.NotNull(dimCollection);
            Assert.Contains(dimCollection!.Descendants(ns + "property"), p => string.Equals(p.Element(ns + "idShort")?.Value, "Mass", StringComparison.Ordinal));
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
    public void Convert_WithCollectionLevelColumns_CreatesNestedCollections_Aas3()
    {
        var inputPath = CreateCollectionWorkbook(useSplitColumns: true);
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-collection-cols-{Guid.NewGuid():N}.xml");

        try
        {
            Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions { Version = AasVersion.Aas3_0 });

            var doc = XDocument.Load(outputPath);
            XNamespace ns = "https://admin-shell.io/aas/3/0";
            var dimCollection = doc.Descendants(ns + "submodelElementCollection")
                .FirstOrDefault(x => string.Equals(x.Element(ns + "idShort")?.Value, "Dim", StringComparison.Ordinal));
            Assert.NotNull(dimCollection);
            Assert.Contains(dimCollection!.Descendants(ns + "property"), p => string.Equals(p.Element(ns + "idShort")?.Value, "Mass", StringComparison.Ordinal));
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
    public void Convert_WithCollectionLevelColumns_CreatesNestedCollections_Aas2()
    {
        var inputPath = CreateCollectionWorkbook(useSplitColumns: true);
        var outputPath = Path.Combine(Path.GetTempPath(), $"aas-collection-cols-v2-{Guid.NewGuid():N}.xml");

        try
        {
            Converter.Convert(inputPath, outputPath, "사양시트", new ConvertOptions { Version = AasVersion.Aas2_0 });

            var doc = XDocument.Load(outputPath);
            XNamespace ns = "http://www.admin-shell.io/aas/2/0";
            var dimCollection = doc.Descendants(ns + "submodelElementCollection")
                .FirstOrDefault(x => string.Equals(x.Element(ns + "idShort")?.Value, "Dim", StringComparison.Ordinal));
            Assert.NotNull(dimCollection);
            Assert.Contains(dimCollection!.Descendants(ns + "property"), p => string.Equals(p.Element(ns + "idShort")?.Value, "Mass", StringComparison.Ordinal));
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

    private static string CreateCollectionWorkbook(bool useSplitColumns)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nested-collection-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();

        var ws = wb.AddWorksheet("사양시트");
        ws.Cell(1, 1).Value = "Asset";
        ws.Cell(1, 2).Value = "Submodel";
        ws.Cell(1, 3).Value = "SubmodelCollection";
        ws.Cell(1, 4).Value = "Property_Kor";
        ws.Cell(1, 5).Value = "Property_Eng";
        ws.Cell(1, 6).Value = "Property type";
        ws.Cell(1, 7).Value = "Value";

        if (useSplitColumns)
        {
            ws.Cell(1, 8).Value = "SubmodelCollection1";
            ws.Cell(1, 9).Value = "SubmodelCollection2";
        }

        ws.Cell(2, 1).Value = "Robot";
        ws.Cell(2, 2).Value = "Operational Information";
        ws.Cell(2, 3).Value = useSplitColumns ? string.Empty : "Spec>Dim";
        ws.Cell(2, 4).Value = "질량";
        ws.Cell(2, 5).Value = "Mass";
        ws.Cell(2, 6).Value = "double";
        ws.Cell(2, 7).Value = "12.3";

        if (useSplitColumns)
        {
            ws.Cell(2, 8).Value = "Spec";
            ws.Cell(2, 9).Value = "Dim";
        }

        wb.SaveAs(path);
        return path;
    }
}
