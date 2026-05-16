using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Domain;
using Neuracode.Crm.Api.Dtos;

namespace Neuracode.Crm.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/import", ImportContacts).WithTags("import");
        return app;
    }

    static async Task<IResult> ImportContacts(ImportRequestDto body, AppDbContext db)
    {
        if (body.Contacts is null || body.Contacts.Count == 0)
            return Results.BadRequest(new { error = "Se requiere un array de contactos" });

        if (body.Contacts.Count > 10_000)
            return Results.BadRequest(new { error = "Máximo 10,000 contactos por importación" });

        var imported = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var item in body.Contacts)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                failed++;
                errors.Add($"Contacto sin nombre: {System.Text.Json.JsonSerializer.Serialize(item)}");
                continue;
            }

            var draft = new LeadDraft(
                Name: item.Name,
                Email: item.Email,
                Phone: item.Phone,
                Company: item.Company,
                Source: item.Source ?? "import",
                Temperature: item.Temperature ?? "cold",
                Score: item.Score ?? 0,
                Notes: item.Notes
            );

            var (contact, activity) = LeadIntake.Build(draft, "Contacto importado");
            db.Contacts.Add(contact);
            db.Activities.Add(activity);
            imported++;
        }

        if (imported > 0)
            await db.SaveChangesAsync();

        var result = new ImportResultDto(imported, failed, errors);
        return failed > 0
            ? Results.Json(result, statusCode: 207)
            : Results.Json(result, statusCode: 201);
    }
}
