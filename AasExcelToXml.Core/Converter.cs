namespace AasExcelToXml.Core;

public static class Converter
{
    public static ConvertResult Convert(string inputPath, string outputPath, string? sheetName = null, ConvertOptions? options = null)
    {
        options ??= new ConvertOptions();
        if (string.IsNullOrWhiteSpace(options.InputFileName))
        {
            options.InputFileName = Path.GetFileNameWithoutExtension(inputPath);
        }

        var rows = ExcelSpecReader.ReadRows(inputPath, sheetName);
        var spec = SpecGrouper.BuildEnvironmentSpec(rows, out var diagnostics);
        spec = ApplyCategorySettings(spec, options);

        var documentIdGenerator = new DocumentIdGenerator(options.DocumentIdSeed);
        var doc = AasXmlWriterFactory.Write(spec, options, diagnostics, documentIdGenerator);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        File.WriteAllText(outputPath, doc.ToString());

        var warningsPath = string.IsNullOrWhiteSpace(outputDir)
            ? "warnings.txt"
            : Path.Combine(outputDir, "warnings.txt");
        var report = diagnostics.CreateReport();
        if (!options.WriteWarningsOnlyWhenNeeded || diagnostics.WarningCount > 0)
        {
            File.WriteAllText(warningsPath, report);
        }
        else if (File.Exists(warningsPath))
        {
            File.Delete(warningsPath);
        }

        return new ConvertResult(outputPath, warningsPath, diagnostics);
    }

    private static AasEnvironmentSpec ApplyCategorySettings(AasEnvironmentSpec spec, ConvertOptions options)
    {
        if (!options.FillMissingCategoryWithConstant)
        {
            return spec;
        }

        var constant = options.MissingCategoryConstant?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(constant))
        {
            return spec;
        }

        var assets = spec.Assets
            .Select(asset => asset with
            {
                Submodels = asset.Submodels
                    .Select(submodel => submodel with
                    {
                        Elements = submodel.Elements
                            .Select(element => string.IsNullOrWhiteSpace(element.Category) ? element with { Category = constant } : element)
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return new AasEnvironmentSpec(assets);
    }
}

public sealed record ConvertResult(string OutputPath, string WarningsPath, SpecDiagnostics Diagnostics);
