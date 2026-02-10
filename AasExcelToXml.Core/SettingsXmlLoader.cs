using System.Xml.Linq;

namespace AasExcelToXml.Core;

public sealed class SettingsXmlLoader
{
    private const string FileName = "setting.xml";

    public ColumnAliasSettings LoadOrCreate()
    {
        var baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AasExcelToXml");
        var path = Path.Combine(baseDirectory, FileName);
        if (!File.Exists(path))
        {
            CreateDefault(path);
        }

        var document = XDocument.Load(path);
        var columns = document.Root?
            .Descendants("Column")
            .Select(ToAlias)
            .Where(item => item is not null)
            .Cast<ColumnAlias>()
            .ToList() ?? new List<ColumnAlias>();

        return new ColumnAliasSettings(path, columns);
    }

    private static void CreateDefault(string path)
    {
        var document = new XDocument(
            new XElement("Settings",
                new XElement("Columns",
                    new XElement("Column", new XAttribute("key", "Asset"), new XAttribute("aliases", "Asset (AAS),Asset,AAS")),
                    new XElement("Column", new XAttribute("key", "Submodel"), new XAttribute("aliases", "Submodel,서브모델")),
                    new XElement("Column", new XAttribute("key", "SubmodelCollection"), new XAttribute("aliases", "SubmodelCollection,Collection,컬렉션")),
                    new XElement("Column", new XAttribute("key", "PropertyKor"), new XAttribute("aliases", "Property_Kor,PropertyKor,한글속성")),
                    new XElement("Column", new XAttribute("key", "PropertyEng"), new XAttribute("aliases", "Property_Eng,PropertyEng,영문속성")),
                    new XElement("Column", new XAttribute("key", "PropertyType"), new XAttribute("aliases", "Property type,Type,데이터타입")),
                    new XElement("Column", new XAttribute("key", "Value"), new XAttribute("aliases", "Value,값")),
                    new XElement("Column", new XAttribute("key", "UOM"), new XAttribute("aliases", "UOM,Unit,단위")),
                    new XElement("Column", new XAttribute("key", "Category"), new XAttribute("aliases", "Category,카테고리"))
                )));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        document.Save(path);
    }

    private static ColumnAlias? ToAlias(XElement element)
    {
        var key = element.Attribute("key")?.Value?.Trim();
        var aliasesRaw = element.Attribute("aliases")?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var aliases = aliasesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new ColumnAlias(key, aliases);
    }
}

public sealed record ColumnAlias(string Key, IReadOnlyList<string> Aliases);

public sealed record ColumnAliasSettings(string Path, IReadOnlyList<ColumnAlias> Columns);
