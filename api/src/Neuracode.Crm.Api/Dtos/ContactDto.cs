namespace Neuracode.Crm.Api.Dtos;

public sealed record ContactDto(
    string Id,
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    string Source,
    string Temperature,
    int Score,
    string? Notes,
    string? WaId,
    bool OptedOut,
    bool BotHandling,
    int CreatedAt,
    int UpdatedAt
);
