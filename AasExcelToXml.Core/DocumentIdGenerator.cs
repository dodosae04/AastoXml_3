namespace AasExcelToXml.Core;

public sealed class DocumentIdGenerator
{
    private static readonly DocumentIdGenerator FallbackGenerator = new(64879470);
    private long _nextId;

    public DocumentIdGenerator(long seed)
    {
        _nextId = seed;
    }

    public string NextId()
    {
        var current = _nextId;
        _nextId++;
        return current.ToString("00000000");
    }

    public static string? Create(string? name, string? type, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(type)
            && string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return FallbackGenerator.NextId();
    }
}
