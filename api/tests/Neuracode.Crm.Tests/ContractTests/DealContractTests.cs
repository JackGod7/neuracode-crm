using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Data.Entities;
using FluentAssertions;

namespace Neuracode.Crm.Tests.ContractTests;

public class DealContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    private static Contact MakeContact(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = "Test Contact",
        Source = "web",
        Temperature = "cold",
        Score = 0,
        CreatedAt = 1000,
        UpdatedAt = 1000
    };

    private static PipelineStage MakeStage(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = "Lead",
        Order = 1,
        Color = "#64748b",
        IsWon = 0,
        IsLost = 0
    };

    // ── GET /api/deals ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeals_ReturnsOkWithJoinedFields()
    {
        var contactId = Guid.NewGuid().ToString();
        var stageId = Guid.NewGuid().ToString();
        var dealId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.PipelineStages.Add(MakeStage(stageId));
            await db.SaveChangesAsync();

            db.Deals.Add(new Deal
            {
                Id = dealId,
                Title = "Big Sale",
                Value = 5000,
                StageId = stageId,
                ContactId = contactId,
                Probability = 75,
                CreatedAt = 2000,
                UpdatedAt = 2000
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/deals");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var deals = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        var deal = deals!.First(d => d.GetProperty("id").GetString() == dealId);
        deal.GetProperty("title").GetString().Should().Be("Big Sale");
        deal.GetProperty("contactName").GetString().Should().Be("Test Contact");
        deal.GetProperty("stageName").GetString().Should().Be("Lead");
        deal.GetProperty("value").GetInt32().Should().Be(5000);
    }

    // ── POST /api/deals ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostDeal_ValidBody_Returns201()
    {
        var contactId = Guid.NewGuid().ToString();
        var stageId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.PipelineStages.Add(MakeStage(stageId));
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/deals", new
        {
            title = "New Deal",
            contactId,
            stageId,
            value = 1000,
            probability = 50
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("title").GetString().Should().Be("New Deal");
        body.GetProperty("value").GetInt32().Should().Be(1000);
        body.GetProperty("probability").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task PostDeal_MissingTitle_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/deals", new { contactId = Guid.NewGuid().ToString() });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDeal_MissingContactId_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/deals", new { title = "X" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/deals/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDealById_Exists_Returns200()
    {
        var contactId = Guid.NewGuid().ToString();
        var stageId = Guid.NewGuid().ToString();
        var dealId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.PipelineStages.Add(MakeStage(stageId));
            await db.SaveChangesAsync();
            db.Deals.Add(new Deal { Id = dealId, Title = "Found", Value = 0, StageId = stageId, ContactId = contactId, Probability = 0, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/deals/{dealId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(dealId);
        body.GetProperty("title").GetString().Should().Be("Found");
    }

    [Fact]
    public async Task GetDealById_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/deals/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/deals/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task PutDeal_ValidUpdate_Returns200()
    {
        var contactId = Guid.NewGuid().ToString();
        var stageId = Guid.NewGuid().ToString();
        var dealId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.PipelineStages.Add(MakeStage(stageId));
            await db.SaveChangesAsync();
            db.Deals.Add(new Deal { Id = dealId, Title = "Old", Value = 100, StageId = stageId, ContactId = contactId, Probability = 10, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/deals/{dealId}", new { title = "Updated", value = 9999, probability = 80 });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Updated");
        body.GetProperty("value").GetInt32().Should().Be(9999);
        body.GetProperty("probability").GetInt32().Should().Be(80);
    }

    [Fact]
    public async Task PutDeal_ProbabilityClamped_StaysInRange()
    {
        var contactId = Guid.NewGuid().ToString();
        var stageId = Guid.NewGuid().ToString();
        var dealId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.PipelineStages.Add(MakeStage(stageId));
            await db.SaveChangesAsync();
            db.Deals.Add(new Deal { Id = dealId, Title = "Clamp", Value = 0, StageId = stageId, ContactId = contactId, Probability = 50, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/deals/{dealId}", new { probability = 999 });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("probability").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task PutDeal_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/deals/{Guid.NewGuid()}", new { title = "X" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/deals/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteDeal_Exists_Returns200WithSuccess()
    {
        var contactId = Guid.NewGuid().ToString();
        var stageId = Guid.NewGuid().ToString();
        var dealId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.PipelineStages.Add(MakeStage(stageId));
            await db.SaveChangesAsync();
            db.Deals.Add(new Deal { Id = dealId, Title = "Doomed", Value = 0, StageId = stageId, ContactId = contactId, Probability = 0, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/deals/{dealId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDeal_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/deals/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
