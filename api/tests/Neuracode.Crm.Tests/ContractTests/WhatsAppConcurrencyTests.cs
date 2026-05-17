using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Neuracode.Crm.Tests.ContractTests;

// ── Payload helper (mirrors AgentContractTests.WaPayload) ────────────────────

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

// ── TEST 1: no-agent factory — concurrent inbound from one new waId ──────────
//
// Bug today: HandleInbound has no mutex per waId, so 5 simultaneous webhooks
// for the same new waId race the contact upsert → multiple contact rows.
// This test fails at the Assert today, passes once a per-waId mutex serializes
// the upsert path.

public class WhatsAppConcurrentNewContactTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    [Fact]
    public async Task Concurrent_NewContact_DoesNotCreateDuplicateContacts()
    {
        // Give each parallel request its own HttpClient — CreateClient itself can race.
        var clients = Enumerable.Range(0, 5).Select(_ => factory.CreateClient()).ToArray();

        var waId = "7100" + Guid.NewGuid().ToString("N")[..8];
        var wamids = Enumerable.Range(0, 5)
            .Select(_ => "wamid.cc." + Guid.NewGuid().ToString("N"))
            .ToArray();

        // Fire 5 truly concurrent POSTs with the SAME waId but DIFFERENT wamids.
        var tasks = Enumerable.Range(0, 5).Select(i =>
            clients[i].PostAsync(
                "/api/webhooks/whatsapp",
                WaPayload.Inbound(waId, wamids[i], "Race Lead", $"msg {i}")));

        await Task.WhenAll(tasks);

        // Read back contacts and assert exactly one row exists for this waId.
        var reader = factory.CreateClient();
        var contacts = await reader.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");

        contacts!
            .Where(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .Should()
            .HaveCount(1, "5 concurrent inbounds for the same waId must upsert to a single contact");
    }
}

// ── TEST 2: responding-agent factory — concurrent inbound on existing contact

public class WhatsAppConcurrentExistingContactTests(RespondingAgentFactory factory)
    : IClassFixture<RespondingAgentFactory>
{
    [Fact]
    public async Task Concurrent_ExistingContact_RecordsAllInbounds_NoOutboundDuplicates()
    {
        var seedClient = factory.CreateClient();
        var waId = "7200" + Guid.NewGuid().ToString("N")[..8];
        var seedWamid = "wamid.seed." + Guid.NewGuid().ToString("N");

        // Pre-create the contact with one inbound. After this call there should
        // be 1 inbound activity and (with the responding agent) 1 outbound.
        await seedClient.PostAsync(
            "/api/webhooks/whatsapp",
            WaPayload.Inbound(waId, seedWamid, "Existing Lead", "Hola"));

        var contacts = await seedClient.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        var contactId = contacts!
            .First(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .GetProperty("id").GetString()!;

        // Fire 3 truly concurrent POSTs with DIFFERENT wamids for the same contact.
        var clients = Enumerable.Range(0, 3).Select(_ => factory.CreateClient()).ToArray();
        var wamids = Enumerable.Range(0, 3)
            .Select(_ => "wamid.cx." + Guid.NewGuid().ToString("N"))
            .ToArray();

        var tasks = Enumerable.Range(0, 3).Select(i =>
            clients[i].PostAsync(
                "/api/webhooks/whatsapp",
                WaPayload.Inbound(waId, wamids[i], "Existing Lead", $"concurrent msg {i}")));

        await Task.WhenAll(tasks);

        // Read activities once after the storm.
        var reader = factory.CreateClient();
        var acts = await reader.GetFromJsonAsync<JsonElement[]>($"/api/activities?contactId={contactId}");

        var inbounds = acts!
            .Where(a => a.GetProperty("type").GetString() == "whatsapp_inbound")
            .ToList();
        var outbounds = acts!
            .Where(a => a.GetProperty("type").GetString() == "whatsapp_outbound")
            .ToList();

        // 1 seed inbound + 3 concurrent inbounds = 4 inbound activities.
        inbounds.Should().HaveCount(4,
            "every inbound webhook with a unique wamid must persist an activity");

        // Sanity: still exactly one contact for this waId (no race-created dupes).
        var contactsAfter = await reader.GetFromJsonAsync<JsonElement[]>("/api/contacts?source=whatsapp");
        contactsAfter!
            .Where(c => c.TryGetProperty("waId", out var w) && w.GetString() == waId)
            .Should().HaveCount(1, "concurrent inbounds for an existing contact must not spawn duplicates");

        // Outbounds: one per processed inbound (1 seed + 3 concurrent = 4).
        // TODO: verify after mutex impl — without a per-waId mutex the agent can
        // see stale RecentMessages and the outbound count may diverge in ways
        // that are hard to assert deterministically. We still assert the
        // one-outbound-per-inbound invariant the fix must guarantee.
        outbounds.Should().HaveCount(inbounds.Count,
            "with the responding agent, each processed inbound produces exactly one outbound");
    }
}
