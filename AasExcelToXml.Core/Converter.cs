namespace AasExcelToXml.Core;

// [역할] 변환 파이프라인의 진입점으로 엑셀 읽기→스펙 그룹핑→XML 작성→경고 파일 저장을 오케스트레이션한다.
// [입력] 입력 엑셀 경로, 출력 XML 경로, 선택 시트명, ConvertOptions.
// [출력] 생성된 XML 파일 경로/경고 파일 경로/진단 정보를 담은 ConvertResult.
// [수정 포인트] 옵션 동작(category constant, warnings 기록 조건)을 바꾸면 전체 결과물 정책이 달라진다.
public static class Converter
{
    /// <summary>
    /// 엑셀 사양서를 AAS XML로 변환한다.
    /// </summary>
    /// <param name="inputPath">입력 엑셀 파일 경로.</param>
    /// <param name="outputPath">출력 XML 파일 경로.</param>
    /// <param name="sheetName">대상 시트명(비우면 기본 규칙에 따라 선택).</param>
    /// <param name="options">변환 옵션(버전/카테고리/경고 정책 등).</param>
    /// <returns>출력 파일 및 진단 정보.</returns>
    /// <remarks>
    /// 이 메서드를 수정하면 전체 변환 흐름과 파일 입출력 정책이 변경된다.
    /// Relationship 명명 규칙 자체는 SpecGrouper에서 확정되어 여기서는 그대로 전달된다.
    /// </remarks>
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
