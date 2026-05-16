using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Neuracode.Crm.Api.Data.Entities;

namespace Neuracode.Crm.Tests.ContractTests;

public class WhatsAppFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["META_VERIFY_TOKEN"] = "test-verify-token"
            }));
    }
}

public class WhatsAppWebhookContractTests(WhatsAppFactory factory) : IClassFixture<WhatsAppFactory>
{
    private static StringContent InboundPayload(string waId, string wamid, string name, string body)
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
                                    new
                                    {
                                        from = waId,
                                        id = wamid,
                                        timestamp = "1746820800",
                                        text = new { body },
                                        type = "text"
                                    }
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

    [Fact]
    public async Task GetVerify_CorrectToken_ReturnsChallengeText()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync(
            "/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=test-verify-token&hub.challenge=abc123");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Be("abc123");
    }

    [Fact]
    public async Task GetVerify_WrongToken_Returns403()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync(
            "/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=WRONG&hub.challenge=abc123");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostInbound_ValidPayload_Returns200()
    {
        var client = factory.CreateClient();
        var waId = "5100" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.ok." + Guid.NewGuid().ToString("N");

        var resp = await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid, "Juan Pérez", "Hola, info"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostInbound_NewContact_CreatesContactWithWhatsAppSource()
    {
        var client = factory.CreateClient();
        var waId = "5198" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.new." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid, "Lead WA", "Consulta"));

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        contacts.Should().Contain(c => c.GetProperty("source").GetString() == "whatsapp");
    }

    [Fact]
    public async Task PostInbound_NewContact_HasHotTemperatureAndWaId()
    {
        var client = factory.CreateClient();
        var waId = "5197" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.hot." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid, "Lead Caliente", "Urgente"));

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contact = contacts!.FirstOrDefault(c =>
            c.TryGetProperty("waId", out var w) && w.GetString() == waId);

        contact.Should().NotBeNull("contact must be created with the given wa_id");
        contact!.GetProperty("temperature").GetString().Should().Be("hot");
    }

    [Fact]
    public async Task PostInbound_DuplicateWamid_Returns200AndCreatesOnlyOneContact()
    {
        var client = factory.CreateClient();
        var waId = "5299" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.dup." + Guid.NewGuid().ToString("N");

        var resp1 = await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid, "Dup Lead", "Primer envío"));
        var resp2 = await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid, "Dup Lead", "Primer envío"));

        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        contacts!.Where(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .Should().HaveCount(1, "duplicate wamid must not create a second contact");
    }

    [Fact]
    public async Task PostInbound_SameWaId_TwoMessages_CreatesOneContactTwoActivities()
    {
        var client = factory.CreateClient();
        var waId = "5388" + Guid.NewGuid().ToString("N")[..8];
        var wamid1 = "wamid.m1." + Guid.NewGuid().ToString("N");
        var wamid2 = "wamid.m2." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid1, "User A", "Primer mensaje"));
        await client.PostAsync("/api/webhooks/whatsapp",
            InboundPayload(waId, wamid2, "User A", "Segundo mensaje"));

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var matched = contacts!.Where(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId).ToList();
        matched.Should().HaveCount(1, "same wa_id maps to one contact");

        var contactId = matched[0].GetProperty("id").GetString()!;
        var acts = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        acts!.Where(a => a.GetProperty("type").GetString() == "whatsapp_inbound")
            .Should().HaveCount(2);
    }
}

public class WhatsAppSendContractTests(WhatsAppFactory factory) : IClassFixture<WhatsAppFactory>
{
    private async Task<string> CreateContactWithWaId(HttpClient client, WhatsAppFactory f, string waId)
    {
        var wamid = "wamid.seed." + Guid.NewGuid().ToString("N");
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
                                contacts = new[] { new { profile = new { name = "Test User" }, wa_id = waId } },
                                messages = new[]
                                {
                                    new { from = waId, id = wamid, timestamp = "1746820800", text = new { body = "Hola" }, type = "text" }
                                }
                            },
                            field = "messages"
                        }
                    }
                }
            }
        };
        await client.PostAsync("/api/webhooks/whatsapp",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        return contacts!.First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task PostSend_ContactNotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/api/contacts/nonexistent-id/whatsapp/send",
            new { templateName = "crm_bienvenida" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSend_ContactWithoutWaId_Returns400()
    {
        var client = factory.CreateClient();
        // Create contact without wa_id via standard endpoint
        var createResp = await client.PostAsJsonAsync("/api/contacts",
            new { name = "No WA Contact", email = "nowa@test.com" });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var contactId = created.GetProperty("id").GetString()!;

        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contactId}/whatsapp/send",
            new { templateName = "crm_bienvenida" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("wa_id");
    }

    [Fact]
    public async Task PostSend_OptedOutContact_Returns400()
    {
        var client = factory.CreateClient();
        var waId = "5499" + Guid.NewGuid().ToString("N")[..8];
        var contactId = await CreateContactWithWaId(client, factory, waId);

        // Mark opted out
        await factory.SeedAsync(async db =>
        {
            var c = await db.Contacts.FindAsync(contactId);
            c!.OptedOut = true;
            await db.SaveChangesAsync();
        });

        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contactId}/whatsapp/send",
            new { templateName = "crm_bienvenida" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("opt-out");
    }

    [Fact]
    public async Task PostSend_MissingTemplateName_Returns400()
    {
        var client = factory.CreateClient();
        var waId = "5599" + Guid.NewGuid().ToString("N")[..8];
        var contactId = await CreateContactWithWaId(client, factory, waId);

        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contactId}/whatsapp/send",
            new { templateName = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSend_MetaNotConfigured_Returns503()
    {
        var client = factory.CreateClient();
        var waId = "5699" + Guid.NewGuid().ToString("N")[..8];
        var contactId = await CreateContactWithWaId(client, factory, waId);

        // META_ACCESS_TOKEN not set in WhatsAppFactory → service throws → 503
        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contactId}/whatsapp/send",
            new { templateName = "crm_seguimiento", parameters = new[] { "Juan", "Agente", "Neuracode", "10/05/2026" } });

        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
