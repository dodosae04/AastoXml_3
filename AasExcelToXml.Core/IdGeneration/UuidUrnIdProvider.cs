using System.Security.Cryptography;
using System.Text;

namespace AasExcelToXml.Core.IdGeneration;

public sealed class UuidUrnIdProvider : IIdProvider
{
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);

    public string GetAssetId(string aasIdShort)
    {
        return GetOrCreate($"asset:{aasIdShort}");
    }

    public string GetSubmodelId(string aasIdShort, string submodelIdShort)
    {
        return GetOrCreate($"submodel:{aasIdShort}:{submodelIdShort}");
    }

    public string GetShellId(string aasIdShort)
    {
        return GetOrCreate($"aas:{aasIdShort}");
    }

    public string GetConceptDescriptionId(string idShort)
    {
        return GetOrCreate($"concept:{idShort}");
    }

    private string GetOrCreate(string seed)
    {
        if (_cache.TryGetValue(seed, out var cached))
        {
            return cached;
        }

        var suffix = 0;
        while (true)
        {
            var attemptSeed = suffix == 0 ? seed : $"{seed}#{suffix}";
            var id = CreateStableId(attemptSeed);
            if (_usedIds.Add(id))
            {
                _cache[seed] = id;
                return id;
            }

            suffix++;
        }
    }

    private static string CreateStableId(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var guidBytes = new byte[16];
        Array.Copy(bytes, guidBytes, guidBytes.Length);
        return $"urn:uuid:{new Guid(guidBytes)}";
    }
}
