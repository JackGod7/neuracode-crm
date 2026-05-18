using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public record AgentContext(
    string ContactId,
    string WaId,
    string Name,
    string Message,
    IReadOnlyList<string> RecentMessages,
    string BusinessPrompt,
    AgentMemoryData? Memory = null);

public interface IAgentService
{
    bool IsConfigured { get; }
    Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default);
}

public sealed class AgentService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<AgentService> logger) : IAgentService
{
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";
    private const string DefaultModel = "claude-haiku-4-5-20251001";

    public bool IsConfigured => !string.IsNullOrEmpty(config["ANTHROPIC_API_KEY"]);

    // Skips "S/. 40" via (?<![/\d]) — "/" precedes the dot in "S/."
    private static readonly Regex SentenceEnd = new(@"(?<![/\d])[.!?](?:\s|$)", RegexOptions.Compiled);

    private static string TruncateToTwoSentences(string text)
    {
        var matches = SentenceEnd.Matches(text);
        if (matches.Count <= 2) return text;
        return text[..(matches[1].Index + 1)].TrimEnd();
    }

    public async Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default)
    {
        var apiKey = config["ANTHROPIC_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var model = config["AGENT_MODEL"] ?? DefaultModel;
            var maxTokens = int.TryParse(config["AGENT_MAX_TOKENS"], out var mt) ? mt : 300;
            var timeoutSeconds = int.TryParse(config["AGENT_TIMEOUT_SECONDS"], out var ts) ? ts : 3;

            var memory = ctx.Memory is { IsEmpty: false }
                ? $"\n\nMemoria del cliente (cliente recurrente — omite el saludo de bienvenida, ya se presentó antes):\n{ctx.Memory.ToPromptString()}"
                : "";

            var history = ctx.RecentMessages.Count > 0
                ? "\n\nHistorial:\n" + string.Join("\n", ctx.RecentMessages.TakeLast(5))
                : "";

            var userContent = $"{memory}{history}\n\nMensaje de {ctx.Name}: {ctx.Message}";

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
            if (string.IsNullOrEmpty(text)) return null;
            text = TruncateToTwoSentences(text);
            if (text.Equals("ESCALAR", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Agent signalled ESCALAR for contact {ContactId}", ctx.ContactId);
                return null;
            }

            return text;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Agent timeout for contact {ContactId}", ctx.ContactId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent error for contact {ContactId}", ctx.ContactId);
            return null;
        }
    }
}
