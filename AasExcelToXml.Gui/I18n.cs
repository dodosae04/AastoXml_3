using System.Globalization;
using System.Resources;

namespace AasExcelToXml.Gui;

public static class I18n
{
    private static readonly ResourceManager ResourceManager = new("AasExcelToXml.Gui.Resources.Strings", typeof(I18n).Assembly);

    public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("ko-KR");

    public static void SetCulture(string? cultureName)
    {
        var name = string.IsNullOrWhiteSpace(cultureName) ? "ko-KR" : cultureName;
        CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = CurrentCulture;
        CultureInfo.CurrentCulture = CurrentCulture;
    }

    public static string T(string key)
    {
        return ResourceManager.GetString(key, CurrentCulture) ?? key;
    }
}
