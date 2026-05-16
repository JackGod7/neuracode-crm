namespace Neuracode.Crm.Api.Dtos;

public sealed record DealDto(
    string Id,
    string Title,
    int Value,
    string StageId,
    string ContactId,
    int? ExpectedClose,
    int Probability,
    string? Notes,
    int CreatedAt,
    int UpdatedAt
);

public sealed record DealListDto(
    string Id,
    string Title,
    int Value,
    string StageId,
    string ContactId,
    int? ExpectedClose,
    int Probability,
    string? Notes,
    int CreatedAt,
    int UpdatedAt,
    string? ContactName,
    string? ContactEmail,
    string? ContactTemperature,
    string? StageName,
    string? StageColor,
    int? StageOrder,
    int? StageIsWon,
    int? StageIsLost
);

public sealed record CreateDealDto(
    string? Title,
    string? ContactId,
    string? StageId,
    int? Value,
    int? Probability,
    int? ExpectedClose,
    string? Notes
);

public sealed record UpdateDealDto(
    string? Title,
    string? ContactId,
    string? StageId,
    int? Value,
    int? Probability,
    int? ExpectedClose,
    string? Notes
);
