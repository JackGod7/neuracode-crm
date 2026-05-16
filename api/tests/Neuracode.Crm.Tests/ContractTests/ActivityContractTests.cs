using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Data.Entities;
using FluentAssertions;

namespace Neuracode.Crm.Tests.ContractTests;

public class ActivityContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    private static Contact MakeContact(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = "Contact X",
        Source = "web",
        Temperature = "cold",
        Score = 0,
        CreatedAt = 1000,
        UpdatedAt = 1000
    };

    private static Activity MakeActivity(string contactId, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "call",
        Description = "Called them",
        ContactId = contactId,
        CreatedAt = 1000
    };

    // ── GET /api/activities ──────────────────────────────────────────────────

    [Fact]
    public async Task GetActivities_ReturnsOkWithContactName()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity { Id = actId, Type = "email", Description = "Sent email", ContactId = contactId, CreatedAt = 5000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/activities");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        var item = list!.First(a => a.GetProperty("id").GetString() == actId);
        item.GetProperty("contactName").GetString().Should().Be("Contact X");
        item.GetProperty("type").GetString().Should().Be("email");
    }

    [Fact]
    public async Task GetActivities_FilterByContactId_ReturnsMatching()
    {
        var contactId = Guid.NewGuid().ToString();
        var otherId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            db.Contacts.Add(MakeContact(otherId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity { Id = Guid.NewGuid().ToString(), Type = "note", Description = "Mine", ContactId = contactId, CreatedAt = 2000 });
            db.Activities.Add(new Activity { Id = Guid.NewGuid().ToString(), Type = "note", Description = "Other", ContactId = otherId, CreatedAt = 2000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/activities?contactId={contactId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        list!.Should().OnlyContain(a => a.GetProperty("contactId").GetString() == contactId);
    }

    // ── POST /api/activities ─────────────────────────────────────────────────

    [Fact]
    public async Task PostActivity_ValidBody_Returns201()
    {
        var contactId = Guid.NewGuid().ToString();
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/activities", new
        {
            type = "meeting",
            description = "Intro call",
            contactId
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("type").GetString().Should().Be("meeting");
        body.GetProperty("description").GetString().Should().Be("Intro call");
        body.GetProperty("createdAt").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostActivity_MissingRequired_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/activities", new { type = "call" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/activities/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task PutActivity_MarkComplete_SetsCompletedAt()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity { Id = actId, Type = "call", Description = "Pending", ContactId = contactId, CreatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/activities/{actId}", new { completedAt = true });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("completedAt").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PutActivity_UpdateDescription_ReturnsUpdated()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity { Id = actId, Type = "note", Description = "Old", ContactId = contactId, CreatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/activities/{actId}", new { description = "New note content" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("New note content");
    }

    [Fact]
    public async Task PutActivity_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/activities/{Guid.NewGuid()}", new { description = "X" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/activities/{id} ──────────────────────────────────────────

    [Fact]
    public async Task DeleteActivity_Exists_Returns200WithSuccess()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity { Id = actId, Type = "call", Description = "Bye", ContactId = contactId, CreatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/activities/{actId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteActivity_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/activities/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
