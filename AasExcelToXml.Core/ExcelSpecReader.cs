using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace AasExcelToXml.Core;

public static class ExcelSpecReader
{
    private static readonly string[] ExternalReferenceAllTokens = { "*", "ALL", "(전체)", "전체" };

    public static List<string> GetWorksheetNames(string excelPath)
    {
        if (!File.Exists(excelPath) || !string.Equals(Path.GetExtension(excelPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

        using var wb = OpenWorkbookWithRetry(excelPath);
        return wb.Worksheets.Select(w => w.Name).ToList();
    }

    public static List<SpecRow> ReadRows(string inputPath, string? sheetName = null)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"입력 파일을 찾을 수 없습니다: {inputPath}");
        }

        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        return extension switch
        {
            ".xlsx" => ReadXlsxRows(inputPath, sheetName),
            ".csv" => ReadCsvRows(inputPath),
            _ => throw new InvalidOperationException($"지원하지 않는 입력 형식입니다: {extension}")
        };
    }

    public static List<ExternalReferenceRow> ReadExternalReferenceRows(string excelPath, string sheetName, SpecDiagnostics diag)
    {
        using var wb = OpenWorkbookWithRetry(excelPath);
        var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, sheetName, StringComparison.OrdinalIgnoreCase));
        if (ws is null)
        {
            var candidates = string.Join(", ", wb.Worksheets.Select(w => w.Name));
            throw new InvalidOperationException($"'{sheetName}' 시트를 찾을 수 없습니다. 사용 가능한 시트: {candidates}");
        }

        return ReadExternalReferenceRowsFromWorksheet(ws, excelPath, diag);
    }

    public static List<ExternalReferenceRow> ReadExternalReferenceRowsMulti(string externalRefExcelPath, string externalRefSheetName, SpecDiagnostics diag)
    {
        var files = SplitMultiValues(externalRefExcelPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sheetTokens = SplitMultiValues(externalRefSheetName).ToList();
        if (files.Count == 0 || sheetTokens.Count == 0)
        {
            return new List<ExternalReferenceRow>();
        }

        var loadAllSheets = sheetTokens.Any(IsExternalReferenceAllToken);
        var allRows = new List<ExternalReferenceRow>();

        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                diag.ExternalReferenceIssues.Add($"외부참조 파일을 찾을 수 없어 건너뜀: {file}");
                continue;
            }

            using var workbook = OpenWorkbookWithRetry(file);
            var worksheets = loadAllSheets
                ? workbook.Worksheets.Where(HasExternalReferenceHeader).ToList()
                : ResolveRequestedSheets(workbook, sheetTokens, diag, file);

            foreach (var worksheet in worksheets)
            {
                allRows.AddRange(ReadExternalReferenceRowsFromWorksheet(worksheet, file, diag));
            }
        }

        return allRows;
    }

    private static List<SpecRow> ReadXlsxRows(string xlsxPath, string? sheetName)
    {
        using var wb = OpenWorkbookWithRetry(xlsxPath);
        var selectedSheetName = string.IsNullOrWhiteSpace(sheetName) ? "사양시트" : sheetName.Trim();
        var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, selectedSheetName, StringComparison.OrdinalIgnoreCase));
        if (ws is null)
        {
            var candidates = string.Join(", ", wb.Worksheets.Select(w => w.Name));
            throw new InvalidOperationException($"'{selectedSheetName}' 시트를 찾을 수 없습니다. 사용 가능한 시트: {candidates}");
        }

        var headerRow = ws.FirstRowUsed();
        if (headerRow is null)
        {
            throw new InvalidOperationException("헤더 행을 찾을 수 없습니다.");
        }

        var headerMap = headerRow.CellsUsed()
            .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
            .ToDictionary(c => ColumnResolver.NormalizeHeaderKey(c.GetString()), c => c.Address.ColumnNumber);

        var columns = ResolveColumns(headerMap);

        var list = new List<SpecRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            if (IsEmptyRow(index => row.Cell(index).GetString(), columns.Required))
            {
                continue;
            }

            var collection = ResolveCollectionValue(
                headerMap,
                index => row.Cell(index).GetString(),
                columns.SubmodelCollection);

            list.Add(new SpecRow(
                Aas: row.Cell(columns.Asset).GetString().Trim(),
                Submodel: row.Cell(columns.Submodel).GetString().Trim(),
                Collection: collection,
                PropKor: row.Cell(columns.PropertyKor).GetString().Trim(),
                PropEng: row.Cell(columns.PropertyEng).GetString().Trim(),
                PropType: row.Cell(columns.PropertyType).GetString().Trim(),
                Value: row.Cell(columns.Value).GetString().Trim(),
                Uom: columns.Uom.HasValue ? row.Cell(columns.Uom.Value).GetString().Trim() : string.Empty,
                Category: columns.Category.HasValue ? row.Cell(columns.Category.Value).GetString().Trim() : string.Empty,
                ReferenceData: columns.ReferenceData.HasValue ? row.Cell(columns.ReferenceData.Value).GetString().Trim() : string.Empty
            ));
        }

        return list;
    }

    private static XLWorkbook OpenWorkbookWithRetry(string xlsxPath)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var stream = new FileStream(xlsxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                try
                {
                    return new XLWorkbook(stream);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(200 * attempt);
            }
            catch (IOException ex)
            {
                throw new IOException("엑셀 파일을 읽을 수 없습니다. 엑셀에서 파일을 닫고 다시 시도", ex);
            }
        }

        throw new IOException("엑셀 파일을 읽을 수 없습니다. 엑셀에서 파일을 닫고 다시 시도");
    }

    private static List<SpecRow> ReadCsvRows(string csvPath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            DetectColumnCountChanges = false
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new InvalidOperationException("CSV 헤더를 찾을 수 없습니다.");
        }

        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var headerMap = headers
            .Select((name, index) => new { name, index })
            .Where(item => !string.IsNullOrWhiteSpace(item.name))
            .ToDictionary(item => ColumnResolver.NormalizeHeaderKey(item.name), item => item.index);

        var columns = ResolveColumns(headerMap);

        var list = new List<SpecRow>();
        while (csv.Read())
        {
            if (IsEmptyRow(index => csv.GetField(index), columns.Required))
            {
                continue;
            }

            var collection = ResolveCollectionValue(
                headerMap,
                index => csv.GetField(index),
                columns.SubmodelCollection);

            list.Add(new SpecRow(
                Aas: csv.GetField(columns.Asset)?.Trim() ?? string.Empty,
                Submodel: csv.GetField(columns.Submodel)?.Trim() ?? string.Empty,
                Collection: collection,
                PropKor: csv.GetField(columns.PropertyKor)?.Trim() ?? string.Empty,
                PropEng: csv.GetField(columns.PropertyEng)?.Trim() ?? string.Empty,
                PropType: csv.GetField(columns.PropertyType)?.Trim() ?? string.Empty,
                Value: csv.GetField(columns.Value)?.Trim() ?? string.Empty,
                Uom: columns.Uom.HasValue ? (csv.GetField(columns.Uom.Value)?.Trim() ?? string.Empty) : string.Empty,
                Category: columns.Category.HasValue ? (csv.GetField(columns.Category.Value)?.Trim() ?? string.Empty) : string.Empty,
                ReferenceData: columns.ReferenceData.HasValue ? (csv.GetField(columns.ReferenceData.Value)?.Trim() ?? string.Empty) : string.Empty
            ));
        }

        return list;
    }

    private static ColumnIndices ResolveColumns(Dictionary<string, int> headerMap)
    {
        var settings = new SettingsXmlLoader().LoadOrCreate();
        var resolver = new ColumnResolver(settings);
        var columns = resolver.Resolve(headerMap);

        int Require(string key)
        {
            if (columns.TryGetValue(key, out var idx))
            {
                return idx;
            }

            throw new InvalidOperationException($"필수 헤더를 찾을 수 없습니다: {key} (setting.xml 확인)");
        }

        int? Optional(string key)
        {
            return columns.TryGetValue(key, out var idx) ? idx : null;
        }

        return new ColumnIndices(
            Asset: Require("Asset"),
            Submodel: Require("Submodel"),
            SubmodelCollection: Require("SubmodelCollection"),
            PropertyKor: Require("PropertyKor"),
            PropertyEng: Require("PropertyEng"),
            PropertyType: Require("PropertyType"),
            Value: Require("Value"),
            Uom: Optional("UOM"),
            Category: Optional("Category"),
            ReferenceData: Optional("ReferenceData"));
    }

    private static ExternalReferenceColumnIndices ResolveExternalReferenceColumns(Dictionary<string, int> headerMap)
    {
        var settings = new SettingsXmlLoader().LoadOrCreate();
        var resolver = new ColumnResolver(settings);
        var columns = resolver.Resolve(headerMap);

        int Require(string key)
        {
            if (columns.TryGetValue(key, out var idx))
            {
                return idx;
            }

            throw new InvalidOperationException($"필수 헤더를 찾을 수 없습니다: {key} (setting.xml 확인)");
        }

        int? Optional(string key) => columns.TryGetValue(key, out var idx) ? idx : null;

        return new ExternalReferenceColumnIndices(
            IdShort: Require("CD_IdShort"),
            Category: Optional("CD_Category"),
            DescriptionLanguage: Optional("CD_DescriptionLanguage"),
            Description: Optional("CD_Description"),
            IdentifiableId: Optional("CD_IdentifiableId"),
            IsCaseOf: Optional("CD_IsCaseOf"));
    }

    private static bool IsEmptyRow(Func<int, string?> getValue, params int[] columns)
    {
        foreach (var column in columns)
        {
            if (!string.IsNullOrWhiteSpace(getValue(column)))
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveCollectionValue(
        IReadOnlyDictionary<string, int> headerMap,
        Func<int, string?> readValue,
        int fallbackCollectionColumn)
    {
        var multiColumns = headerMap
            .Where(item => IsCollectionLevelColumn(item.Key))
            .OrderBy(item => ExtractTrailingNumber(item.Key))
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Value)
            .ToList();

        if (multiColumns.Count > 0)
        {
            var segments = multiColumns
                .Select(index => (readValue(index) ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (segments.Count > 0)
            {
                return string.Join(">", segments);
            }
        }

        return (readValue(fallbackCollectionColumn) ?? string.Empty).Trim();
    }

    private static bool IsCollectionLevelColumn(string normalizedHeader)
    {
        var header = normalizedHeader.Trim().ToLowerInvariant();
        return Regex.IsMatch(header, "^(submodelcollection|collection)\\d+$");
    }

    private static int ExtractTrailingNumber(string normalizedHeader)
    {
        var match = Regex.Match(normalizedHeader, "(\\d+)$");
        return match.Success && int.TryParse(match.Value, out var number) ? number : int.MaxValue;
    }

    private static bool IsExternalReferenceAllToken(string value)
    {
        return ExternalReferenceAllTokens.Any(token => string.Equals(token, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitMultiValues(string input)
    {
        return (input ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static List<IXLWorksheet> ResolveRequestedSheets(XLWorkbook workbook, List<string> requestedSheets, SpecDiagnostics diag, string file)
    {
        var result = new List<IXLWorksheet>();
        foreach (var requested in requestedSheets)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault(ws => string.Equals(ws.Name, requested, StringComparison.OrdinalIgnoreCase));
            if (worksheet is null)
            {
                diag.ExternalReferenceIssues.Add($"외부참조 시트 없음: 파일={file}, 시트={requested}");
                continue;
            }

            result.Add(worksheet);
        }

        return result;
    }

    private static bool HasExternalReferenceHeader(IXLWorksheet worksheet)
    {
        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return false;
        }

        var headerMap = headerRow.CellsUsed()
            .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
            .ToDictionary(c => ColumnResolver.NormalizeHeaderKey(c.GetString()), c => c.Address.ColumnNumber);

        try
        {
            var columns = ResolveExternalReferenceColumns(headerMap);
            return columns.IdShort > 0;
        }
        catch
        {
            return false;
        }
    }

    private static List<ExternalReferenceRow> ReadExternalReferenceRowsFromWorksheet(IXLWorksheet ws, string sourceFilePath, SpecDiagnostics diag)
    {
        var headerRow = ws.FirstRowUsed() ?? throw new InvalidOperationException("헤더 행을 찾을 수 없습니다.");
        var headerMap = headerRow.CellsUsed()
            .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
            .ToDictionary(c => ColumnResolver.NormalizeHeaderKey(c.GetString()), c => c.Address.ColumnNumber);

        var columns = ResolveExternalReferenceColumns(headerMap);
        var list = new List<ExternalReferenceRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var idShort = row.Cell(columns.IdShort).GetString().Trim();
            if (string.IsNullOrWhiteSpace(idShort))
            {
                diag.ExternalReferenceIssues.Add($"외부참조 시트 행 스킵: IdShort 누락 (시트={ws.Name}, 행 {row.RowNumber()}).");
                continue;
            }

            var sourceKey = $"{Path.GetFileName(sourceFilePath)}::{ws.Name}::{row.RowNumber()}";
            list.Add(new ExternalReferenceRow(
                IdShort: idShort,
                Category: columns.Category.HasValue ? row.Cell(columns.Category.Value).GetString().Trim() : string.Empty,
                DescriptionLanguage: columns.DescriptionLanguage.HasValue ? row.Cell(columns.DescriptionLanguage.Value).GetString().Trim() : string.Empty,
                Description: columns.Description.HasValue ? row.Cell(columns.Description.Value).GetString().Trim() : string.Empty,
                IdentifiableId: columns.IdentifiableId.HasValue ? row.Cell(columns.IdentifiableId.Value).GetString().Trim() : string.Empty,
                IsCaseOf: columns.IsCaseOf.HasValue ? row.Cell(columns.IsCaseOf.Value).GetString().Trim() : string.Empty,
                SourceKey: sourceKey));
        }

        return list;
    }

    private readonly record struct ColumnIndices(
        int Asset,
        int Submodel,
        int SubmodelCollection,
        int PropertyKor,
        int PropertyEng,
        int PropertyType,
        int Value,
        int? Uom,
        int? Category,
        int? ReferenceData)
    {
        public int[] Required => new[] { Asset, Submodel, SubmodelCollection, PropertyKor, PropertyEng, PropertyType, Value };
    }

    private readonly record struct ExternalReferenceColumnIndices(
        int IdShort,
        int? Category,
        int? DescriptionLanguage,
        int? Description,
        int? IdentifiableId,
        int? IsCaseOf);
}
