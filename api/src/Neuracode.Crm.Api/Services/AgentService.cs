using System.Net.Http.Json;
using System.Text.Json;

namespace Neuracode.Crm.Api.Services;

public record AgentContext(
    string ContactId,
    string WaId,
    string Name,
    string Message,
    IReadOnlyList<string> RecentMessages,
    string BusinessPrompt);

public interface IAgentService
{
    bool IsConfigured { get; }
    Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default);
}

public sealed class AgentService(IHttpClientFactory httpFactory, IConfiguration config) : IAgentService
{
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";
    private const string DefaultModel = "claude-haiku-4-5-20251001";

    public bool IsConfigured => !string.IsNullOrEmpty(config["ANTHROPIC_API_KEY"]);

    public async Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default)
    {
        var apiKey = config["ANTHROPIC_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var model = config["AGENT_MODEL"] ?? DefaultModel;
            var maxTokens = int.TryParse(config["AGENT_MAX_TOKENS"], out var mt) ? mt : 300;
            var timeoutSeconds = int.TryParse(config["AGENT_TIMEOUT_SECONDS"], out var ts) ? ts : 3;

            var history = ctx.RecentMessages.Count > 0
                ? "\n\nHistorial:\n" + string.Join("\n", ctx.RecentMessages.TakeLast(5))
                : "";

            var userContent = $"{history}\n\nMensaje de {ctx.Name}: {ctx.Message}";

            var requestBody = new
            {
                model,
                max_tokens = maxTokens,
                system = ctx.BusinessPrompt,
                messages = new[] { new { role = "user", content = userContent } }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var resp = await client.PostAsJsonAsync(AnthropicUrl, requestBody, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            if (!json.TryGetProperty("content", out var content) || content.GetArrayLength() == 0) return null;
            if (!content[0].TryGetProperty("text", out var textEl)) return null;

            var text = textEl.GetString()?.Trim();
            if (string.IsNullOrEmpty(text) || text.Equals("ESCALAR", StringComparison.OrdinalIgnoreCase))
                return null;

            return text;
        }
        catch
        {
            return null;
        }
    }
}
