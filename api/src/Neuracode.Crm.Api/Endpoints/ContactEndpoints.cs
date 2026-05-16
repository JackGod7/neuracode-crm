using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Domain;
using Neuracode.Crm.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class ContactEndpoints
{
    static readonly HashSet<string> ValidTemperatures = new(StringComparer.OrdinalIgnoreCase) { "cold", "warm", "hot" };

    static IResult? ValidateEnums(string? temperature, string? source)
    {
        if (temperature is not null && !ValidTemperatures.Contains(temperature))
            return Results.BadRequest(new { error = $"Temperatura inválida: '{temperature}'. Válidos: cold, warm, hot" });
        if (source is not null && !LeadSource.IsValid(source))
            return Results.BadRequest(new { error = $"Fuente inválida: '{source}'. Válidos: {string.Join(", ", LeadSource.AllCodes)}" });
        return null;
    }

    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contacts", GetContacts).WithTags("contacts");
        app.MapPost("/api/contacts", CreateContact).WithTags("contacts");
        app.MapGet("/api/contacts/{id}", GetContactById).WithTags("contacts");
        app.MapPut("/api/contacts/{id}", UpdateContact).WithTags("contacts");
        app.MapDelete("/api/contacts/{id}", DeleteContact).WithTags("contacts");
        return app;
    }

    static async Task<IResult> GetContacts(
        AppDbContext db,
        string? search = null,
        string? temperature = null,
        string? source = null)
    {
        var query = db.Contacts.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{search}%") ||
                (c.Email != null && EF.Functions.Like(c.Email, $"%{search}%")) ||
                (c.Company != null && EF.Functions.Like(c.Company, $"%{search}%")));

        if (!string.IsNullOrEmpty(temperature))
            query = query.Where(c => c.Temperature == temperature);

        if (!string.IsNullOrEmpty(source))
            query = query.Where(c => c.Source == source);

        var contacts = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => ToDto(c))
            .ToListAsync();

        return Results.Ok(contacts);
    }

    static async Task<IResult> CreateContact(CreateContactDto body, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "El nombre es requerido" });

        var enumError = ValidateEnums(body.Temperature, body.Source);
        if (enumError is not null) return enumError;

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var contact = new Contact
        {
            Id = Guid.NewGuid().ToString(),
            Name = body.Name,
            Email = body.Email,
            Phone = body.Phone,
            Company = body.Company,
            Source = body.Source ?? "otro",
            Temperature = body.Temperature ?? "cold",
            Score = body.Score ?? 0,
            Notes = body.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        return Results.Created($"/api/contacts/{contact.Id}", ToDto(contact));
    }

    static async Task<IResult> GetContactById(string id, AppDbContext db)
    {
        var contact = await db.Contacts.FindAsync(id);
        return contact is null ? Results.NotFound() : Results.Ok(ToDto(contact));
    }

    static async Task<IResult> UpdateContact(string id, UpdateContactDto body, AppDbContext db)
    {
        var enumError = ValidateEnums(body.Temperature, body.Source);
        if (enumError is not null) return enumError;

        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return Results.NotFound();

        if (body.Name is not null) contact.Name = body.Name;
        if (body.Email is not null) contact.Email = body.Email;
        if (body.Phone is not null) contact.Phone = body.Phone;
        if (body.Company is not null) contact.Company = body.Company;
        if (body.Source is not null) contact.Source = body.Source;
        if (body.Temperature is not null) contact.Temperature = body.Temperature;
        if (body.Score is not null) contact.Score = body.Score.Value;
        if (body.Notes is not null) contact.Notes = body.Notes;
        contact.UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await db.SaveChangesAsync();
        return Results.Ok(ToDto(contact));
    }

    static async Task<IResult> DeleteContact(string id, AppDbContext db)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return Results.NotFound();

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    static ContactDto ToDto(Contact c) => new(
        c.Id, c.Name, c.Email, c.Phone, c.Company,
        c.Source, c.Temperature, c.Score, c.Notes,
        c.WaId, c.OptedOut, c.BotHandling,
        c.CreatedAt, c.UpdatedAt);
}
