using FluentAssertions;
using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Tests.UnitTests;

public class AgentMemoryDataTests
{
    [Fact]
    public void Empty_HasBrowsingState()
    {
        AgentMemoryData.Empty.SalesState.Should().Be("browsing");
        AgentMemoryData.Empty.Name.Should().BeNull();
        AgentMemoryData.Empty.InterestedIn.Should().BeEmpty();
    }

    [Fact]
    public void MergeWith_NonNullFieldsOverwrite()
    {
        var existing = AgentMemoryData.Empty;
        var extracted = new AgentMemoryData("Juan", "S/. 200", ["BOSS"], "considering", null);

        var merged = existing.MergeWith(extracted);

        merged.Name.Should().Be("Juan");
        merged.Budget.Should().Be("S/. 200");
        merged.InterestedIn.Should().Contain("BOSS");
        merged.SalesState.Should().Be("considering");
        merged.Notes.Should().BeNull();
    }

    [Fact]
    public void MergeWith_NullFieldsPreserveExisting()
    {
        var existing = new AgentMemoryData("Maria", "S/. 300", ["METROPOLE"], "considering", "regalo cumple");
        var extracted = new AgentMemoryData(null, null, [], "considering", null);

        var merged = existing.MergeWith(extracted);

        merged.Name.Should().Be("Maria");
        merged.Budget.Should().Be("S/. 300");
        merged.InterestedIn.Should().Contain("METROPOLE");
        merged.Notes.Should().Be("regalo cumple");
    }

    [Fact]
    public void MergeWith_InterestedIn_MergesDistinct()
    {
        var existing = new AgentMemoryData(null, null, ["BOSS"], "browsing", null);
        var extracted = new AgentMemoryData(null, null, ["BOSS", "Combos Love"], "considering", null);

        var merged = existing.MergeWith(extracted);

        merged.InterestedIn.Should().Contain("BOSS").And.Contain("Combos Love");
        merged.InterestedIn.Distinct().Should().HaveCount(merged.InterestedIn.Count, "no duplicates");
    }

    [Fact]
    public void ToPromptString_IncludesNonNullFields()
    {
        var data = new AgentMemoryData("Juan", "S/. 200", ["BOSS"], "considering", "regalo");

        var prompt = data.ToPromptString();

        prompt.Should().Contain("Juan");
        prompt.Should().Contain("BOSS");
        prompt.Should().Contain("considering");
    }

    [Fact]
    public void ToPromptString_Empty_ReturnsEmptyOrMinimal()
    {
        var prompt = AgentMemoryData.Empty.ToPromptString();

        prompt.Should().NotBeNull();
        prompt.Length.Should().BeLessThan(100, "empty memory should not bloat the prompt");
    }

    [Fact]
    public void IsEmpty_TrueWhenAllFieldsDefault()
    {
        AgentMemoryData.Empty.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_FalseWhenNameSet()
    {
        var data = AgentMemoryData.Empty with { Name = "Juan" };
        data.IsEmpty.Should().BeFalse();
    }
}
