namespace Neuracode.Crm.Api.Services;

public interface IWhatsAppService
{
    bool IsConfigured { get; }
    Task<(bool Success, string? MessageId)> SendTextAsync(string toWaId, string text);
    Task<(bool Success, string? MessageId)> SendTemplateAsync(string toWaId, string templateName, string languageCode = "es", params string[] parameters);
}
