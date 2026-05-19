using System.Net.Http.Json;
using System.Text.Json;
using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public record AgentContext(
    string ContactId,
    string WaId,
    string Name,
    string Message,
    IReadOnlyList<string> RecentMessages,
    string BusinessPrompt,
    AgentMemoryData? Memory = null,
    ProductEntry[]? Catalog = null);

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

    private static readonly object[] ToolDefinitions =
    [
        new
        {
            name = "get_product_price",
            description = "Obtiene precio exacto y detalles de un producto del catálogo",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    sku = new
                    {
                        type = "string",
                        description = "Código del producto: METROPOLE, BOSS, E.ARMANI, CROCODILE, ANGEL_EYES, COMBOS_LOVE"
                    }
                },
                required = new[] { "sku" }
            }
        },
        new
        {
            name = "check_stock",
            description = "Verifica si un producto está disponible en inventario",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    sku = new { type = "string", description = "Código del producto" }
                },
                required = new[] { "sku" }
            }
        }
    ];

    public async Task<string?> HandleAsync(AgentContext ctx, CancellationToken ct = default)
    {
        var apiKey = config["ANTHROPIC_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var model = config["AGENT_MODEL"] ?? DefaultModel;
            var maxTokens = int.TryParse(config["AGENT_MAX_TOKENS"], out var mt) ? mt : 120;
            var timeoutSeconds = int.TryParse(config["AGENT_TIMEOUT_SECONDS"], out var ts) ? ts : 10;
            var catalog = ctx.Catalog ?? ProductCatalog.Default;

            var memory = ctx.Memory is { IsEmpty: false }
                ? $"\n\nMemoria del cliente (cliente recurrente — omite el saludo de bienvenida, ya se presentó antes):\n{ctx.Memory.ToPromptString()}"
                : "";

            var history = ctx.RecentMessages.Count > 0
                ? "\n\nHistorial:\n" + string.Join("\n", ctx.RecentMessages.TakeLast(5))
                : "";

            var userContent = $"{memory}{history}\n\nMensaje de {ctx.Name}: {ctx.Message}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var requestBody = new
            {
                model,
                max_tokens = 512,   // room for tool_use blocks; final response capped at maxTokens
                system = ctx.BusinessPrompt,
                tools = ToolDefinitions,
                messages = new[] { new { role = "user", content = userContent } }
            };

            var resp = await client.PostAsJsonAsync(AnthropicUrl, requestBody, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cts.Token);

            // Tool use loop (max 1 round-trip per N1)
            if (json.TryGetProperty("stop_reason", out var sr) && sr.GetString() == "tool_use" &&
                json.TryGetProperty("content", out var assistantContent))
            {
                var toolResults = new List<object>();

                foreach (var block in assistantContent.EnumerateArray())
                {
                    if (!block.TryGetProperty("type", out var bt) || bt.GetString() != "tool_use") continue;
                    var toolId = block.GetProperty("id").GetString()!;
                    var toolName = block.GetProperty("name").GetString()!;
                    var toolInput = block.GetProperty("input");
                    var result = ProductCatalog.ExecuteToolCall(toolName, toolInput, catalog);
                    toolResults.Add(new { type = "tool_result", tool_use_id = toolId, content = result });
                    logger.LogDebug("Tool {Tool} called for contact {ContactId}, result: {Result}", toolName, ctx.ContactId, result);
                }

                if (toolResults.Count > 0)
                {
                    var body2 = new
                    {
                        model,
                        max_tokens = maxTokens,
                        system = ctx.BusinessPrompt,
                        tools = ToolDefinitions,
                        messages = new object[]
                        {
                            new { role = "user", content = userContent },
                            new { role = "assistant", content = assistantContent },
                            new { role = "user", content = toolResults }
                        }
                    };

                    resp = await client.PostAsJsonAsync(AnthropicUrl, body2, cts.Token);
                    if (!resp.IsSuccessStatusCode) return null;
                    json = await resp.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
                }
            }

            var text = ExtractText(json);
            if (string.IsNullOrEmpty(text)) return null;
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

    private static string? ExtractText(JsonElement json)
    {
        if (!json.TryGetProperty("content", out var content)) return null;
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                block.TryGetProperty("text", out var textEl))
                return textEl.GetString()?.Trim();
        }
        return null;
    }
}
