using System.Text.RegularExpressions;
using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class ConversionRegressionTests
{
    [Fact]
    public void Convert_Aas3_Uses_ExampleIri_And_Resolves_References_Without_Warnings()
    {
        var rows = new List<SpecRow>
        {
            new("Robot_body", "Operational Information", string.Empty, "Payload", "Payload", "string", "5kg", string.Empty),
            new("Airbalance_robot", "Operational Information", string.Empty, "Reference Payload", "[Robot_body Reference] Payload", "string", string.Empty, string.Empty),
            new("Airbalance_robot", "Operational Information", string.Empty, "Airbalance Robot", "Ent_Airbalance_robot", "Entity", string.Empty, string.Empty),
            new("Airbalance_robot", "Operational Information", string.Empty, "Brake list", "Ent_Brake_list", "Entity", string.Empty, string.Empty),
            new("Airbalance_robot", "Operational Information", string.Empty, "Rel", "Rel_Airbalance_to_Brake", "Relationship", "[first] Ent_Airbalance_robot [second] Ent_Brake_list", string.Empty)
        };

        var spec = SpecGrouper.BuildEnvironmentSpec(rows, out var diagnostics);
        var writer = new AasV3XmlWriter(new ConvertOptions { Version = AasVersion.Aas3_0 }, diagnostics, new DocumentIdGenerator(64879470));
        var document = writer.Write(spec);

        Assert.False(diagnostics.HasWarnings);

        var xml = document.ToString();
        Assert.DoesNotContain("urn:uuid:", xml, StringComparison.OrdinalIgnoreCase);

        var aasNs = (XNamespace)"https://admin-shell.io/aas/3/0";
        var referenceElement = document.Descendants(aasNs + "referenceElement").FirstOrDefault();
        Assert.NotNull(referenceElement);

        var keys = referenceElement!.Descendants(aasNs + "key").ToList();
        Assert.Equal(2, keys.Count);
        Assert.Equal("Submodel", GetKeyField(keys[0], "type"));
        Assert.Equal("Property", GetKeyField(keys[1], "type"));

        var entityKeys = document.Descendants(aasNs + "relationshipElement")
            .SelectMany(element => element.Descendants(aasNs + "key"))
            .Where(key => string.Equals(GetKeyField(key, "type"), "Entity", StringComparison.Ordinal))
            .Select(key => GetKeyField(key, "value"))
            .ToList();

        Assert.All(entityKeys, value => Assert.DoesNotMatch(new Regex("^Ent_", RegexOptions.IgnoreCase), value));
    }

    private static string GetKeyField(XElement keyElement, string name)
    {
        return keyElement.Attribute(name)?.Value
            ?? keyElement.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value
            ?? string.Empty;
    }
}
