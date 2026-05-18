using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public sealed class MemoryExtractorService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<MemoryExtractorService> logger) : IMemoryExtractorService
{
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";
    private const string ExtractorModel = "claude-haiku-4-5-20251001";

    private const string ExtractorSystemPrompt =
        "Extract customer data from this WhatsApp conversation turn. " +
        "Return ONLY valid JSON with these fields (omit fields you cannot determine): " +
        "{ \"name\": string|null, \"budget\": string|null, " +
        "\"interested_in\": string[], \"sales_state\": \"browsing\"|\"considering\"|\"ready_to_buy\", " +
        "\"notes\": string|null }. " +
        "Return {} if nothing to extract. Return ONLY JSON, no explanation.";

    public async Task<AgentMemoryData?> ExtractAsync(
        string userMessage, string agentResponse,
        AgentMemoryData existing, CancellationToken ct = default)
    {
        var apiKey = config["ANTHROPIC_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var turn = $"Cliente: {userMessage}\nAgente: {agentResponse}";
            var requestBody = new
            {
                model = ExtractorModel,
                max_tokens = 200,
                system = ExtractorSystemPrompt,
                messages = new[] { new { role = "user", content = turn } }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            using var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var resp = await client.PostAsJsonAsync(AnthropicUrl, requestBody, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            if (!json.TryGetProperty("content", out var content) || content.GetArrayLength() == 0) return null;
            if (!content[0].TryGetProperty("text", out var textEl)) return null;

            var text = textEl.GetString()?.Trim() ?? "";
            return AgentMemoryData.FromJson(text);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Memory extractor timed out");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Memory extractor error");
            return null;
        }
    }
}
