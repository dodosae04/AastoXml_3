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
        EnsureRequiredColumns(document, path);

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
                    DefaultColumns.Select(item => new XElement("Column",
                        new XAttribute("key", item.Key),
                        new XAttribute("aliases", item.Aliases))))));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        document.Save(path);
    }

    private static void EnsureRequiredColumns(XDocument document, string path)
    {
        var columnsElement = document.Root?.Element("Columns");
        if (columnsElement is null)
        {
            document.Root?.Add(new XElement("Columns"));
            columnsElement = document.Root?.Element("Columns");
        }

        if (columnsElement is null)
        {
            return;
        }

        var existingKeys = new HashSet<string>(
            columnsElement.Elements("Column")
                .Select(e => e.Attribute("key")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>(),
            StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var required in DefaultColumns)
        {
            if (existingKeys.Contains(required.Key))
            {
                continue;
            }

            columnsElement.Add(new XElement("Column",
                new XAttribute("key", required.Key),
                new XAttribute("aliases", required.Aliases)));
            changed = true;
        }

        if (changed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            document.Save(path);
        }
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

    private static readonly (string Key, string Aliases)[] DefaultColumns =
    {
        ("Asset", "Asset (AAS),Asset,AAS"),
        ("Submodel", "Submodel,서브모델"),
        ("SubmodelCollection", "SubmodelCollection,Collection,컬렉션"),
        ("PropertyKor", "Property_Kor,PropertyKor,한글속성"),
        ("PropertyEng", "Property_Eng,PropertyEng,영문속성"),
        ("PropertyType", "Property type,Type,데이터타입"),
        ("Value", "Value,값"),
        ("UOM", "UOM,Unit,단위"),
        ("Category", "Category,카테고리"),
        ("ReferenceData", "Reference_data,ReferenceData,ref_data,참조데이터,Reference"),
        ("CD_IdShort", "IdShort,CD_IdShort"),
        ("CD_Category", "Category,CD_Category"),
        ("CD_DescriptionLanguage", "Description Language,DescriptionLanguage,Lang,Language"),
        ("CD_Description", "Description,설명"),
        ("CD_IdentifiableId", "Identifiable ID,IdentifiableID,IdentifiableId,ID"),
        ("CD_IsCaseOf", "isCaseOf,IsCaseOf,isCaseof")
    };
}

public sealed record ColumnAlias(string Key, IReadOnlyList<string> Aliases);

public sealed record ColumnAliasSettings(string Path, IReadOnlyList<ColumnAlias> Columns);
