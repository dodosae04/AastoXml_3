using System.Xml.Linq;
using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class Aas3NamespaceTests
{
    [Fact]
    public void Write_Aas3_Uses_Default_Namespace_Without_Prefix()
    {
        var spec = new AasEnvironmentSpec(
            new List<AasSpec>
            {
                new("SampleAas", "SampleAas", new List<SubmodelSpec>
                {
                    new("SampleSubmodel", "SampleSubmodel", new List<ElementSpec>
                    {
                        new(string.Empty, "TestProp", "테스트", ElementKind.Property, "string", "value", string.Empty, null, null)
                    })
                })
            });

        var diagnostics = new SpecDiagnostics();
        var writer = new AasV3XmlWriter(new ConvertOptions { Version = AasVersion.Aas3_0 }, diagnostics, new DocumentIdGenerator(64879470));
        var doc = writer.Write(spec);
        var xml = doc.ToString(SaveOptions.DisableFormatting);

        Assert.DoesNotContain("<aas:", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("xmlns:aas", xml, StringComparison.Ordinal);
        Assert.Contains("xmlns=\"https://admin-shell.io/aas/3/0\"", xml, StringComparison.Ordinal);
    }
}
