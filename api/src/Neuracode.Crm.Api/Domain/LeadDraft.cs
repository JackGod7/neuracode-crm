namespace Neuracode.Crm.Api.Domain;

public record LeadDraft(
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Company = null,
    string Source = "otro",
    string Temperature = "cold",
    int Score = 0,
    string? Notes = null);
