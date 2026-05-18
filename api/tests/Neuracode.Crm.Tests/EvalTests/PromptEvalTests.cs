using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Neuracode.Crm.Tests.ContractTests;

namespace Neuracode.Crm.Tests.EvalTests;

// Uses RealAgentFactory (defined in WhatsAppGherkinTests.cs) — real Anthropic API + CapturingWhatsAppService

[Collection("RealAgent")]
public class PromptEvalTests(RealAgentFactory factory) : IClassFixture<RealAgentFactory>
{
    private static readonly string? ApiKey =
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private static bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

    // Delay per turn: debounce (50ms in test config) + Anthropic API (≤5s) + buffer
    private const int TurnWaitMs = 7000;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Scenario loading ──────────────────────────────────────────────────────

    public static IEnumerable<object[]> GetScenarios()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "EvalTests", "scenarios", "accesorios-para-el.json");
        if (!File.Exists(path)) return [];
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<ScenariosFile>(json, JsonOpts)!;
        return file.Scenarios.Select(s => new object[] { s.Name, s });
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetScenarios))]
    public async Task Scenario_MeetsScoreThreshold(string scenarioName, EvalScenario scenario)
    {
        if (!HasApiKey) return;

        var (reply, conversation) = await RunTurns(scenario);

        reply.Should().NotBeNull(
            $"scenario '{scenarioName}': agent must produce a reply");

        using var judge = new LlmJudge(ApiKey!);
        var verdict = await judge.EvaluateAsync(
            conversation, reply!, scenario.Expected.DeflectionForbidden);

        verdict.Should().NotBeNull(
            $"scenario '{scenarioName}': judge must return a verdict");

        verdict!.Naturalidad.Should().BeGreaterThanOrEqualTo(
            scenario.Expected.MinNaturalidad,
            $"[{scenarioName}] naturalidad too low. Razon: {verdict.Razon}");

        verdict.Precision.Should().BeGreaterThanOrEqualTo(
            scenario.Expected.MinPrecision,
            $"[{scenarioName}] precision too low. Razon: {verdict.Razon}");

        if (scenario.Expected.DeflectionForbidden)
            verdict.Deflectado.Should().BeFalse(
                $"[{scenarioName}] must not deflect to asesor. Razon: {verdict.Razon}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(string? lastReply, string conversation)> RunTurns(EvalScenario scenario)
    {
        var client = factory.CreateClient();
        var waId = "eval" + Guid.NewGuid().ToString("N")[..10];
        var conversationLines = new List<string>();
        string? lastReply = null;

        foreach (var turn in scenario.Turns)
        {
            // Send all messages in the turn rapidly (debounce groups them)
            foreach (var msg in turn.Messages)
            {
                var wamid = "wamid.eval." + Guid.NewGuid().ToString("N");
                await client.PostAsync("/api/webhooks/whatsapp",
                    BuildInbound(waId, wamid, turn.Name, msg));
                conversationLines.Add($"Cliente ({turn.Name}): {msg}");
            }

            // Wait for debounce + agent response
            await Task.Delay(TurnWaitMs);

            // Read last outbound via compact /chat endpoint
            lastReply = await GetLastOutbound(client, waId);
            if (lastReply is not null)
                conversationLines.Add($"Agente: {lastReply}");
        }

        return (lastReply, string.Join("\n", conversationLines));
    }

    private static async Task<string?> GetLastOutbound(HttpClient client, string waId)
    {
        // First find the contactId
        var contacts = await client.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        if (contacts is null) return null;
        string? contactId = null;
        foreach (var c in contacts)
            if (c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            { contactId = c.GetProperty("id").GetString(); break; }
        if (contactId is null) return null;

        var chat = await client.GetFromJsonAsync<JsonElement[]>($"/api/contacts/{contactId}/chat");
        return chat?
            .Where(m => m.GetProperty("dir").GetString() == "out")
            .Select(m => m.GetProperty("msg").GetString())
            .LastOrDefault();
    }

    private static StringContent BuildInbound(string waId, string wamid, string name, string body)
    {
        var payload = new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "0", changes = new[] { new { value = new {
                messaging_product = "whatsapp",
                contacts = new[] { new { profile = new { name }, wa_id = waId } },
                messages = new[] { new { from = waId, id = wamid, timestamp = "1746820800",
                    text = new { body }, type = "text" } }
            }, field = "messages" } } } }
        };
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}
