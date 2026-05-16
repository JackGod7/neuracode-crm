using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Data.Entities;
using FluentAssertions;

namespace Neuracode.Crm.Tests.ContractTests;

public class ClassifyContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    private static Contact MakeContact(string? id = null, string temperature = "cold") => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = "Test Lead",
        Email = "lead@example.com",
        Source = "web",
        Temperature = temperature,
        Score = 0,
        CreatedAt = 1000,
        UpdatedAt = 1000
    };

    [Fact]
    public async Task PostClassify_ValidContact_Returns200WithRulesResult()
    {
        var contactId = Guid.NewGuid().ToString();
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId, "warm"));
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/classify", new { contactId });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("temperature").GetString().Should().BeOneOf("cold", "warm", "hot");
        body.GetProperty("score").GetInt32().Should().BeInRange(0, 100);
        body.GetProperty("mode").GetString().Should().Be("rules");
        body.GetProperty("reasoning").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostClassify_MissingContactId_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/classify", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostClassify_ContactNotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/classify", new { contactId = Guid.NewGuid().ToString() });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostClassify_HotContactWithActivities_ScoreAbove40()
    {
        var contactId = Guid.NewGuid().ToString();
        var recentTs = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400; // yesterday

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId, "hot"));
            await db.SaveChangesAsync();
            db.Activities.AddRange(
                new Activity { Id = Guid.NewGuid().ToString(), Type = "call", Description = "A", ContactId = contactId, CreatedAt = recentTs },
                new Activity { Id = Guid.NewGuid().ToString(), Type = "email", Description = "B", ContactId = contactId, CreatedAt = recentTs }
            );
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/classify", new { contactId });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("score").GetInt32().Should().BeGreaterThan(40);
    }
}

public class FollowupContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    private static Contact MakeContact(string id) => new()
    {
        Id = id,
        Name = "Followup Contact",
        Company = "ACME",
        Source = "web",
        Temperature = "cold",
        Score = 0,
        CreatedAt = 1000,
        UpdatedAt = 1000
    };

    [Fact]
    public async Task GetFollowups_ReturnsBucketsShape()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/followups");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("overdue", out _).Should().BeTrue();
        body.TryGetProperty("today", out _).Should().BeTrue();
        body.TryGetProperty("upcoming", out _).Should().BeTrue();
        body.TryGetProperty("unscheduled", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetFollowups_OverdueActivity_IsInOverdueBucket()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();
        var pastTs = 1000; // Unix epoch + 1000s = very old

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity
            {
                Id = actId, Type = "follow_up", Description = "Overdue",
                ContactId = contactId, ScheduledAt = pastTs, CreatedAt = 1000
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/followups");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var overdue = body.GetProperty("overdue").EnumerateArray().ToList();
        overdue.Should().Contain(a => a.GetProperty("id").GetString() == actId);
    }

    [Fact]
    public async Task GetFollowups_UnscheduledActivity_IsInUnscheduledBucket()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity
            {
                Id = actId, Type = "note", Description = "No schedule",
                ContactId = contactId, ScheduledAt = null, CreatedAt = 1000
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/followups");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var unscheduled = body.GetProperty("unscheduled").EnumerateArray().ToList();
        unscheduled.Should().Contain(a => a.GetProperty("id").GetString() == actId);
    }

    [Fact]
    public async Task GetFollowups_CompletedActivity_NotIncluded()
    {
        var contactId = Guid.NewGuid().ToString();
        var actId = Guid.NewGuid().ToString();

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(MakeContact(contactId));
            await db.SaveChangesAsync();
            db.Activities.Add(new Activity
            {
                Id = actId, Type = "call", Description = "Done",
                ContactId = contactId, CompletedAt = 9999, CreatedAt = 1000
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/followups");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var allIds = body.GetProperty("overdue").EnumerateArray()
            .Concat(body.GetProperty("today").EnumerateArray())
            .Concat(body.GetProperty("upcoming").EnumerateArray())
            .Concat(body.GetProperty("unscheduled").EnumerateArray())
            .Select(a => a.GetProperty("id").GetString());

        allIds.Should().NotContain(actId);
    }
}
