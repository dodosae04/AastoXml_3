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
            new("Break_list", "Operational Info", string.Empty, "Spec", "Spec", "string", "1", string.Empty, string.Empty),
            new("Robot_body", "Operational Info", string.Empty, "Brake list", "Ent_Brake_list", "Entity", "[Reference] Brake_list", string.Empty, string.Empty)
        };

        _ = SpecGrouper.BuildEnvironmentSpec(rows, out var diagnostics);

        Assert.Empty(diagnostics.MissingEntityReferences);
    }


    [Fact]
    public void BuildEnvironmentSpec_EntityReference_UsesDistanceTwoOnlyForUniqueAasCandidate()
    {
        var rows = new List<SpecRow>
        {
            new("Break_list", "Operational Info", string.Empty, "Spec", "Spec", "string", "1", string.Empty, string.Empty),
            new("Robot_body", "Assembly", string.Empty, "브레이크", "Ent_Brake_list", "Entity", "[Reference] Brake_list", string.Empty, string.Empty)
        };

        _ = SpecGrouper.BuildEnvironmentSpec(rows, out var diagnostics);

        Assert.Empty(diagnostics.MissingEntityReferences);
    }

    [Fact]
    public void BuildEnvironmentSpec_RelationshipPropType_Preserves_PropertyEng_As_IdShort()
    {
        var rows = new List<SpecRow>
        {
            new("Robot", "SM", string.Empty, "관계", "Rel_Robot_body", "Relationship", "[first] Ent_A\n[second] Ent_B", string.Empty, string.Empty)
        };

        var spec = SpecGrouper.BuildEnvironmentSpec(rows, out _);
        var relationship = spec.Assets.Single().Submodels.Single().Elements.Single();

        Assert.Equal(ElementKind.Relationship, relationship.Kind);
        Assert.Equal("Rel_Robot_body", relationship.IdShort);
    }

    [Fact]
    public void BuildEnvironmentSpec_EntityWithFirstSecond_Preserves_PropertyEng_As_IdShort()
    {
        var rows = new List<SpecRow>
        {
            new("Robot", "SM", string.Empty, "관계", "Rel_Robot_head", "Entity", "[first] Ent_A\n[second] Ent_B", string.Empty, string.Empty)
        };

        var spec = SpecGrouper.BuildEnvironmentSpec(rows, out _);
        var relationship = spec.Assets.Single().Submodels.Single().Elements.Single();

        Assert.Equal(ElementKind.Relationship, relationship.Kind);
        Assert.Equal("Rel_Robot_head", relationship.IdShort);
    }
}
