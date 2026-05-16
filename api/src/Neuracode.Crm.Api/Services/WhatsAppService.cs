using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Neuracode.Crm.Api.Services;

public sealed class WhatsAppService(IHttpClientFactory httpFactory, IConfiguration config)
{
    private const string GraphUrl = "https://graph.facebook.com/v25.0";

    public bool IsConfigured =>
        !string.IsNullOrEmpty(config["META_PHONE_NUMBER_ID"]) &&
        !string.IsNullOrEmpty(config["META_ACCESS_TOKEN"]);

    public async Task<(bool Success, string? MessageId)> SendTextAsync(string toWaId, string text)
    {
        var phoneNumberId = config["META_PHONE_NUMBER_ID"];
        var accessToken = config["META_ACCESS_TOKEN"];
        if (string.IsNullOrEmpty(phoneNumberId) || string.IsNullOrEmpty(accessToken))
            return (false, null);

        var body = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toWaId,
            type = "text",
            text = new { body = text }
        };

        try
        {
            using var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await client.PostAsJsonAsync($"{GraphUrl}/{phoneNumberId}/messages", body);
            if (!resp.IsSuccessStatusCode) return (false, null);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var messageId = json.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0
                && msgs[0].TryGetProperty("id", out var id) ? id.GetString() : null;
            return (true, messageId);
        }
        catch
        {
            return (false, null);
        }
    }

    public async Task<(bool Success, string? MessageId)> SendTemplateAsync(
        string toWaId,
        string templateName,
        string languageCode = "es",
        params string[] parameters)
    {
        var phoneNumberId = config["META_PHONE_NUMBER_ID"]
            ?? throw new InvalidOperationException("META_PHONE_NUMBER_ID not configured");
        var accessToken = config["META_ACCESS_TOKEN"]
            ?? throw new InvalidOperationException("META_ACCESS_TOKEN not configured");

        var body = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toWaId,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = parameters.Length == 0 ? null : new[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(p => new { type = "text", text = p }).ToArray()
                    }
                }
            }
        };

        using var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var resp = await client.PostAsJsonAsync($"{GraphUrl}/{phoneNumberId}/messages", body);
        if (!resp.IsSuccessStatusCode) return (false, null);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = json.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0
            && msgs[0].TryGetProperty("id", out var id) ? id.GetString() : null;

        return (true, messageId);
    }
}
