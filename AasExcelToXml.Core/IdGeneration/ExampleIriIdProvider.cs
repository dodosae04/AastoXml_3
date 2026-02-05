using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace AasExcelToXml.Core.IdGeneration;

public sealed class ExampleIriIdProvider : IIdProvider
{
    private readonly string _baseIri;
    private readonly ExampleIriDigitsMode _digitsMode;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);

    public ExampleIriIdProvider(string baseIri, ExampleIriDigitsMode digitsMode)
    {
        _baseIri = string.IsNullOrWhiteSpace(baseIri) ? "https://example.com/ids" : baseIri.TrimEnd('/');
        _digitsMode = digitsMode;
    }

    public string GetAssetId(string aasIdShort)
    {
        return GetOrCreate($"asset:{aasIdShort}", seed =>
        {
            var digits = GetDigits(seed, 16);
            return $"{_baseIri}/asset/{FormatDigits(digits)}";
        });
    }

    public string GetSubmodelId(string aasIdShort, string submodelIdShort)
    {
        return GetOrCreate($"sm:{aasIdShort}:{submodelIdShort}", seed =>
        {
            var digits = GetDigits(seed, 16);
            return $"{_baseIri}/sm/{FormatDigits(digits)}";
        });
    }

    public string GetShellId(string aasIdShort)
    {
        return GetOrCreate($"shell:{aasIdShort}", seed =>
        {
            var hex = TakeHex(seed, 8);
            return $"AssetAdministrationShell---{hex}";
        });
    }

    public string GetConceptDescriptionId(string idShort)
    {
        return GetOrCreate($"concept:{idShort}", seed =>
        {
            var hex = TakeHex(seed, 8);
            return $"ConceptDescription---{hex}";
        });
    }

    private string GetOrCreate(string seed, Func<string, string> generator)
    {
        if (_cache.TryGetValue(seed, out var cached))
        {
            return cached;
        }

        var suffix = 0;
        while (true)
        {
            var attemptSeed = suffix == 0 ? seed : $"{seed}#{suffix}";
            var id = generator(attemptSeed);
            if (_usedIds.Add(id))
            {
                _cache[seed] = id;
                return id;
            }

            suffix++;
        }
    }

    private static string TakeHex(string seed, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var hex = Convert.ToHexString(bytes);
        return hex.Substring(0, length);
    }

    private static string TakeDecimalDigits(string seed, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var number = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var digits = number.ToString();
        if (digits.Length < length)
        {
            digits = digits.PadLeft(length, '0');
        }

        return digits.Substring(0, length);
    }

    private string GetDigits(string seed, int length)
    {
        return _digitsMode == ExampleIriDigitsMode.RandomSecure
            ? CreateRandomDigits(length)
            : TakeDecimalDigits(seed, length);
    }

    private static string CreateRandomDigits(int length)
    {
        Span<byte> buffer = stackalloc byte[8];
        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            var value = BitConverter.ToUInt64(buffer);
            var digits = value.ToString().PadLeft(length, '0');
            if (digits.Length > length)
            {
                digits = digits.Substring(0, length);
            }

            if (!string.IsNullOrWhiteSpace(digits))
            {
                return digits;
            }
        }
    }

    private static string FormatDigits(string digits)
    {
        return $"{digits.Substring(0, 4)}_{digits.Substring(4, 4)}_{digits.Substring(8, 4)}_{digits.Substring(12, 4)}";
    }
}
