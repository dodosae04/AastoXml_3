using System;
using System.Windows.Forms;

namespace AasExcelToXml.Gui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var settings = SettingsStore.Load();
        I18n.SetCulture(settings.Language);
        Application.Run(new MainForm(settings));
    }
}
