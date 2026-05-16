namespace Neuracode.Crm.Api.Dtos;

public sealed record ImportContactItemDto(
    string? Name,
    string? Email,
    string? Phone,
    string? Company,
    string? Source,
    string? Temperature,
    int? Score,
    string? Notes
);

public sealed record ImportRequestDto(List<ImportContactItemDto>? Contacts);

public sealed record ImportResultDto(int Imported, int Failed, List<string> Errors);
