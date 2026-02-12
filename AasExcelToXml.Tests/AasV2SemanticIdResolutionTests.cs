using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class AasV2SemanticIdResolutionTests
{
    [Fact]
    public void Write_WhenSemanticIdMatchesConceptDescription_UsesLocalConceptDescriptionReference()
    {
        var spec = new AasEnvironmentSpec(
            new List<AasSpec>
            {
                new("Robot", "Robot", new List<SubmodelSpec>
                {
                    new("Operational", "Operational", new List<ElementSpec>
                    {
                        new(string.Empty, "Payload", "Payload", ElementKind.Property, "string", "5", string.Empty, string.Empty, "urn:cd:payload", null)
                    })
                })
            },
            new List<ConceptDescriptionSpec>
            {
                new("CD_Payload", "urn:cd:payload", "CONSTANT", new List<LangStringSpec>())
            });

        var writer = new AasV2XmlWriter(new ConvertOptions { Version = AasVersion.Aas2_0 }, new SpecDiagnostics(), new DocumentIdGenerator(1));
        var doc = writer.Write(spec);

        var semanticKey = doc.Descendants().First(e => e.Name.LocalName == "semanticId")
            .Descendants().First(e => e.Name.LocalName == "key");

        Assert.Equal("ConceptDescription", semanticKey.Attribute("type")?.Value);
        Assert.Equal("true", semanticKey.Attribute("local")?.Value);
        Assert.Equal("IRI", semanticKey.Attribute("idType")?.Value);
        Assert.Equal("urn:cd:payload", semanticKey.Value);
    }

    [Fact]
    public void Write_WhenSemanticIdNotInConceptDescriptions_KeepsGlobalReference()
    {
        var spec = new AasEnvironmentSpec(
            new List<AasSpec>
            {
                new("Robot", "Robot", new List<SubmodelSpec>
                {
                    new("Operational", "Operational", new List<ElementSpec>
                    {
                        new(string.Empty, "Payload", "Payload", ElementKind.Property, "string", "5", string.Empty, string.Empty, "urn:external:payload", null)
                    })
                })
            },
            new List<ConceptDescriptionSpec>
            {
                new("CD_Payload", "urn:cd:payload", "CONSTANT", new List<LangStringSpec>())
            });

        var writer = new AasV2XmlWriter(new ConvertOptions { Version = AasVersion.Aas2_0 }, new SpecDiagnostics(), new DocumentIdGenerator(1));
        var doc = writer.Write(spec);

        var semanticKey = doc.Descendants().First(e => e.Name.LocalName == "semanticId")
            .Descendants().First(e => e.Name.LocalName == "key");

        Assert.Equal("GlobalReference", semanticKey.Attribute("type")?.Value);
        Assert.Equal("false", semanticKey.Attribute("local")?.Value);
    }
}
