using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class ClassifyEndpoints
{
    public static IEndpointRouteBuilder MapClassifyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/classify", Classify).WithTags("classify");
        return app;
    }

    static async Task<IResult> Classify(ClassifyRequestDto body, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(body.ContactId))
            return Results.BadRequest(new { error = "contactId es requerido" });

        var contact = await db.Contacts.FindAsync(body.ContactId);
        if (contact is null) return Results.NotFound(new { error = "Contacto no encontrado" });

        var contactActivities = await db.Activities
            .Where(a => a.ContactId == body.ContactId)
            .ToListAsync();

        var nowTs = (long)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var lastActivity = contactActivities
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        var daysSinceLastActivity = lastActivity is not null
            ? (int)Math.Floor((nowTs - (long)lastActivity.CreatedAt) / 86400.0)
            : 999;

        var score = CalculateLeadScore(
            contact.Temperature,
            contact.Email is not null,
            contact.Phone is not null,
            contact.Company is not null,
            contactActivities.Count,
            daysSinceLastActivity);

        var temperature = SuggestTemperature(score);

        contact.Temperature = temperature;
        contact.Score = score;
        contact.UpdatedAt = (int)nowTs;
        await db.SaveChangesAsync();

        return Results.Ok(new ClassifyResultDto(
            temperature, score,
            "Revisar manualmente y dar seguimiento",
            "Clasificacion basada en reglas (sin API key)",
            "rules"));
    }

    static int CalculateLeadScore(
        string temperature, bool hasEmail, bool hasPhone,
        bool hasCompany, int activityCount, int daysSinceLastActivity)
    {
        var score = temperature switch
        {
            "hot" => 40,
            "warm" => 25,
            _ => 10
        };

        if (hasEmail) score += 10;
        if (hasPhone) score += 10;
        if (hasCompany) score += 5;

        score += Math.Min(activityCount * 5, 20);

        if (daysSinceLastActivity > 30) score -= 15;
        else if (daysSinceLastActivity > 14) score -= 10;
        else if (daysSinceLastActivity > 7) score -= 5;

        return Math.Clamp(score, 0, 100);
    }

    static string SuggestTemperature(int score) =>
        score >= 70 ? "hot" : score >= 40 ? "warm" : "cold";
}
