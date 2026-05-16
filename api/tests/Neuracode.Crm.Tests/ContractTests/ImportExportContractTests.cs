using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Data.Entities;
using FluentAssertions;

namespace Neuracode.Crm.Tests.ContractTests;

public class ImportContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    [Fact]
    public async Task PostImport_ValidContacts_Returns201WithCount()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/import", new
        {
            contacts = new object[]
            {
                new { name = "Import One", email = "one@example.com", source = "import" },
                new { name = "Import Two", phone = "555-0001" }
            }
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(2);
        body.GetProperty("failed").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task PostImport_DefaultsApplied()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/import", new
        {
            contacts = new[] { new { name = "Default Test " + Guid.NewGuid().ToString("N")[..6] } }
        });

        // verify via GET that defaults were applied
        var getResp = await client.GetAsync("/api/contacts?search=Default+Test");
        var list = await getResp.Content.ReadFromJsonAsync<List<JsonElement>>();
        var imported = list!.FirstOrDefault(c =>
            c.GetProperty("source").GetString() == "import" &&
            c.GetProperty("temperature").GetString() == "cold");
        imported.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task PostImport_EmptyArray_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/import", new { contacts = Array.Empty<object>() });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostImport_MissingContactsField_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/import", new { data = "wrong" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostImport_ExceedsLimit_Returns400()
    {
        var client = factory.CreateClient();
        var contacts = Enumerable.Range(0, 10_001)
            .Select(i => new { name = $"Contact {i}" })
            .ToArray();
        var resp = await client.PostAsJsonAsync("/api/import", new { contacts });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostImport_MixedValidAndInvalid_Returns207()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/import", new
        {
            contacts = new object[]
            {
                new { name = "Valid Contact Mix" },
                new { email = "no-name@example.com" }  // missing name — should fail
            }
        });

        resp.StatusCode.Should().Be(HttpStatusCode.MultiStatus); // 207
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(1);
        body.GetProperty("failed").GetInt32().Should().Be(1);
        body.GetProperty("errors").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task PostImport_CreatesActivityForEachImportedContact()
    {
        var client = factory.CreateClient();
        var name = "ActivityImport " + Guid.NewGuid().ToString("N")[..6];
        var resp = await client.PostAsJsonAsync("/api/import", new
        {
            contacts = new[] { new { name } }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/contacts?search={Uri.EscapeDataString(name)}");
        list.Should().HaveCountGreaterThanOrEqualTo(1);
        var contactId = list![0].GetProperty("id").GetString()!;

        var acts = await client.GetFromJsonAsync<List<JsonElement>>($"/api/activities?contactId={contactId}");
        acts.Should().NotBeNullOrEmpty("import must create an activity for each contact");
        acts!.Should().Contain(a => a.GetProperty("description").GetString()!.Contains("import", StringComparison.OrdinalIgnoreCase));
    }
}

public class ExportContractTests(NeuracodeFactory factory) : IClassFixture<NeuracodeFactory>
{
    [Fact]
    public async Task GetExport_Contacts_ReturnsCsvContentType()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/export?type=contacts");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Nombre");
    }

    [Fact]
    public async Task GetExport_Deals_ReturnsCsvContentType()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/export?type=deals");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Titulo");
    }

    [Fact]
    public async Task GetExport_DefaultType_IsContacts()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/export");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Nombre");
    }

    [Fact]
    public async Task GetExport_InvalidType_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/export?type=unknown");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
