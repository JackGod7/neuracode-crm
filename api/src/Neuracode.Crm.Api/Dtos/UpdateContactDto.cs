namespace Neuracode.Crm.Api.Dtos;

public sealed record UpdateContactDto(
    string? Name,
    string? Email,
    string? Phone,
    string? Company,
    string? Source,
    string? Temperature,
    int? Score,
    string? Notes
);
