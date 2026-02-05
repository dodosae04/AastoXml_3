using System.Globalization;
using System.Windows;
using AasExcelToXml.Wpf.Services;

namespace AasExcelToXml.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = SettingsService.Load();
        ApplyCulture(settings.Language);
    }

    private static void ApplyCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        var culture = new CultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        LocalizationService.Instance.SetCulture(language);
    }
}
