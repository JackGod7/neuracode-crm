using System.Net.Http.Json;
using System.Text.Json;

namespace Neuracode.Crm.Tests.EvalTests;

public sealed class LlmJudge : IDisposable
{
    private const string Model = "claude-haiku-4-5-20251001";
    private const string Url = "https://api.anthropic.com/v1/messages";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private const string SystemPrompt =
        "Eres evaluador de calidad de un asistente de ventas WhatsApp de una tienda peruana " +
        "de pulseras y brazaletes premium (Accesorios Para Él).\n\n" +
        "Evalúa la respuesta del asistente dado el contexto de la conversación.\n" +
        "Responde ÚNICAMENTE con JSON válido, sin texto adicional:\n" +
        "{\"naturalidad\": <1-5>, \"precision\": <1-5>, \"deflectado\": <true|false>, \"razon\": \"<1 línea>\"}\n\n" +
        "Criterios:\n" +
        "- naturalidad: 1=robot corporativo con frases genéricas, 3=aceptable, 5=vendedor humano cálido y directo\n" +
        "- precision: 1=ignoró la pregunta, 3=respondió parcialmente, 5=respondió exactamente lo preguntado\n" +
        "- deflectado: true si redirigió a 'un asesor te contactará' para pregunta de info general " +
        "(precios, catálogo, políticas, disponibilidad). false si respondió directo o si el cliente pidió asesor.";

    private readonly HttpClient _http;

    public LlmJudge(string apiKey)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<JudgeVerdict?> EvaluateAsync(
        string conversation, string agentReply, bool deflectionForbidden,
        CancellationToken ct = default)
    {
        var userContent =
            $"Conversación del cliente:\n{conversation}\n\n" +
            $"Respuesta del asistente a evaluar:\n\"{agentReply}\"\n\n" +
            $"Contexto: el cliente {(deflectionForbidden ? "preguntó info general — NO debería recibir respuesta de asesor" : "podría requerir asesor")}.";

        var body = new
        {
            model = Model,
            max_tokens = 200,
            system = SystemPrompt,
            messages = new[] { new { role = "user", content = userContent } }
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var resp = await _http.PostAsJsonAsync(Url, body, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            var text = json.GetProperty("content")[0].GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            return JsonSerializer.Deserialize<JudgeVerdict>(text[start..(end + 1)], JsonOpts);
        }
        catch { return null; }
    }

    public void Dispose() => _http.Dispose();
}
