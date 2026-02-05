using System.Linq;
using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class Aas3WriterRegressionTests
{
    [Fact]
    public void Write_Aas3_Does_Not_Emit_Aas2_Collection_Flags_Or_MimeType()
    {
        var spec = new AasEnvironmentSpec(
            new List<AasSpec>
            {
                new("SampleAas", "SampleAas", new List<SubmodelSpec>
                {
                    new("RegularSubmodel", "RegularSubmodel", new List<ElementSpec>
                    {
                        new("GroupA", "GroupedProp", "Grouped", ElementKind.Property, "string", "value", string.Empty, null, null)
                    }),
                    new("Documentation", "Documentation", new List<ElementSpec>())
                })
            });

        var diagnostics = new SpecDiagnostics();
        var writer = new AasV3XmlWriter(new ConvertOptions { Version = AasVersion.Aas3_0 }, diagnostics, new DocumentIdGenerator(64879470));
        var doc = writer.Write(spec);

        var aasNs = (XNamespace)"https://admin-shell.io/aas/3/0";
        var collectionFlagElements = doc
            .Descendants(aasNs + "submodelElementCollection")
            .SelectMany(collection => collection.Descendants(aasNs + "ordered")
                .Concat(collection.Descendants(aasNs + "allowDuplicates")))
            .ToList();

        Assert.Empty(collectionFlagElements);
        Assert.Empty(doc.Descendants(aasNs + "mimeType"));
        Assert.NotEmpty(doc.Descendants(aasNs + "contentType"));
    }

    [Fact]
    public void Write_Aas3_Relationships_Do_Not_Nest_Reference_Elements()
    {
        var spec = new AasEnvironmentSpec(
            new List<AasSpec>
            {
                new("SampleAas", "SampleAas", new List<SubmodelSpec>
                {
                    new("RegularSubmodel", "RegularSubmodel", new List<ElementSpec>
                    {
                        new(string.Empty, "Ent_FirstEntity", "First Entity", ElementKind.Entity, "string", string.Empty, string.Empty, null, null),
                        new(string.Empty, "Ent_SecondEntity", "Second Entity", ElementKind.Entity, "string", string.Empty, string.Empty, null, null),
                        new(string.Empty, "Rel_First_to_Second", "Rel", ElementKind.Relationship, "string", string.Empty, string.Empty, null,
                            new RelationshipSpec("Ent_FirstEntity", "Ent_SecondEntity"))
                    })
                })
            });

        var diagnostics = new SpecDiagnostics();
        var writer = new AasV3XmlWriter(new ConvertOptions { Version = AasVersion.Aas3_0 }, diagnostics, new DocumentIdGenerator(64879470));
        var doc = writer.Write(spec);

        var aasNs = (XNamespace)"https://admin-shell.io/aas/3/0";
        var hasNestedReference = doc
            .Descendants(aasNs + "relationshipElement")
            .SelectMany(element => element.Elements().Where(e => e.Name == aasNs + "first" || e.Name == aasNs + "second"))
            .Any(target => target.Elements(aasNs + "reference").Any());

        Assert.False(hasNestedReference);
    }
}
