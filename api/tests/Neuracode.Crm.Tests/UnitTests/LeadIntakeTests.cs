using Neuracode.Crm.Api.Domain;
using FluentAssertions;

namespace Neuracode.Crm.Tests.UnitTests;

public class LeadIntakeTests
{
    [Fact]
    public void Build_ReturnsContactWithDraftFields()
    {
        var draft = new LeadDraft("Ana Torres", "ana@test.com", "+52 5555", "ACME", "webhook");

        var (contact, _) = LeadIntake.Build(draft, "webhook lead");

        contact.Name.Should().Be("Ana Torres");
        contact.Email.Should().Be("ana@test.com");
        contact.Phone.Should().Be("+52 5555");
        contact.Company.Should().Be("ACME");
        contact.Source.Should().Be("webhook");
        contact.Temperature.Should().Be("cold");
        contact.Score.Should().Be(0);
    }

    [Fact]
    public void Build_ReturnsActivityLinkedToContact()
    {
        var draft = new LeadDraft("Pedro López", Source: "import");

        var (contact, activity) = LeadIntake.Build(draft, "Contacto importado");

        activity.ContactId.Should().Be(contact.Id);
        activity.Description.Should().Be("Contacto importado");
        activity.Type.Should().Be("note");
    }

    [Fact]
    public void Build_AssignsUniqueIds()
    {
        var draft = new LeadDraft("Test");

        var (a, _) = LeadIntake.Build(draft, "note");
        var (b, _) = LeadIntake.Build(draft, "note");

        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void Build_SetsCreatedAtAndUpdatedAt()
    {
        var before = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
        var draft = new LeadDraft("Test");

        var (contact, activity) = LeadIntake.Build(draft, "note");

        var after = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1;
        contact.CreatedAt.Should().BeInRange(before, after);
        contact.UpdatedAt.Should().Be(contact.CreatedAt);
        activity.CreatedAt.Should().BeInRange(before, after);
    }

    [Fact]
    public void Build_DefaultTemperatureAndScore()
    {
        var draft = new LeadDraft("Min Draft");

        var (contact, _) = LeadIntake.Build(draft, "x");

        contact.Temperature.Should().Be("cold");
        contact.Score.Should().Be(0);
    }
}
