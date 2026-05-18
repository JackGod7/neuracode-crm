using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public record WhatsAppSendRequest(string TemplateName, string? LanguageCode, string[]? Parameters);

public static class WhatsAppEndpoints
{
    const string DefaultBusinessPrompt =
        "Eres el asistente de atención al cliente. Respondes en español, amable y conciso. " +
        "Puedes informar sobre: horarios, métodos de pago, envíos a todo el país (2-3 días hábiles), " +
        "política de devoluciones (7 días). " +
        "Si el cliente pregunta precios específicos o quiere coordinar una compra, " +
        "di que un asesor le contactará pronto. " +
        "Si el cliente pide hablar con una persona, responde ÚNICAMENTE con la palabra: ESCALAR. " +
        "Máximo 2 oraciones. Sin emojis.";

    static readonly ConcurrentDictionary<string, SemaphoreSlim> _contactLocks = new();

    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/webhooks/whatsapp", VerifyWebhook).WithTags("whatsapp");
        app.MapPost("/api/webhooks/whatsapp", HandleInbound).WithTags("whatsapp");
        app.MapPost("/api/contacts/{id}/whatsapp/send", SendTemplate).WithTags("whatsapp");
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
        IAgentService agentService, IWhatsAppService whatsApp)
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
                            Type = "whatsapp_inbound",
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
                            Direction = "inbound",
                            Body = msgBody,
                            Status = "received",
                            CreatedAt = now
                        });

                        await db.SaveChangesAsync();

                        // Agent hook — never propagates exceptions, bounded by 3s timeout
                        if (contact.BotHandling && agentService.IsConfigured && !contact.OptedOut)
                        {
                            try
                            {
                                var recentMsgs = await db.Activities
                                    .Where(a => a.ContactId == contact.Id && a.Type!.StartsWith("whatsapp"))
                                    .OrderByDescending(a => a.CreatedAt)
                                    .Take(5)
                                    .Select(a => (a.Type == "whatsapp_inbound" ? "Cliente" : "Bot") + ": " + a.Description)
                                    .ToListAsync();

                                var businessPrompt = config["AGENT_BUSINESS_PROMPT"] ?? DefaultBusinessPrompt;
                                var ctx = new AgentContext(contact.Id, waId, displayName, msgBody, recentMsgs, businessPrompt);
                                var agentReply = await agentService.HandleAsync(ctx);

                                if (agentReply is not null)
                                {
                                    var (sendSuccess, replyWamid) = await whatsApp.SendTextAsync(waId, agentReply);
                                    if (sendSuccess)
                                    {
                                        db.Activities.Add(new Activity
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            Type = "whatsapp_outbound",
                                            Description = agentReply,
                                            ContactId = contact.Id,
                                            Wamid = replyWamid,
                                            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                        });
                                        await db.SaveChangesAsync();
                                    }
                                }
                            }
                            catch { /* agent must never break webhook */ }
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
            Type = "whatsapp_outbound",
            Description = $"Template enviado: {body.TemplateName}",
            ContactId = contact.Id,
            Wamid = messageId,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true, messageId });
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
