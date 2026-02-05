using System.Globalization;
using System.Resources;

namespace AasExcelToXml.Wpf.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManagerInstance = new("AasExcelToXml.Wpf.Resources.Strings", typeof(Strings).Assembly);

    public static ResourceManager ResourceManager => ResourceManagerInstance;

    public static CultureInfo? Culture { get; set; }
}
