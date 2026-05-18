using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neuracode.Crm.Api.Domain;
using Neuracode.Crm.Api.Services;

namespace Neuracode.Crm.Tests.ContractTests;

// ── Fakes ─────────────────────────────────────────────────────────────────────

public sealed class SpyAgentService : IAgentService
{
    public AgentContext? LastContext { get; private set; }
    public bool IsConfigured => true;
    public Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default)
    {
        LastContext = ctx;
        return Task.FromResult<string?>("Respuesta de prueba.");
    }
}

file sealed class FakeWhatsAppMem : IWhatsAppService
{
    public bool IsConfigured => true;
    public Task<(bool Success, string? MessageId)> SendTextAsync(string toWaId, string text) =>
        Task.FromResult<(bool, string?)>((true, "wamid.mem." + Guid.NewGuid().ToString("N")));
    public Task<(bool Success, string? MessageId)> SendTemplateAsync(string toWaId, string templateName,
        string languageCode = "es", params string[] parameters) =>
        Task.FromResult<(bool, string?)>((true, "wamid.mem." + Guid.NewGuid().ToString("N")));
}

file sealed class ExtractingFakeService : IMemoryExtractorService
{
    private readonly AgentMemoryData? _result;
    public ExtractingFakeService(AgentMemoryData? result) => _result = result;
    public Task<AgentMemoryData?> ExtractAsync(string userMessage, string agentResponse,
        AgentMemoryData existing, CancellationToken ct = default) =>
        Task.FromResult(_result);
}

file sealed class FailingExtractorService : IMemoryExtractorService
{
    public Task<AgentMemoryData?> ExtractAsync(string userMessage, string agentResponse,
        AgentMemoryData existing, CancellationToken ct = default) =>
        throw new InvalidOperationException("Extractor simulated failure");
}

// ── Factories ─────────────────────────────────────────────────────────────────

public class AgentMemorySpyFactory : NeuracodeFactory
{
    public SpyAgentService SpyAgent { get; } = new();
    private readonly AgentMemoryData? _extractorResult;

    public AgentMemorySpyFactory() =>
        _extractorResult = new AgentMemoryData("Juan Extraido", null, ["BOSS"], "considering", null);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentService>();
            services.AddSingleton<IAgentService>(SpyAgent);
            services.RemoveAll<IWhatsAppService>();
            services.AddSingleton<IWhatsAppService, FakeWhatsAppMem>();
            services.RemoveAll<IMemoryExtractorService>();
            services.AddSingleton<IMemoryExtractorService>(new ExtractingFakeService(_extractorResult));
        });
    }
}

public class FailingExtractorFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentService>();
            services.AddSingleton<IAgentService, RespondingMemAgent>();
            services.RemoveAll<IWhatsAppService>();
            services.AddSingleton<IWhatsAppService, FakeWhatsAppMem>();
            services.RemoveAll<IMemoryExtractorService>();
            services.AddSingleton<IMemoryExtractorService, FailingExtractorService>();
        });
    }
}

file sealed class RespondingMemAgent : IAgentService
{
    public bool IsConfigured => true;
    public Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default) =>
        Task.FromResult<string?>("Respuesta.");
}

// ── Helper ────────────────────────────────────────────────────────────────────

file static class MemWaPayload
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
                                contacts = new[] { new { profile = new { name }, wa_id = waId } },
                                messages = new[]
                                {
                                    new { from = waId, id = wamid, timestamp = "1746820800",
                                          text = new { body }, type = "text" }
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

// ── Tests ─────────────────────────────────────────────────────────────────────

public class AgentMemoryContractTests(AgentMemorySpyFactory factory) : IClassFixture<AgentMemorySpyFactory>
{
    [Fact]
    public async Task FirstInbound_AgentContextHasMemoryObject()
    {
        var client = factory.CreateClient();
        var waId = "7000" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.mem1." + Guid.NewGuid().ToString("N");

        var resp = await client.PostAsync("/api/webhooks/whatsapp",
            MemWaPayload.Inbound(waId, wamid, "Cliente", "Hola"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.SpyAgent.LastContext.Should().NotBeNull();
        factory.SpyAgent.LastContext!.Memory.Should().NotBeNull(
            "agent context must always include a Memory object, even if empty");
    }

    [Fact]
    public async Task SecondInbound_AgentReceivesMemoryFromFirstTurn()
    {
        var client = factory.CreateClient();
        var waId = "7100" + Guid.NewGuid().ToString("N")[..8];

        // First inbound: extractor saves "Juan Extraido" into memory
        var wamid1 = "wamid.mem2a." + Guid.NewGuid().ToString("N");
        await client.PostAsync("/api/webhooks/whatsapp",
            MemWaPayload.Inbound(waId, wamid1, "Cliente", "Me llamo Juan"));

        // Let background task complete
        await Task.Delay(300);

        // Second inbound: agent should receive the persisted memory
        var wamid2 = "wamid.mem2b." + Guid.NewGuid().ToString("N");
        await client.PostAsync("/api/webhooks/whatsapp",
            MemWaPayload.Inbound(waId, wamid2, "Cliente", "¿tienen el BOSS?"));

        factory.SpyAgent.LastContext!.Memory.Should().NotBeNull();
        factory.SpyAgent.LastContext.Memory!.Name.Should().Be("Juan Extraido",
            "memory extracted in first turn must be available in second turn");
        factory.SpyAgent.LastContext.Memory.SalesState.Should().Be("considering");
    }

    [Fact]
    public async Task Inbound_SeededMemory_AgentReceivesIt()
    {
        var client = factory.CreateClient();
        var waId = "7200" + Guid.NewGuid().ToString("N")[..8];

        await factory.SeedAsync(async db =>
        {
            db.AgentMemories.Add(new Neuracode.Crm.Api.Data.Entities.AgentMemory
            {
                WaId = waId,
                Data = """{"name":"Maria","budget":"S/. 400","interested_in":["Combos Love"],"sales_state":"considering","notes":null}""",
                UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await db.SaveChangesAsync();
        });

        var wamid = "wamid.seeded." + Guid.NewGuid().ToString("N");
        await client.PostAsync("/api/webhooks/whatsapp",
            MemWaPayload.Inbound(waId, wamid, "Maria", "Quiero el combo"));

        factory.SpyAgent.LastContext!.Memory!.Name.Should().Be("Maria");
        factory.SpyAgent.LastContext.Memory.SalesState.Should().Be("considering");
        factory.SpyAgent.LastContext.Memory.InterestedIn.Should().Contain("Combos Love");
    }
}

public class AgentMemoryFailingExtractorTests(FailingExtractorFactory factory) : IClassFixture<FailingExtractorFactory>
{
    [Fact]
    public async Task Inbound_ExtractorThrows_Returns200_MemoryUnchanged()
    {
        var client = factory.CreateClient();
        var waId = "7300" + Guid.NewGuid().ToString("N")[..8];
        var wamid = "wamid.failext." + Guid.NewGuid().ToString("N");

        var resp = await client.PostAsync("/api/webhooks/whatsapp",
            MemWaPayload.Inbound(waId, wamid, "Cliente", "Hola"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "extractor failure must never propagate to the webhook response");
    }
}
