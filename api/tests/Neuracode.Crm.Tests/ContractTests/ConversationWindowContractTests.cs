using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Domain;
using Neuracode.Crm.Api.Services;

namespace Neuracode.Crm.Tests.ContractTests;

// ── Fakes ──────────────────────────────────────────────────────────────────────

file sealed class FakeWhatsAppWindow : IWhatsAppService
{
    public bool IsConfigured => true;
    public Task<(bool Success, string? MessageId)> SendTextAsync(string toWaId, string text) =>
        Task.FromResult<(bool, string?)>((true, "wamid.win." + Guid.NewGuid().ToString("N")));
    public Task<(bool Success, string? MessageId)> SendTemplateAsync(string toWaId, string templateName,
        string languageCode = "es", params string[] parameters) =>
        Task.FromResult<(bool, string?)>((true, "wamid.win." + Guid.NewGuid().ToString("N")));
}

// ── Factory ────────────────────────────────────────────────────────────────────

public class WindowFactory : NeuracodeFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWhatsAppService>();
            services.AddSingleton<IWhatsAppService, FakeWhatsAppWindow>();
        });
    }
}

// ── Helpers ────────────────────────────────────────────────────────────────────

file static class WindowHelper
{
    public static async Task<Contact> CreateContactAsync(
        WindowFactory factory,
        string? waId = null,
        bool optedOut = false)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Contact",
            Phone = "+51999000000",
            WaId = waId,
            Source = "whatsapp",
            Temperature = "warm",
            OptedOut = optedOut,
            BotHandling = false,
            Score = 0,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await factory.SeedAsync(async db =>
        {
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();
        });

        return contact;
    }

    public static async Task SeedInboundAsync(WindowFactory factory, string waId, DateTime utcTime)
    {
        var ts = (int)new DateTimeOffset(utcTime, TimeSpan.Zero).ToUnixTimeSeconds();
        await factory.SeedAsync(async db =>
        {
            db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                Id = Guid.NewGuid().ToString(),
                WaId = waId,
                Wamid = "wamid.ct." + Guid.NewGuid().ToString("N"),
                Direction = WaDirection.Inbound,
                Body = "hola",
                Status = "received",
                CreatedAt = ts
            });
            await db.SaveChangesAsync();
        });
    }
}

// ── Tests ──────────────────────────────────────────────────────────────────────

public class ConversationWindowContractTests(WindowFactory factory) : IClassFixture<WindowFactory>
{
    // ── GET /api/contacts/{id}/wa-window ──────────────────────────────────────

    [Fact]
    public async Task GetWindow_ContactNotFound_Returns404()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts/{Guid.NewGuid()}/wa-window");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWindow_ContactHasNoWaId_Returns200IsOpenFalse()
    {
        var contact = await WindowHelper.CreateContactAsync(factory, waId: null);
        var client = factory.CreateClient();

        var resp = await client.GetAsync($"/api/contacts/{contact.Id}/wa-window");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isOpen").GetBoolean().Should().BeFalse();
        body.GetProperty("expiresAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("secondsRemaining").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetWindow_LastInbound1hAgo_Returns200IsOpenTrue()
    {
        var waId = "601" + Guid.NewGuid().ToString("N")[..8];
        var contact = await WindowHelper.CreateContactAsync(factory, waId: waId);
        await WindowHelper.SeedInboundAsync(factory, waId, DateTime.UtcNow.AddHours(-1));

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts/{contact.Id}/wa-window");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isOpen").GetBoolean().Should().BeTrue();
        body.GetProperty("secondsRemaining").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("expiresAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetWindow_LastInbound25hAgo_Returns200IsOpenFalse()
    {
        var waId = "602" + Guid.NewGuid().ToString("N")[..8];
        var contact = await WindowHelper.CreateContactAsync(factory, waId: waId);
        await WindowHelper.SeedInboundAsync(factory, waId, DateTime.UtcNow.AddHours(-25));

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/contacts/{contact.Id}/wa-window");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isOpen").GetBoolean().Should().BeFalse();
        body.GetProperty("secondsRemaining").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("expiresAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── POST /api/contacts/{id}/wa-send ───────────────────────────────────────

    [Fact]
    public async Task WaSend_TextType_WindowClosed_Returns400WithWindowClosedError()
    {
        var waId = "603" + Guid.NewGuid().ToString("N")[..8];
        var contact = await WindowHelper.CreateContactAsync(factory, waId: waId);
        await WindowHelper.SeedInboundAsync(factory, waId, DateTime.UtcNow.AddHours(-25));

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contact.Id}/wa-send",
            new { type = "text", content = "hola" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("window closed");
    }

    [Fact]
    public async Task WaSend_OptedOut_Returns400()
    {
        var waId = "604" + Guid.NewGuid().ToString("N")[..8];
        var contact = await WindowHelper.CreateContactAsync(factory, waId: waId, optedOut: true);
        await WindowHelper.SeedInboundAsync(factory, waId, DateTime.UtcNow.AddHours(-1));

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contact.Id}/wa-send",
            new { type = "text", content = "hola" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("opt-out");
    }

    [Fact]
    public async Task WaSend_TemplateType_OptedOut_Returns400()
    {
        var waId = "605" + Guid.NewGuid().ToString("N")[..8];
        var contact = await WindowHelper.CreateContactAsync(factory, waId: waId, optedOut: true);
        await WindowHelper.SeedInboundAsync(factory, waId, DateTime.UtcNow.AddHours(-25));

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            $"/api/contacts/{contact.Id}/wa-send",
            new { type = "template", templateName = "bienvenida" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("opt-out");
    }
}
