namespace Neuracode.Crm.Api.Dtos;

public sealed record ActivityDto(
    string Id,
    string Type,
    string Description,
    string ContactId,
    string? DealId,
    int? ScheduledAt,
    int? CompletedAt,
    int CreatedAt,
    string? ContactName
);

public sealed record CreateActivityDto(
    string? Type,
    string? Description,
    string? ContactId,
    string? DealId,
    int? ScheduledAt
);

public sealed record UpdateActivityDto(
    bool? CompletedAt,
    string? Description,
    int? ScheduledAt
);
