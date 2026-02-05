using System.ComponentModel;
using System.Globalization;
using System.Resources;
using AasExcelToXml.Wpf.Resources;

namespace AasExcelToXml.Wpf.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager = Strings.ResourceManager;
    private CultureInfo _culture = new("ko-KR");

    public static LocalizationService Instance { get; } = new();

    public CultureInfo CurrentCulture => _culture;

    public string this[string key] => _resourceManager.GetString(key, _culture) ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetCulture(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return;
        }

        var culture = new CultureInfo(cultureName);
        if (Equals(_culture, culture))
        {
            return;
        }

        _culture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
