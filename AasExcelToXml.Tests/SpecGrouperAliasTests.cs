using AasExcelToXml.Core;
using Xunit;

namespace AasExcelToXml.Tests;

public sealed class SpecGrouperAliasTests
{
    [Fact]
    public void BuildEnvironmentSpec_Normalizes_Brake_List_References()
    {
        var rows = new List<SpecRow>
        {
            new("Break_list", "Operational Info", string.Empty, "Spec", "Spec", "string", "1", string.Empty),
            new("Robot_body", "Operational Info", string.Empty, "Brake list", "Ent_Brake_list", "Entity", "[Reference] Brake_list", string.Empty)
        };

        _ = SpecGrouper.BuildEnvironmentSpec(rows, out var diagnostics);

        Assert.Empty(diagnostics.MissingEntityReferences);
    }
}
