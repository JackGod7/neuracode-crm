namespace Neuracode.Crm.Api.Dtos;

public sealed record ClassifyRequestDto(string? ContactId);

public sealed record ClassifyResultDto(
    string Temperature,
    int Score,
    string NextAction,
    string Reasoning,
    string Mode
);

public sealed record FollowupItemDto(
    string Id,
    string Type,
    string Description,
    string ContactId,
    string? DealId,
    int? ScheduledAt,
    int? CompletedAt,
    int CreatedAt,
    string? ContactName,
    string? ContactCompany
);

public sealed record FollowupBucketsDto(
    List<FollowupItemDto> Overdue,
    List<FollowupItemDto> Today,
    List<FollowupItemDto> Upcoming,
    List<FollowupItemDto> Unscheduled
);
