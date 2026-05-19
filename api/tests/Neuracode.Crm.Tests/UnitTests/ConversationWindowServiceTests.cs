using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Domain;
using Neuracode.Crm.Api.Services;

namespace Neuracode.Crm.Tests.UnitTests;

public class ConversationWindowServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ConversationWindowService _sut;

    public ConversationWindowServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _sut = new ConversationWindowService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task SeedInboundMessageAsync(string waId, DateTime utcTime)
    {
        var ts = (int)new DateTimeOffset(utcTime, TimeSpan.Zero).ToUnixTimeSeconds();
        _db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Id = Guid.NewGuid().ToString(),
            WaId = waId,
            Wamid = "wamid.unit." + Guid.NewGuid().ToString("N"),
            Direction = WaDirection.Inbound,
            Body = "hola",
            Status = "received",
            CreatedAt = ts
        });
        await _db.SaveChangesAsync();
    }

    // ── test 1: window is open when last inbound was 23h59m ago ─────────────

    [Fact]
    public async Task GetWindowAsync_LastInbound23h59mAgo_IsOpen()
    {
        var waId = "521" + Guid.NewGuid().ToString("N")[..8];
        var lastInbound = DateTime.UtcNow.AddHours(-23).AddMinutes(-59);
        await SeedInboundMessageAsync(waId, lastInbound);

        var result = await _sut.GetWindowAsync(waId);

        result.IsOpen.Should().BeTrue("window should still be open when 23h59m have passed");
        result.WaId.Should().Be(waId);
    }

    // ── test 2: window is closed when last inbound was 24h01m ago ───────────

    [Fact]
    public async Task GetWindowAsync_LastInbound24h01mAgo_IsClosed()
    {
        var waId = "522" + Guid.NewGuid().ToString("N")[..8];
        var lastInbound = DateTime.UtcNow.AddHours(-24).AddMinutes(-1);
        await SeedInboundMessageAsync(waId, lastInbound);

        var result = await _sut.GetWindowAsync(waId);

        result.IsOpen.Should().BeFalse("window should be closed when 24h01m have passed");
    }

    // ── test 3: no inbound messages → window is closed ──────────────────────

    [Fact]
    public async Task GetWindowAsync_NoInboundMessages_IsClosed()
    {
        var waId = "523" + Guid.NewGuid().ToString("N")[..8];

        var result = await _sut.GetWindowAsync(waId);

        result.IsOpen.Should().BeFalse("window should be closed when there are no inbound messages");
        result.WaId.Should().Be(waId);
    }

    // ── test 4: open window → SecondsRemaining is not null and > 0 ──────────

    [Fact]
    public async Task GetWindowAsync_IsOpen_SecondsRemainingIsPositive()
    {
        var waId = "524" + Guid.NewGuid().ToString("N")[..8];
        var lastInbound = DateTime.UtcNow.AddHours(-1);
        await SeedInboundMessageAsync(waId, lastInbound);

        var result = await _sut.GetWindowAsync(waId);

        result.IsOpen.Should().BeTrue();
        result.SecondsRemaining.Should().NotBeNull("SecondsRemaining must be set when window is open");
        result.SecondsRemaining.Should().BeGreaterThan(0, "SecondsRemaining must be positive");
        result.ExpiresAt.Should().NotBeNull("ExpiresAt must be set when window is open");
    }

    // ── test 5: closed window → SecondsRemaining and ExpiresAt are null ─────

    [Fact]
    public async Task GetWindowAsync_IsClosed_SecondsRemainingAndExpiresAtAreNull()
    {
        var waId = "525" + Guid.NewGuid().ToString("N")[..8];
        var lastInbound = DateTime.UtcNow.AddHours(-25);
        await SeedInboundMessageAsync(waId, lastInbound);

        var result = await _sut.GetWindowAsync(waId);

        result.IsOpen.Should().BeFalse();
        result.SecondsRemaining.Should().BeNull("SecondsRemaining must be null when window is closed");
        result.ExpiresAt.Should().BeNull("ExpiresAt must be null when window is closed");
    }
}
