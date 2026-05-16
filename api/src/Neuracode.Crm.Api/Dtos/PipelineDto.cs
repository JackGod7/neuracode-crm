namespace Neuracode.Crm.Api.Dtos;

public sealed record PipelineStageDto(
    string Id,
    string Name,
    int Order,
    string Color,
    int IsWon,
    int IsLost,
    List<PipelineDealDto> Deals
);

public sealed record PipelineDealDto(
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
    string? ContactTemperature
);

public sealed record PipelinePutDto(
    string? DealId,
    string? StageId,
    List<BulkStageInputDto>? Stages
);

public sealed record BulkStageInputDto(
    string Name,
    int Order,
    string? Color,
    bool? IsWon,
    bool? IsLost
);
