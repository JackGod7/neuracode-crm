namespace Neuracode.Crm.Api.Dtos;

public sealed record CreateContactDto(
    string? Name,
    string? Email,
    string? Phone,
    string? Company,
    string? Source,
    string? Temperature,
    int? Score,
    string? Notes
);
