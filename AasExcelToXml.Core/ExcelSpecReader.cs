using System.Globalization;
using System.Threading;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace AasExcelToXml.Core;

public static class ExcelSpecReader
{
    public static List<SpecRow> ReadRows(string inputPath, string? sheetName = null)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"입력 파일을 찾을 수 없습니다: {inputPath}");
        }

        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        return extension switch
        {
            ".xlsx" => ReadXlsxRows(inputPath, sheetName ?? "사양시트"),
            ".csv" => ReadCsvRows(inputPath),
            _ => throw new InvalidOperationException($"지원하지 않는 입력 형식입니다: {extension}")
        };
    }

    private static List<SpecRow> ReadXlsxRows(string xlsxPath, string sheetName)
    {
        using var wb = OpenWorkbookWithRetry(xlsxPath);
        var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, sheetName, StringComparison.OrdinalIgnoreCase));
        if (ws is null)
        {
            var candidates = string.Join(", ", wb.Worksheets.Select(w => w.Name));
            throw new InvalidOperationException($"'{sheetName}' 시트를 찾을 수 없습니다. 사용 가능한 시트: {candidates}");
        }

        // 헤더 alias 규칙: 공백/언더스코어 제거 + 대소문자 무시로 비교하여 약간의 변형을 허용한다.
        var headerRow = ws.FirstRowUsed();
        if (headerRow is null)
        {
            throw new InvalidOperationException("헤더 행을 찾을 수 없습니다.");
        }

        var headerMap = headerRow.CellsUsed()
            .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
            .ToDictionary(
                c => NormalizeHeaderKey(c.GetString()),
                c => c.Address.ColumnNumber
            );

        var columns = ResolveColumns(headerMap);

        var list = new List<SpecRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            // 최소 필수 컬럼이 모두 비어 있으면 빈 행으로 간주하여 스킵
            if (IsEmptyRow(index => row.Cell(index).GetString(), columns.Required))
            {
                continue;
            }

            list.Add(new SpecRow(
                Aas: row.Cell(columns.Aas).GetString().Trim(),
                Submodel: row.Cell(columns.Submodel).GetString().Trim(),
                Collection: row.Cell(columns.Collection).GetString().Trim(),
                PropKor: row.Cell(columns.PropKor).GetString().Trim(),
                PropEng: row.Cell(columns.PropEng).GetString().Trim(),
                PropType: row.Cell(columns.PropType).GetString().Trim(),
                Value: row.Cell(columns.Value).GetString().Trim(),
                Uom: columns.Uom.HasValue ? row.Cell(columns.Uom.Value).GetString().Trim() : string.Empty
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
        // 헤더 alias 규칙: 공백/언더스코어 제거 + 대소문자 무시로 비교하여 약간의 변형을 허용한다.
        var headerMap = headers
            .Select((name, index) => new { name, index })
            .Where(item => !string.IsNullOrWhiteSpace(item.name))
            .ToDictionary(item => NormalizeHeaderKey(item.name), item => item.index);

        var columns = ResolveColumns(headerMap);

        var list = new List<SpecRow>();
        while (csv.Read())
        {
            // 최소 필수 컬럼이 모두 비어 있으면 빈 행으로 간주하여 스킵
            if (IsEmptyRow(index => csv.GetField(index), columns.Required))
            {
                continue;
            }

            list.Add(new SpecRow(
                Aas: csv.GetField(columns.Aas)?.Trim() ?? string.Empty,
                Submodel: csv.GetField(columns.Submodel)?.Trim() ?? string.Empty,
                Collection: csv.GetField(columns.Collection)?.Trim() ?? string.Empty,
                PropKor: csv.GetField(columns.PropKor)?.Trim() ?? string.Empty,
                PropEng: csv.GetField(columns.PropEng)?.Trim() ?? string.Empty,
                PropType: csv.GetField(columns.PropType)?.Trim() ?? string.Empty,
                Value: csv.GetField(columns.Value)?.Trim() ?? string.Empty,
                Uom: columns.Uom.HasValue ? (csv.GetField(columns.Uom.Value)?.Trim() ?? string.Empty) : string.Empty
            ));
        }

        return list;
    }

    private static ColumnIndices ResolveColumns(Dictionary<string, int> headerMap)
    {
        int Col(IEnumerable<string> aliases)
        {
            foreach (var alias in aliases)
            {
                var key = NormalizeHeaderKey(alias);
                if (headerMap.TryGetValue(key, out var idx))
                {
                    return idx;
                }
            }

            throw new InvalidOperationException($"필수 헤더를 찾을 수 없습니다. 후보: {string.Join(", ", aliases)}");
        }

        int? OptionalCol(IEnumerable<string> aliases)
        {
            foreach (var alias in aliases)
            {
                var key = NormalizeHeaderKey(alias);
                if (headerMap.TryGetValue(key, out var idx))
                {
                    return idx;
                }
            }

            return null;
        }

        var colAas = Col(new[] { "AAS", "Asset (AAS)", "Asset(AAS)", "Asset" });
        var colSm = Col(new[] { "Submodel", "Sub Model", "Sub-Model" });
        var colSmc = Col(new[] { "SubmodelCollection", "Submodel Collection", "Submodel_Collection", "Collection" });
        var colKor = Col(new[] { "Property_Kor", "Property(Kor)", "Property Kor", "Property_KR", "Property_ko", "Property_KO", "PropertyKOR" });
        var colEng = Col(new[] { "Property_Eng", "Property(Eng)", "Property Eng", "Property_EN", "Property_en", "PropertyENG" });
        var colType = Col(new[] { "Property type", "Property Type", "PropertyType", "Type", "Data Type" });
        var colVal = Col(new[] { "Value", "값", "Data" });
        var colUom = OptionalCol(new[] { "UOM", "UoM", "Unit", "Unit of Measure" });

        return new ColumnIndices(colAas, colSm, colSmc, colKor, colEng, colType, colVal, colUom);
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

    private static string NormalizeHeaderKey(string header)
    {
        // 헤더 정규화: 공백/언더스코어 제거 + 소문자화로 alias 비교를 단순화한다.
        return header.Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    private readonly record struct ColumnIndices(
        int Aas,
        int Submodel,
        int Collection,
        int PropKor,
        int PropEng,
        int PropType,
        int Value,
        int? Uom)
    {
        public int[] Required => new[] { Aas, Submodel, Collection, PropKor, PropEng, PropType, Value };
    }
}
