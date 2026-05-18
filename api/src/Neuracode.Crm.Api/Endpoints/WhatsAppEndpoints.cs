using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Domain;
using Neuracode.Crm.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public record WhatsAppSendRequest(string TemplateName, string? LanguageCode, string[]? Parameters);

public static class WhatsAppEndpoints
{
    private sealed class DebounceState
    {
        public readonly List<string> Messages = [];
        public CancellationTokenSource Cts = new();
    }

    const string DefaultBusinessPrompt =
        "Eres el asistente de ventas de Accesorios Para Él (accesoriosparael.store), " +
        "tienda de joyería y accesorios en Perú. " +
        "Respondes SOLO en español, sin emojis, con EXACTAMENTE 1 o 2 oraciones por respuesta. " +
        "NUNCA escribas una tercera oración. Termina después de la segunda oración sin frases de cierre ni invitación.\n\n" +
        "Productos disponibles:\n" +
        "- Brazaletes/pulseras para caballero: METROPOLE, BOSS, E.ARMANI, CROCODILE\n" +
        "- Brazaletes/pulseras para dama: ANGEL EYES\n" +
        "- Combos para parejas: Combos Love (brazalete caballero + pulsera dama)\n" +
        "- Precios: S/. 149–607, hasta 63% de descuento\n\n" +
        "Política de la tienda:\n" +
        "- Envíos: todo Perú + internacional; 2-3 días hábiles a provincias\n" +
        "- Pagos: Yape, Plin, transferencia bancaria, tarjeta de crédito/débito, contra entrega\n" +
        "- Separados: depósito mínimo S/. 40 confirma la compra\n" +
        "- Cambios: hasta 15 días calendario desde la compra\n" +
        "- Atención: todos los días de 9am a 7pm\n\n" +
        "Productos que NO vendemos (si preguntan, acláralo y redirige al catálogo):\n" +
        "- Anillos, collares, aretes, relojes, billeteras ni ningún otro accesorio\n" +
        "- Solo vendemos brazaletes y pulseras (caballero y dama) y Combos Love para parejas\n\n" +
        "Si el cliente dice 'quiero hablar con alguien', 'quiero un asesor', o similar → " +
        "responde ESCALAR (solo esa palabra, nada más).\n\n" +
        "Si la pregunta está fuera de tu alcance (temas técnicos, preguntas no relacionadas) " +
        "→ redirecciona amablemente a los productos disponibles.";

    static readonly ConcurrentDictionary<string, SemaphoreSlim> _contactLocks = new();
    static readonly ConcurrentDictionary<string, DebounceState> _debounce = new();

    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/webhooks/whatsapp", VerifyWebhook).WithTags("whatsapp");
        app.MapPost("/api/webhooks/whatsapp", HandleInbound).WithTags("whatsapp");
        app.MapPost("/api/contacts/{id}/whatsapp/send", SendTemplate).WithTags("whatsapp");
        app.MapGet("/api/contacts/{id}/chat", Chat).WithTags("whatsapp");
        app.MapPost("/api/contacts/{id}/handoff", Handoff).WithTags("whatsapp");
        app.MapPost("/api/contacts/{id}/bot-resume", BotResume).WithTags("whatsapp");
        return app;
    }

    static IResult VerifyWebhook(HttpRequest request, IConfiguration config)
    {
        var mode = request.Query["hub.mode"].ToString();
        var token = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();
        var expected = config["META_VERIFY_TOKEN"] ?? "";

        return mode == "subscribe" && expected.Length > 0 && token == expected && challenge.Length > 0
            ? Results.Text(challenge)
            : Results.Json(new { error = "Forbidden" }, statusCode: 403);
    }

    static async Task<IResult> HandleInbound(
        HttpRequest request, AppDbContext db, IConfiguration config,
        IAgentService agentService, IWhatsAppService whatsApp,
        IAgentMemoryRepository memoryRepo, IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory)
    {
        request.EnableBuffering();
        var rawBody = await new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        request.Body.Position = 0;

        var appSecret = config["META_APP_SECRET"];
        if (!string.IsNullOrEmpty(appSecret))
        {
            var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (!ValidateHmac(rawBody, appSecret, signature))
                return Results.Json(new { error = "Invalid signature" }, statusCode: 403);
        }

        JsonElement payload;
        try { payload = JsonSerializer.Deserialize<JsonElement>(rawBody); }
        catch { return Results.BadRequest(new { error = "Invalid JSON" }); }

        if (!payload.TryGetProperty("entry", out var entries))
            return Results.Ok(new { received = true });

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes)) continue;
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)) continue;
                if (!value.TryGetProperty("messages", out var messages)) continue;

                var contactsEl = value.TryGetProperty("contacts", out var c) ? c : default;

                foreach (var msg in messages.EnumerateArray())
                {
                    var wamid = msg.TryGetProperty("id", out var mid) ? mid.GetString() : null;
                    var waId = msg.TryGetProperty("from", out var from) ? from.GetString() : null;
                    var msgBody = msg.TryGetProperty("text", out var txt) && txt.TryGetProperty("body", out var b)
                        ? b.GetString() ?? ""
                        : "";

                    if (string.IsNullOrEmpty(wamid) || string.IsNullOrEmpty(waId)) continue;

                    var logger = loggerFactory.CreateLogger("WhatsApp");
                    var correlationId = request.HttpContext.TraceIdentifier;
                    using var scope = logger.BeginScope(new { correlationId, waId });

                    var sem = _contactLocks.GetOrAdd(waId, _ => new SemaphoreSlim(1, 1));
                    await sem.WaitAsync();
                    try
                    {
                        if (await db.WhatsAppMessages.AnyAsync(m => m.Wamid == wamid)) continue;

                        var displayName = ResolveDisplayName(contactsEl, waId);

                        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.WaId == waId);
                        if (contact is null)
                        {
                            contact = new Contact
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = displayName,
                                Phone = "+" + waId,
                                WaId = waId,
                                Source = "whatsapp",
                                Temperature = "hot",
                                BotHandling = true,
                                Score = 0,
                                CreatedAt = now,
                                UpdatedAt = now
                            };
                            db.Contacts.Add(contact);
                            await db.SaveChangesAsync();
                        }

                        db.Activities.Add(new Activity
                        {
                            Id = Guid.NewGuid().ToString(),
                            Type = ActivityTypes.WhatsAppInbound,
                            Description = msgBody,
                            ContactId = contact.Id,
                            Wamid = wamid,
                            CreatedAt = now
                        });

                        db.WhatsAppMessages.Add(new WhatsAppMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            WaId = waId,
                            Wamid = wamid,
                            Direction = WaDirection.Inbound,
                            Body = msgBody,
                            Status = "received",
                            CreatedAt = now
                        });

                        await db.SaveChangesAsync();

                        // Debounce: buffer message, fire agent after inactivity window
                        if (contact.BotHandling && agentService.IsConfigured && !contact.OptedOut
                            && !string.IsNullOrEmpty(msgBody))
                        {
                            var debounceMs = int.TryParse(config["AGENT_DEBOUNCE_MS"], out var dm) ? dm : 4000;
                            var state = _debounce.GetOrAdd(waId, _ => new DebounceState());
                            CancellationTokenSource newCts;
                            lock (state)
                            {
                                state.Messages.Add(msgBody);
                                state.Cts.Cancel();
                                state.Cts.Dispose();
                                state.Cts = newCts = new CancellationTokenSource();
                            }

                            var capturedWaId = waId;
                            var capturedDisplayName = displayName;
                            var capturedConfig = config;
                            var capturedFactory = scopeFactory;
                            var capturedLogger = logger;
                            _ = Task.Run(async () =>
                            {
                                try { await Task.Delay(debounceMs, newCts.Token); }
                                catch (OperationCanceledException) { return; }

                                List<string> msgs;
                                lock (state) { msgs = [.. state.Messages]; state.Messages.Clear(); }
                                if (msgs.Count == 0) return;
                                var combined = string.Join(" ", msgs);

                                try
                                {
                                    using var agentScope = capturedFactory.CreateScope();
                                    var sp = agentScope.ServiceProvider;
                                    var scopedDb = sp.GetRequiredService<AppDbContext>();
                                    var scopedAgent = sp.GetRequiredService<IAgentService>();
                                    var scopedWhatsApp = sp.GetRequiredService<IWhatsAppService>();
                                    var scopedMemoryRepo = sp.GetRequiredService<IAgentMemoryRepository>();

                                    var freshContact = await scopedDb.Contacts.FirstOrDefaultAsync(c => c.WaId == capturedWaId);
                                    if (freshContact is null || !freshContact.BotHandling || freshContact.OptedOut) return;

                                    var recentMsgs = await scopedDb.Activities
                                        .Where(a => a.ContactId == freshContact.Id && a.Type!.StartsWith("whatsapp"))
                                        .OrderByDescending(a => a.CreatedAt)
                                        .Take(10)
                                        .Select(a => (a.Type == "whatsapp_inbound" ? "Cliente" : "Bot") + ": " + a.Description)
                                        .ToListAsync();

                                    var memory = await scopedMemoryRepo.GetAsync(capturedWaId);
                                    var businessPrompt = capturedConfig["AGENT_BUSINESS_PROMPT"] ?? DefaultBusinessPrompt;
                                    var ctx = new AgentContext(freshContact.Id, capturedWaId, capturedDisplayName, combined, recentMsgs, businessPrompt, memory);
                                    var agentReply = await scopedAgent.HandleAsync(ctx);

                                    if (agentReply is null) return;

                                    var (sendSuccess, replyWamid) = await scopedWhatsApp.SendTextAsync(capturedWaId, agentReply);
                                    if (!sendSuccess) return;

                                    var now2 = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    scopedDb.Activities.Add(new Activity
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Type = ActivityTypes.WhatsAppOutbound,
                                        Description = agentReply,
                                        ContactId = freshContact.Id,
                                        Wamid = replyWamid,
                                        CreatedAt = now2
                                    });
                                    await scopedDb.SaveChangesAsync();

                                    var capturedMsg = combined;
                                    var capturedReply = agentReply;
                                    var capturedMemory = memory;
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using var extractScope = capturedFactory.CreateScope();
                                            var extractor = extractScope.ServiceProvider.GetRequiredService<IMemoryExtractorService>();
                                            var repo = extractScope.ServiceProvider.GetRequiredService<IAgentMemoryRepository>();
                                            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                                            var extracted = await extractor.ExtractAsync(capturedMsg, capturedReply, capturedMemory, cts2.Token);
                                            if (extracted is not null)
                                                await repo.UpsertAsync(capturedWaId, capturedMemory.MergeWith(extracted), cts2.Token);
                                        }
                                        catch (Exception ex)
                                        {
                                            capturedLogger.LogWarning(ex, "Memory extraction failed for waId {WaId}", capturedWaId);
                                        }
                                    });
                                }
                                catch (Exception ex)
                                {
                                    capturedLogger.LogWarning(ex, "Debounced agent pipeline error for waId {WaId}", capturedWaId);
                                }
                            }, CancellationToken.None);
                        }
                    }
                    finally { sem.Release(); }
                }
            }
        }

        return Results.Ok(new { received = true });
    }

    static async Task<IResult> SendTemplate(
        string id,
        WhatsAppSendRequest body,
        AppDbContext db,
        IWhatsAppService whatsApp)
    {
        if (string.IsNullOrWhiteSpace(body.TemplateName))
            return Results.BadRequest(new { error = "templateName es requerido" });

        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return Results.NotFound();

        if (string.IsNullOrEmpty(contact.WaId))
            return Results.BadRequest(new { error = "El contacto no tiene wa_id — no recibió mensajes de WhatsApp" });

        if (contact.OptedOut)
            return Results.BadRequest(new { error = "El contacto ha dado opt-out de mensajes WhatsApp" });

        var lang = body.LanguageCode ?? "es";
        var parameters = body.Parameters ?? [];

        bool success;
        string? messageId;
        try
        {
            (success, messageId) = await whatsApp.SendTemplateAsync(contact.WaId, body.TemplateName, lang, parameters);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 503);
        }

        if (!success)
            return Results.Json(new { error = "Meta API rechazó el mensaje" }, statusCode: 502);

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        db.Activities.Add(new Activity
        {
            Id = Guid.NewGuid().ToString(),
            Type = ActivityTypes.WhatsAppOutbound,
            Description = $"Template enviado: {body.TemplateName}",
            ContactId = contact.Id,
            Wamid = messageId,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true, messageId });
    }

    static async Task<IResult> Chat(string id, AppDbContext db)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return Results.NotFound();
        var msgs = await db.Activities
            .Where(a => a.ContactId == id && a.Type!.StartsWith("whatsapp"))
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { dir = a.Type == "whatsapp_inbound" ? "in" : "out", msg = a.Description, ts = a.CreatedAt })
            .ToListAsync();
        return Results.Ok(msgs);
    }

    static async Task<IResult> Handoff(string id, AppDbContext db)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return Results.NotFound();
        contact.BotHandling = false;
        contact.UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true, botHandling = false });
    }

    static async Task<IResult> BotResume(string id, AppDbContext db)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return Results.NotFound();
        contact.BotHandling = true;
        contact.UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true, botHandling = true });
    }

    static string ResolveDisplayName(JsonElement contactsEl, string waId)
    {
        if (contactsEl.ValueKind != JsonValueKind.Array) return waId;
        foreach (var el in contactsEl.EnumerateArray())
        {
            if (el.TryGetProperty("wa_id", out var cWaId) && cWaId.GetString() == waId
                && el.TryGetProperty("profile", out var profile)
                && profile.TryGetProperty("name", out var nameEl))
                return nameEl.GetString() ?? waId;
        }
        return waId;
    }

    static bool ValidateHmac(string body, string secret, string? signature)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var provided = signature[prefix.Length..].ToLowerInvariant();

        var computed = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))
        ).ToLowerInvariant();

        if (provided.Length != computed.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(provided));
    }
}
