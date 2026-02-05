using System.Windows.Markup;

namespace AasExcelToXml.Wpf.Services;

[MarkupExtensionReturnType(typeof(string))]
public class Loc : MarkupExtension
{
    public Loc() { }
    
    public Loc(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationService.Instance[Key];
    }
}
