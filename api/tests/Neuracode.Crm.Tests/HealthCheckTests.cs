using System.Net;
using FluentAssertions;

namespace Neuracode.Crm.Tests;

public class HealthCheckTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }
}
