namespace AasExcelToXml.Core;

public static class Converter
{
    // 변환 로직을 Core에 집중시키기 위해 입력/출력만 받아 처리한다.
    public static ConvertResult Convert(string inputPath, string outputPath, string? sheetName = null, ConvertOptions? options = null)
    {
        options ??= new ConvertOptions();
        if (string.IsNullOrWhiteSpace(options.InputFileName))
        {
            options.InputFileName = Path.GetFileNameWithoutExtension(inputPath);
        }
        var rows = ExcelSpecReader.ReadRows(inputPath, sheetName);
        var spec = SpecGrouper.BuildEnvironmentSpec(rows, out var diagnostics);
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
}

public sealed record ConvertResult(string OutputPath, string WarningsPath, SpecDiagnostics Diagnostics);
