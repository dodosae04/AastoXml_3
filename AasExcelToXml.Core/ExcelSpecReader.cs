using System.Globalization;
using System.Threading;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace AasExcelToXml.Core;

public static class ExcelSpecReader
{
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

            list.Add(new SpecRow(
                Aas: row.Cell(columns.Asset).GetString().Trim(),
                Submodel: row.Cell(columns.Submodel).GetString().Trim(),
                Collection: row.Cell(columns.SubmodelCollection).GetString().Trim(),
                PropKor: row.Cell(columns.PropertyKor).GetString().Trim(),
                PropEng: row.Cell(columns.PropertyEng).GetString().Trim(),
                PropType: row.Cell(columns.PropertyType).GetString().Trim(),
                Value: row.Cell(columns.Value).GetString().Trim(),
                Uom: columns.Uom.HasValue ? row.Cell(columns.Uom.Value).GetString().Trim() : string.Empty,
                Category: columns.Category.HasValue ? row.Cell(columns.Category.Value).GetString().Trim() : string.Empty
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

            list.Add(new SpecRow(
                Aas: csv.GetField(columns.Asset)?.Trim() ?? string.Empty,
                Submodel: csv.GetField(columns.Submodel)?.Trim() ?? string.Empty,
                Collection: csv.GetField(columns.SubmodelCollection)?.Trim() ?? string.Empty,
                PropKor: csv.GetField(columns.PropertyKor)?.Trim() ?? string.Empty,
                PropEng: csv.GetField(columns.PropertyEng)?.Trim() ?? string.Empty,
                PropType: csv.GetField(columns.PropertyType)?.Trim() ?? string.Empty,
                Value: csv.GetField(columns.Value)?.Trim() ?? string.Empty,
                Uom: columns.Uom.HasValue ? (csv.GetField(columns.Uom.Value)?.Trim() ?? string.Empty) : string.Empty,
                Category: columns.Category.HasValue ? (csv.GetField(columns.Category.Value)?.Trim() ?? string.Empty) : string.Empty
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
            Category: Optional("Category"));
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

    private readonly record struct ColumnIndices(
        int Asset,
        int Submodel,
        int SubmodelCollection,
        int PropertyKor,
        int PropertyEng,
        int PropertyType,
        int Value,
        int? Uom,
        int? Category)
    {
        public int[] Required => new[] { Asset, Submodel, SubmodelCollection, PropertyKor, PropertyEng, PropertyType, Value };
    }
}
