using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Data.Entities;
using FluentAssertions;

namespace Neuracode.Crm.Tests.ContractTests;

public class ContactContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    // ── GET /api/contacts ────────────────────────────────────────────────────

    [Fact]
    public async Task GetContacts_ReturnsOkWithContacts()
    {
        var src = "src-" + Guid.NewGuid().ToString("N")[..8];
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Alice",
                Source = src,
                Temperature = "hot",
                Score = 90,
                CreatedAt = 1000,
                UpdatedAt = 1000
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts?source={src}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        body.Should().HaveCount(1);
        body![0].GetProperty("name").GetString().Should().Be("Alice");
        body[0].GetProperty("temperature").GetString().Should().Be("hot");
    }

    [Fact]
    public async Task GetContacts_FilterByTemperature_ReturnsOnlyMatching()
    {
        var src = "src-" + Guid.NewGuid().ToString("N")[..8];
        await factory.SeedAsync(async db =>
        {
            db.Contacts.AddRange(
                new Contact { Id = Guid.NewGuid().ToString(), Name = "Hot Lead", Source = src, Temperature = "hot", Score = 80, CreatedAt = 2000, UpdatedAt = 2000 },
                new Contact { Id = Guid.NewGuid().ToString(), Name = "Cold Lead", Source = src, Temperature = "cold", Score = 10, CreatedAt = 1000, UpdatedAt = 1000 }
            );
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts?temperature=hot&source={src}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        contacts.Should().HaveCount(1);
        contacts![0].GetProperty("temperature").GetString().Should().Be("hot");
    }

    [Fact]
    public async Task GetContacts_FilterBySearch_ReturnsMatchingByName()
    {
        var uniqueName = "Xylophone-" + Guid.NewGuid().ToString("N")[..6];
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid().ToString(),
                Name = uniqueName,
                Source = "web",
                Temperature = "warm",
                Score = 50,
                CreatedAt = 1500,
                UpdatedAt = 1500
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts?search=Xylophone");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        contacts.Should().Contain(c => c.GetProperty("name").GetString() == uniqueName);
    }

    [Fact]
    public async Task GetContacts_OrderedByCreatedAtDesc()
    {
        var src = "src-order-" + Guid.NewGuid().ToString("N")[..6];
        await factory.SeedAsync(async db =>
        {
            db.Contacts.AddRange(
                new Contact { Id = Guid.NewGuid().ToString(), Name = "First", Source = src, Temperature = "cold", Score = 0, CreatedAt = 100, UpdatedAt = 100 },
                new Contact { Id = Guid.NewGuid().ToString(), Name = "Third", Source = src, Temperature = "cold", Score = 0, CreatedAt = 300, UpdatedAt = 300 },
                new Contact { Id = Guid.NewGuid().ToString(), Name = "Second", Source = src, Temperature = "cold", Score = 0, CreatedAt = 200, UpdatedAt = 200 }
            );
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts?source={src}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        contacts.Should().HaveCount(3);
        contacts![0].GetProperty("name").GetString().Should().Be("Third");
        contacts[1].GetProperty("name").GetString().Should().Be("Second");
        contacts[2].GetProperty("name").GetString().Should().Be("First");
    }

    // ── POST /api/contacts ───────────────────────────────────────────────────

    [Fact]
    public async Task PostContact_ValidBody_Returns201WithContact()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/contacts", new
        {
            name = "Bob Builder",
            email = "bob@example.com",
            source = "ads",
            temperature = "warm",
            score = 55
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("name").GetString().Should().Be("Bob Builder");
        body.GetProperty("email").GetString().Should().Be("bob@example.com");
        body.GetProperty("temperature").GetString().Should().Be("warm");
        body.GetProperty("score").GetInt32().Should().Be(55);
        body.GetProperty("createdAt").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostContact_MissingName_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/contacts", new { email = "x@x.com" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostContact_Defaults_AppliedWhenOmitted()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/contacts", new { name = "Minimal Contact" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("source").GetString().Should().Be("otro");
        body.GetProperty("temperature").GetString().Should().Be("cold");
        body.GetProperty("score").GetInt32().Should().Be(0);
    }

    // ── GET /api/contacts/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetContactById_Exists_Returns200()
    {
        var id = Guid.NewGuid().ToString();
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(new Contact { Id = id, Name = "Detail Guy", Source = "web", Temperature = "cold", Score = 0, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(id);
        body.GetProperty("name").GetString().Should().Be("Detail Guy");
    }

    [Fact]
    public async Task GetContactById_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/contacts/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task PutContact_ValidUpdate_Returns200WithUpdated()
    {
        var id = Guid.NewGuid().ToString();
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(new Contact { Id = id, Name = "Old Name", Source = "web", Temperature = "cold", Score = 0, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/contacts/{id}", new { name = "New Name", temperature = "hot", score = 99 });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("New Name");
        body.GetProperty("temperature").GetString().Should().Be("hot");
        body.GetProperty("score").GetInt32().Should().Be(99);
    }

    [Fact]
    public async Task PutContact_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/contacts/{Guid.NewGuid()}", new { name = "X" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/contacts/{id} ────────────────────────────────────────────

    [Fact]
    public async Task DeleteContact_Exists_Returns200WithSuccess()
    {
        var id = Guid.NewGuid().ToString();
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(new Contact { Id = id, Name = "Doomed", Source = "web", Temperature = "cold", Score = 0, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/contacts/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteContact_NotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/contacts/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Enum validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostContact_InvalidTemperature_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/contacts", new
        {
            name = "Enum Test",
            temperature = "banana"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutContact_InvalidSource_Returns400()
    {
        var id = Guid.NewGuid().ToString();
        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(new Contact { Id = id, Name = "Source Test", Source = "web", Temperature = "cold", Score = 0, CreatedAt = 1000, UpdatedAt = 1000 });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync($"/api/contacts/{id}", new { source = "invalid_source" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
