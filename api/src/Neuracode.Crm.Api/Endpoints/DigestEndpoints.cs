using System.Globalization;
using System.Net.Http.Headers;
using Neuracode.Crm.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Neuracode.Crm.Api.Endpoints;

public static class DigestEndpoints
{
    public static IEndpointRouteBuilder MapDigestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/digest", SendDigest).WithTags("digest");
        return app;
    }

    static async Task<IResult> SendDigest(
        AppDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        var apiKey = config["RESEND_API_KEY"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
        var email = config["DIGEST_EMAIL"] ?? Environment.GetEnvironmentVariable("DIGEST_EMAIL");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(email))
            return Results.BadRequest(new
            {
                error = "Email digest no configurado",
                instructions = new[]
                {
                    "1. Registrate en https://resend.com (gratis)",
                    "2. Crea un API key en el dashboard",
                    "3. Agrega a .env.local:",
                    "   RESEND_API_KEY=re_...",
                    "   DIGEST_EMAIL=tu@email.com",
                    "4. Reinicia el servidor dev"
                }
            });

        var allContacts = await db.Contacts.ToListAsync();
        var allDeals = await db.Deals.ToListAsync();
        var stages = await db.PipelineStages.OrderBy(s => s.Order).ToListAsync();
        var pendingActivities = await db.Activities
            .Where(a => a.CompletedAt == null)
            .Select(a => new { a.Id, a.Type, a.Description, a.ScheduledAt, ContactName = a.Contact != null ? a.Contact.Name : null })
            .ToListAsync();

        var nowTs = (long)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var overdue = pendingActivities.Where(a => a.ScheduledAt.HasValue && a.ScheduledAt.Value < nowTs).ToList();
        var hotLeads = allContacts.Where(c => c.Temperature == "hot").ToList();
        var wonLostIds = stages.Where(s => s.IsWon == 1 || s.IsLost == 1).Select(s => s.Id).ToHashSet();
        var activeDeals = allDeals.Where(d => !wonLostIds.Contains(d.StageId)).ToList();
        var pipelineValue = activeDeals.Sum(d => d.Value);

        var today = DateTime.UtcNow.ToString("dddd, d 'de' MMMM", new CultureInfo("es-MX"));
        var overdueHtml = overdue.Count > 0
            ? $"<div style='background:#fef2f2;border:1px solid #fecaca;border-radius:8px;padding:16px;margin-bottom:16px;'>" +
              $"<h2 style='color:#dc2626;font-size:16px;margin:0 0 8px;'>Seguimientos vencidos ({overdue.Count})</h2>" +
              $"<ul style='margin:0;padding-left:20px;color:#991b1b;'>" +
              string.Join("", overdue.Select(a => $"<li>{a.Description} — {a.ContactName ?? "Sin contacto"}</li>")) +
              "</ul></div>"
            : "";

        var html = $"<div style='font-family:sans-serif;max-width:600px;margin:0 auto;padding:20px;'>" +
                   $"<h1 style='color:#1e293b;'>Neuracode CRM</h1>" +
                   $"<p style='color:#64748b;'>Resumen diario — {today}</p><hr/>" +
                   overdueHtml +
                   $"<p>Contactos: {allContacts.Count} | Deals activos: {activeDeals.Count} | Pipeline: {pipelineValue / 100m:C}</p>" +
                   "</div>";

        var subject = overdue.Count > 0
            ? $"CRM Digest: {overdue.Count} vencidos"
            : $"CRM Digest: {activeDeals.Count} deals activos";

        var from = config["DIGEST_FROM"] ?? Environment.GetEnvironmentVariable("DIGEST_FROM") ?? "Neuracode CRM <onboarding@resend.dev>";

        try
        {
            var http = httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new { from, to = new[] { email }, subject, html };
            var response = await http.PostAsJsonAsync("https://api.resend.com/emails", payload);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return Results.Problem($"Error de Resend: {err}", statusCode: 500);
            }

            var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return Results.Ok(new
            {
                success = true,
                emailId = result.GetProperty("id").GetString(),
                sentTo = email,
                summary = new { overdue = overdue.Count, hotLeads = hotLeads.Count, activeDeals = activeDeals.Count, pipelineValue }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error enviando email: {ex.Message}", statusCode: 500);
        }
    }
}
