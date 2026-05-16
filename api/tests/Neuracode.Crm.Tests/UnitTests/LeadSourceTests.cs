using Neuracode.Crm.Api.Domain;
using FluentAssertions;

namespace Neuracode.Crm.Tests.UnitTests;

public class LeadSourceTests
{
    [Theory]
    [InlineData("web")]
    [InlineData("referral")]
    [InlineData("linkedin")]
    [InlineData("evento")]
    [InlineData("otro")]
    [InlineData("import")]
    [InlineData("webhook")]
    [InlineData("ads")]
    [InlineData("whatsapp")]
    public void IsValid_ReturnsTrue_ForCanonicalCodes(string code)
    {
        LeadSource.IsValid(code).Should().BeTrue();
    }

    [Theory]
    [InlineData("WEB")]
    [InlineData("Referral")]
    [InlineData("WEBHOOK")]
    public void IsValid_CaseInsensitive(string code)
    {
        LeadSource.IsValid(code).Should().BeTrue();
    }

    [Theory]
    [InlineData("website")]
    [InlineData("redes_sociales")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_ReturnsFalse_ForUnknownCodes(string? code)
    {
        LeadSource.IsValid(code).Should().BeFalse();
    }

    [Theory]
    [InlineData("web", "Sitio web")]
    [InlineData("referral", "Referido")]
    [InlineData("linkedin", "LinkedIn")]
    [InlineData("evento", "Evento")]
    [InlineData("otro", "Otro")]
    [InlineData("import", "Importado")]
    [InlineData("webhook", "Webhook")]
    [InlineData("ads", "Publicidad")]
    [InlineData("whatsapp", "WhatsApp")]
    public void GetLabel_ReturnsSpanishLabel_ForCanonicalCode(string code, string expected)
    {
        LeadSource.GetLabel(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("WEB", "Sitio web")]
    [InlineData("REFERRAL", "Referido")]
    public void GetLabel_CaseInsensitive(string code, string expected)
    {
        LeadSource.GetLabel(code).Should().Be(expected);
    }

    [Fact]
    public void GetLabel_ReturnsRawCode_WhenUnknown()
    {
        LeadSource.GetLabel("unknown").Should().Be("unknown");
    }

    [Fact]
    public void GetLabel_ReturnsEmpty_WhenNullOrEmpty()
    {
        LeadSource.GetLabel(null).Should().Be("");
        LeadSource.GetLabel("").Should().Be("");
    }

    [Fact]
    public void AllCanonicalCodes_HaveLabels()
    {
        foreach (var code in LeadSource.AllCodes)
            LeadSource.GetLabel(code).Should().NotBeNullOrEmpty($"code '{code}' must have a label");
    }

}
