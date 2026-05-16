using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/activities", GetActivities).WithTags("activities");
        app.MapPost("/api/activities", CreateActivity).WithTags("activities");
        app.MapPut("/api/activities/{id}", UpdateActivity).WithTags("activities");
        app.MapDelete("/api/activities/{id}", DeleteActivity).WithTags("activities");
        return app;
    }

    static async Task<IResult> GetActivities(
        AppDbContext db,
        string? contactId = null,
        string? dealId = null)
    {
        var query = db.Activities.AsQueryable();

        if (!string.IsNullOrEmpty(contactId))
            query = query.Where(a => a.ContactId == contactId);

        if (!string.IsNullOrEmpty(dealId))
            query = query.Where(a => a.DealId == dealId);

        var results = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ActivityDto(
                a.Id, a.Type, a.Description, a.ContactId, a.DealId,
                a.ScheduledAt, a.CompletedAt, a.CreatedAt,
                a.Contact != null ? a.Contact.Name : null))
            .ToListAsync();

        return Results.Ok(results);
    }

    static async Task<IResult> CreateActivity(CreateActivityDto body, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(body.Type) ||
            string.IsNullOrWhiteSpace(body.Description) ||
            string.IsNullOrWhiteSpace(body.ContactId))
            return Results.BadRequest(new { error = "Tipo, descripcion y contacto son requeridos" });

        var activity = new Activity
        {
            Id = Guid.NewGuid().ToString(),
            Type = body.Type,
            Description = body.Description,
            ContactId = body.ContactId,
            DealId = body.DealId,
            ScheduledAt = body.ScheduledAt,
            CompletedAt = null,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        return Results.Created($"/api/activities/{activity.Id}", ToDto(activity, null));
    }

    static async Task<IResult> UpdateActivity(string id, UpdateActivityDto body, AppDbContext db)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity is null) return Results.NotFound();

        if (body.CompletedAt == true)
            activity.CompletedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (body.Description is not null)
            activity.Description = body.Description;

        if (body.ScheduledAt is not null)
            activity.ScheduledAt = body.ScheduledAt;

        await db.SaveChangesAsync();
        return Results.Ok(ToDto(activity, null));
    }

    static async Task<IResult> DeleteActivity(string id, AppDbContext db)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity is null) return Results.NotFound();

        db.Activities.Remove(activity);
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    static ActivityDto ToDto(Activity a, string? contactName) => new(
        a.Id, a.Type, a.Description, a.ContactId, a.DealId,
        a.ScheduledAt, a.CompletedAt, a.CreatedAt, contactName);
}
