using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Services;

namespace Neuracode.Crm.Tests.ContractTests;

// ── Fake agent — returns a fixed response string ─────────────────────────────

file sealed class RespondingAgentService : IAgentService
{
    public bool IsConfigured => true;
    public Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default) =>
        Task.FromResult<string?>("Hola, gracias por contactarnos. Un asesor te atenderá pronto.");
}

// ── Fake agent — always escalates (returns null) ──────────────────────────────

file sealed class EscalatingAgentService : IAgentService
{
    public bool IsConfigured => true;
    public Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default) => Task.FromResult<string?>(null);
}

// ── Fake WhatsApp service — always succeeds in tests ─────────────────────────

file sealed class FakeWhatsAppService : IWhatsAppService
{
    public bool IsConfigured => true;
    public Task<(bool Success, string? MessageId)> SendTextAsync(string toWaId, string text) =>
        Task.FromResult<(bool, string?)>((true, "wamid.fake." + Guid.NewGuid().ToString("N")));
    public Task<(bool Success, string? MessageId)> SendTemplateAsync(string toWaId, string templateName, string languageCode = "es", params string[] parameters) =>
        Task.FromResult<(bool, string?)>((true, "wamid.fake." + Guid.NewGuid().ToString("N")));
}

// ── Factories ─────────────────────────────────────────────────────────────────

public class RespondingAgentFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentService>();
            services.AddSingleton<IAgentService, RespondingAgentService>();
            services.RemoveAll<IWhatsAppService>();
            services.AddSingleton<IWhatsAppService, FakeWhatsAppService>();
        });
    }
}

public class EscalatingAgentFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentService>();
            services.AddSingleton<IAgentService, EscalatingAgentService>();
        });
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

file static class WaPayload
{
    public static StringContent Inbound(string waId, string wamid, string name, string body)
    {
        var payload = new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "1612237300105652",
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata = new { display_phone_number = "+51997055975", phone_number_id = "1003010076238371" },
                                contacts = new[] { new { profile = new { name }, wa_id = waId } },
                                messages = new[]
                                {
                                    new { from = waId, id = wamid, timestamp = "1746820800", text = new { body }, type = "text" }
                                }
                            },
                            field = "messages"
                        }
                    }
                }
            }
        };
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}

// ── Handoff + bot-resume contract tests ──────────────────────────────────────

public class AgentHandoffContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    private async Task<string> CreateWaContact(HttpClient client, string waId)
    {
        var wamid = "wamid.hof." + Guid.NewGuid().ToString("N");
        await client.PostAsync("/api/webhooks/whatsapp", WaPayload.Inbound(waId, wamid, "Test", "Hola"));
        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        return contacts!.First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task PostHandoff_WaContact_SetsBotHandlingFalse()
    {
        var client = factory.CreateClient();
        var waId = "5700" + Guid.NewGuid().ToString("N")[..8];
        var contactId = await CreateWaContact(client, waId);

        var resp = await client.PostAsync($"/api/contacts/{contactId}/handoff", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("botHandling").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PostHandoff_NonExistent_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsync($"/api/contacts/{Guid.NewGuid()}/handoff", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostBotResume_AfterHandoff_SetsBotHandlingTrue()
    {
        var client = factory.CreateClient();
        var waId = "5800" + Guid.NewGuid().ToString("N")[..8];
        var contactId = await CreateWaContact(client, waId);

        await client.PostAsync($"/api/contacts/{contactId}/handoff", null);
        var resp = await client.PostAsync($"/api/contacts/{contactId}/bot-resume", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("botHandling").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PostBotResume_NonExistent_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsync($"/api/contacts/{Guid.NewGuid()}/bot-resume", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NewWaContact_HasBotHandlingTrue()
    {
        var client = factory.CreateClient();
        var waId = "5900" + Guid.NewGuid().ToString("N")[..8];
        await CreateWaContact(client, waId);

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contact = contacts!.First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId);

        contact.GetProperty("botHandling").GetBoolean().Should().BeTrue();
    }
}

// ── Agent auto-response contract tests ───────────────────────────────────────

public class AgentRespondingContractTests(RespondingAgentFactory factory) : IClassFixture<RespondingAgentFactory>
{
    [Fact]
    public async Task PostInbound_BotHandlingTrue_AgentResponds_CreatesOutboundActivity()
    {
        var client = factory.CreateClient();
        var waId = "6000" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.agent." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp", WaPayload.Inbound(waId, wamid, "Cliente", "¿hacen envíos?"));

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contact = contacts!.First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId);
        var contactId = contact.GetProperty("id").GetString()!;

        var acts = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        acts!.Should().Contain(a => a.GetProperty("type").GetString() == "whatsapp_outbound",
            "agent responded → outbound activity must be recorded");
    }

    [Fact]
    public async Task PostInbound_AfterHandoff_BotHandlingFalse_NoOutboundActivity()
    {
        var client = factory.CreateClient();
        var waId = "6100" + Guid.NewGuid().ToString("N")[..8];
        var wamid1 = "wamid.hof1." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp", WaPayload.Inbound(waId, wamid1, "Cliente", "Hola"));
        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contactId = contacts!.First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .GetProperty("id").GetString()!;

        var actsBefore = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        var outboundBefore = actsBefore!.Count(a => a.GetProperty("type").GetString() == "whatsapp_outbound");

        await client.PostAsync($"/api/contacts/{contactId}/handoff", null);

        var wamid2 = "wamid.hof2." + Guid.NewGuid().ToString("N");
        await client.PostAsync("/api/webhooks/whatsapp", WaPayload.Inbound(waId, wamid2, "Cliente", "Sigo esperando"));

        var actsAfter = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        var outboundAfter = actsAfter!.Count(a => a.GetProperty("type").GetString() == "whatsapp_outbound");

        outboundAfter.Should().Be(outboundBefore, "after handoff, agent must not auto-respond to second message");
    }
}

public class AgentEscalatingContractTests(EscalatingAgentFactory factory) : IClassFixture<EscalatingAgentFactory>
{
    [Fact]
    public async Task PostInbound_AgentReturnsNull_NoOutboundActivity_Returns200()
    {
        var client = factory.CreateClient();
        var waId = "6200" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.esc." + Guid.NewGuid().ToString("N");

        var resp = await client.PostAsync("/api/webhooks/whatsapp",
            WaPayload.Inbound(waId, wamid, "Cliente", "quiero hablar con alguien"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contactId = contacts!.First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .GetProperty("id").GetString()!;

        var acts = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        acts!.Should().NotContain(a => a.GetProperty("type").GetString() == "whatsapp_outbound",
            "agent returned null → no auto-reply should be recorded");
    }
}
