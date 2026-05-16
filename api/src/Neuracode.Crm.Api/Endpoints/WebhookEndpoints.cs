using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class WebhookEndpoints
{
    private static readonly Dictionary<string, string> FieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "name",
        ["nombre"] = "name",
        ["full_name"] = "name",
        ["fullname"] = "name",
        ["first_name"] = "name",
        ["nombre_completo"] = "name",
        ["email"] = "email",
        ["correo"] = "email",
        ["email_address"] = "email",
        ["correo_electronico"] = "email",
        ["phone"] = "phone",
        ["telefono"] = "phone",
        ["phone_number"] = "phone",
        ["cel"] = "phone",
        ["celular"] = "phone",
        ["whatsapp"] = "phone",
        ["movil"] = "phone",
        ["company"] = "company",
        ["empresa"] = "company",
        ["company_name"] = "company",
        ["negocio"] = "company",
        ["organizacion"] = "company",
        ["notes"] = "notes",
        ["notas"] = "notes",
        ["message"] = "notes",
        ["mensaje"] = "notes",
        ["comments"] = "notes",
        ["comentarios"] = "notes",
        ["descripcion"] = "notes"
    };

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhook", HandleWebhook).WithTags("webhook");
        return app;
    }

    static async Task<IResult> HandleWebhook(
        HttpRequest request,
        AppDbContext db)
    {
        var stored = await db.CrmSettings
            .FirstOrDefaultAsync(s => s.Key == "webhook_secret");

        if (stored is not null)
        {
            var header = request.Headers["x-webhook-secret"].FirstOrDefault();
            if (string.IsNullOrEmpty(header) || header != stored.Value)
                return Results.Json(new { error = "Secret invalido o faltante" }, statusCode: 401);
        }

        System.Text.Json.JsonElement payload;
        try
        {
            payload = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.JsonElement>(request.Body);
        }
        catch
        {
            return Results.BadRequest(new { error = "JSON invalido" });
        }

        var fields = ExtractFields(payload);

        if (!fields.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return Results.BadRequest(new
            {
                error = "Campo 'name' o 'nombre' es requerido",
                hint = "Campos soportados: name, nombre, full_name, email, correo, phone, telefono, company, empresa, notes, notas, message"
            });

        var company = fields.GetValueOrDefault("company");
        var draft = new LeadDraft(
            Name: name,
            Email: fields.GetValueOrDefault("email"),
            Phone: fields.GetValueOrDefault("phone"),
            Company: company,
            Source: "webhook",
            Notes: fields.GetValueOrDefault("notes")
        );

        var contact = await LeadIntake.IngestAsync(db, draft,
            $"Lead recibido via webhook{(company is not null ? $" ({company})" : "")}");

        return Results.Created($"/api/contacts/{contact.Id}", new
        {
            success = true,
            contact = new { id = contact.Id, name = contact.Name, email = contact.Email, source = contact.Source }
        });
    }

    static Dictionary<string, string> ExtractFields(System.Text.Json.JsonElement payload)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Handle Typeform-style nested data
        var data = payload.ValueKind == System.Text.Json.JsonValueKind.Object &&
                   payload.TryGetProperty("data", out var nested) &&
                   nested.ValueKind == System.Text.Json.JsonValueKind.Object
            ? nested
            : payload;

        if (data.ValueKind != System.Text.Json.JsonValueKind.Object)
            return result;

        foreach (var prop in data.EnumerateObject())
        {
            if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.String &&
                prop.Value.ValueKind != System.Text.Json.JsonValueKind.Number)
                continue;

            var key = prop.Name.ToLowerInvariant().Trim().Replace(' ', '_');
            if (FieldMap.TryGetValue(key, out var mapped) && !result.ContainsKey(mapped))
                result[mapped] = prop.Value.ToString().Trim();
        }

        // first_name + last_name → name
        if (!result.ContainsKey("name") &&
            data.TryGetProperty("first_name", out var fn) &&
            fn.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var fullName = fn.GetString()!;
            if (data.TryGetProperty("last_name", out var ln) && ln.ValueKind == System.Text.Json.JsonValueKind.String)
                fullName = $"{fullName} {ln.GetString()}".Trim();
            result["name"] = fullName;
        }

        return result;
    }
}
