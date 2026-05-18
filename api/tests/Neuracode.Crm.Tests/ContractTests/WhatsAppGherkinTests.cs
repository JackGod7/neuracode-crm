using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neuracode.Crm.Api.Services;

namespace Neuracode.Crm.Tests.ContractTests;

// ── Shared helpers ────────────────────────────────────────────────────────────

file static class GherkinAssert
{
    // Skips "S/. 40" via (?<![/\d]) — "/" precedes the dot in "S/."
    public static int SentenceCount(string text) =>
        Regex.Matches(text.Trim(), @"(?<![/\d])[.!?](?:\s|$)").Count;

    // Detects emoji: high surrogate pairs (U+10000+) or Misc Symbols U+2600–U+27FF
    public static bool ContainsEmoji(string text) =>
        text.Any(char.IsHighSurrogate) || text.Any(c => c >= '☀' && c <= '⟿');
}

file static class WaMsg
{
    public static StringContent Inbound(string waId, string wamid, string name, string body)
    {
        var p = new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "0", changes = new[] { new { value = new {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "+51997055975", phone_number_id = "0" },
                contacts = new[] { new { profile = new { name }, wa_id = waId } },
                messages = new[] { new { from = waId, id = wamid, timestamp = "1746820800", text = new { body }, type = "text" } }
            }, field = "messages" } } } }
        };
        return new StringContent(JsonSerializer.Serialize(p), Encoding.UTF8, "application/json");
    }
}

file sealed class CapturingWhatsAppService : IWhatsAppService
{
    public bool IsConfigured => true;
    public Task<(bool Success, string? MessageId)> SendTextAsync(string toWaId, string text) =>
        Task.FromResult<(bool, string?)>((true, "wamid.cap." + Guid.NewGuid().ToString("N")));
    public Task<(bool Success, string? MessageId)> SendTemplateAsync(string toWaId, string templateName, string languageCode = "es", params string[] parameters) =>
        Task.FromResult<(bool, string?)>((true, null));
}

file sealed class SlowAgentService : IAgentService
{
    public bool IsConfigured => true;
    public async Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default)
    {
        try { await Task.Delay(10_000, ct); }
        catch (OperationCanceledException) { /* expected: timeout cancels this */ }
        return null;
    }
}

// ── Factories ─────────────────────────────────────────────────────────────────

public class TimeoutAgentFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(cfg =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
                { ["AGENT_TIMEOUT_SECONDS"] = "1" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentService>();
            services.AddSingleton<IAgentService, SlowAgentService>();
            services.RemoveAll<IWhatsAppService>();
            services.AddSingleton<IWhatsAppService, CapturingWhatsAppService>();
        });
    }
}

public class RealAgentFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(cfg =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "",
                ["AGENT_TIMEOUT_SECONDS"] = "15"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWhatsAppService>();
            services.AddSingleton<IWhatsAppService, CapturingWhatsAppService>();
        });
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// CONTRACT TESTS — always run, no API key required
// ══════════════════════════════════════════════════════════════════════════════

public class WhatsAppRobustnessContractTests(TimeoutAgentFactory factory) : IClassFixture<TimeoutAgentFactory>
{
    // robustness.feature — Timeout del agente mayor a 3 s no falla el webhook
    [Fact]
    public async Task AgentTimeout_WebhookReturns200_InboundRecorded_NoOutbound()
    {
        var client = factory.CreateClient();
        var waId = "8100" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.tmo." + Guid.NewGuid().ToString("N");

        var resp = await client.PostAsync("/api/webhooks/whatsapp",
            WaMsg.Inbound(waId, wamid, "Test", "¿cuánto cuesta?"));

        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var match = FindByWaId(contacts!, waId);
        match.Should().NotBeNull("contact must be created even when agent times out");

        var contactId = match!.Value.GetProperty("id").GetString()!;
        var acts = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        acts!.Should().Contain(a => a.GetProperty("type").GetString() == "whatsapp_inbound");
        acts.Should().NotContain(a => a.GetProperty("type").GetString() == "whatsapp_outbound",
            "timed-out agent must not produce outbound activity");
    }

    // robustness.feature — Mensaje de más de 500 caracteres no provoca fallo
    [Fact]
    public async Task LongMessage_520Chars_Returns200_ContactCreated()
    {
        var client = factory.CreateClient();
        var waId = "8200" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.lng." + Guid.NewGuid().ToString("N");

        var resp = await client.PostAsync("/api/webhooks/whatsapp",
            WaMsg.Inbound(waId, wamid, "Test", new string('a', 520)));

        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        FindByWaId(contacts!, waId).Should().NotBeNull("contact must be created for long messages");
    }

    private static JsonElement? FindByWaId(JsonElement[] contacts, string waId)
    {
        foreach (var c in contacts)
            if (c.TryGetProperty("waId", out var w) && w.GetString() == waId)
                return c;
        return null;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PROMPT EVAL TESTS — require ANTHROPIC_API_KEY; silently skip otherwise
// ══════════════════════════════════════════════════════════════════════════════

[Collection("RealAgent")]
public class WhatsAppPromptEvalTests(RealAgentFactory factory) : IClassFixture<RealAgentFactory>
{
    private static bool HasApiKey =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    private async Task<string?> GetOutboundText(string message)
    {
        var client = factory.CreateClient();
        var waId = "9" + Guid.NewGuid().ToString("N")[..11];
        var wamid = "wamid.pe." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp", WaMsg.Inbound(waId, wamid, "Cliente", message));
        await Task.Delay(7000); // debounce (50ms) + Anthropic API (≤5s) + buffer

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contactId = FindContactId(contacts!, waId);
        if (contactId is null) return null;

        var acts = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        return acts!
            .Where(a => a.GetProperty("type").GetString() == "whatsapp_outbound")
            .Select(a => a.GetProperty("description").GetString())
            .FirstOrDefault();
    }

    private static string? FindContactId(JsonElement[] contacts, string waId)
    {
        foreach (var c in contacts)
            if (c.TryGetProperty("waId", out var w) && w.GetString() == waId)
                return c.GetProperty("id").GetString();
        return null;
    }

    // auto-response.feature — Nuevo contacto recibe saludo
    [Fact]
    public async Task NewContact_Hola_ReceivesSpanishGreeting_TwoSentencesMax_NoEmoji()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("Hola");
        reply.Should().NotBeNullOrEmpty();
        GherkinAssert.SentenceCount(reply!).Should().BeLessThanOrEqualTo(2, "max 2 sentences");
        GherkinAssert.ContainsEmoji(reply!).Should().BeFalse("no emojis");
    }

    // auto-response.feature — envíos a provincias
    [Fact]
    public async Task Envios_Arequipa_MentionsDaysDelivery()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿envían a Arequipa?");
        reply.Should().NotBeNullOrEmpty();
        reply!.Should().MatchRegex(@"2.{0,5}3", "must mention 2-3 day delivery window");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // auto-response.feature — envíos internacionales
    [Fact]
    public async Task Envios_Internacional_ConfirmsInternational()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿hacen envíos al extranjero?");
        reply.Should().NotBeNullOrEmpty();
        reply!.ToLower().Should().ContainAny(
            ["internacional", "extranjero", "fuera", "exterior", "todo el mundo"],
            "must confirm international shipping");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
    }

    // auto-response.feature — métodos de pago
    [Fact]
    public async Task Pagos_MentionsAtLeastTwoMethods()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿cómo puedo pagar?");
        reply.Should().NotBeNullOrEmpty();
        var methods = new[] { "yape", "plin", "transferencia", "tarjeta", "contra entrega", "efectivo" };
        var mentioned = methods.Count(m => reply!.ToLower().Contains(m));
        mentioned.Should().BeGreaterThanOrEqualTo(2, "must mention at least 2 payment methods");
        GherkinAssert.SentenceCount(reply!).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply!).Should().BeFalse();
    }

    // auto-response.feature — separados
    [Fact]
    public async Task Separado_MentionsMinimumDeposit()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿puedo separar un producto?");
        reply.Should().NotBeNullOrEmpty();
        reply!.Should().Contain("40", "must mention S/. 40 minimum deposit");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // auto-response.feature — devoluciones
    [Fact]
    public async Task Devoluciones_MentionsFifteenDays()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿aceptan devoluciones?");
        reply.Should().NotBeNullOrEmpty();
        reply!.Should().Contain("15", "must mention 15-day return window");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // auto-response.feature — horario
    [Fact]
    public async Task Horario_MentionsOpeningAndClosingTime()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿a qué hora atienden?");
        reply.Should().NotBeNullOrEmpty();
        reply!.Should().Contain("9", "must mention 9am opening");
        reply.Should().Contain("7", "must mention 7pm closing");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // auto-response.feature — colección METROPOLE
    [Fact]
    public async Task Coleccion_METROPOLE_MentionsCollection()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿tienen la colección METROPOLE?");
        reply.Should().NotBeNullOrEmpty();
        reply!.ToUpper().Should().Contain("METROPOLE");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // auto-response.feature — deflexión Python
    [Fact]
    public async Task OffTopic_Python_RedirectsToCatalog()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿cómo programo en Python?");
        reply.Should().NotBeNullOrEmpty();
        var catalogTerms = new[] { "brazalete", "pulsera", "accesorios", "colección", "metropole", "boss", "armani", "crocodile", "angel" };
        reply!.ToLower().Should().ContainAny(catalogTerms, "must redirect to product catalog");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // auto-response.feature — redirección anillos/relojes
    [Fact]
    public async Task OutOfCatalog_Anillos_RedirectsToAvailableProducts()
    {
        if (!HasApiKey) return;
        var reply = await GetOutboundText("¿tienen anillos o relojes?");
        reply.Should().NotBeNullOrEmpty();
        var catalogTerms = new[] { "brazalete", "pulsera", "colección", "metropole", "boss", "armani", "crocodile", "angel" };
        reply!.ToLower().Should().ContainAny(catalogTerms, "must mention at least one available product");
        GherkinAssert.SentenceCount(reply).Should().BeLessThanOrEqualTo(2);
        GherkinAssert.ContainsEmoji(reply).Should().BeFalse();
    }

    // escalation.feature — "quiero hablar con alguien" → ESCALAR → no outbound
    [Fact]
    public async Task EscalarTrigger_NoOutboundActivity_InboundRecorded()
    {
        if (!HasApiKey) return;
        var client = factory.CreateClient();
        var waId = "9800" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.esc." + Guid.NewGuid().ToString("N");

        await client.PostAsync("/api/webhooks/whatsapp",
            WaMsg.Inbound(waId, wamid, "Cliente", "quiero hablar con alguien"));

        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contactId = FindContactId(contacts!, waId);
        contactId.Should().NotBeNull();

        var acts = await client.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");
        acts!.Should().NotContain(a => a.GetProperty("type").GetString() == "whatsapp_outbound",
            "ESCALAR signal must produce no outbound activity");
        acts.Should().Contain(a => a.GetProperty("type").GetString() == "whatsapp_inbound",
            "inbound activity must still be recorded");
    }
}
