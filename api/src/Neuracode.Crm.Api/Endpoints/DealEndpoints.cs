using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class DealEndpoints
{
    public static IEndpointRouteBuilder MapDealEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/deals", GetDeals).WithTags("deals");
        app.MapPost("/api/deals", CreateDeal).WithTags("deals");
        app.MapGet("/api/deals/{id}", GetDealById).WithTags("deals");
        app.MapPut("/api/deals/{id}", UpdateDeal).WithTags("deals");
        app.MapDelete("/api/deals/{id}", DeleteDeal).WithTags("deals");
        return app;
    }

    static async Task<IResult> GetDeals(AppDbContext db)
    {
        var deals = await db.Deals
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DealListDto(
                d.Id, d.Title, d.Value, d.StageId, d.ContactId,
                d.ExpectedClose, d.Probability, d.Notes, d.CreatedAt, d.UpdatedAt,
                d.Contact != null ? d.Contact.Name : null,
                d.Contact != null ? d.Contact.Email : null,
                d.Contact != null ? d.Contact.Temperature : null,
                d.Stage != null ? d.Stage.Name : null,
                d.Stage != null ? d.Stage.Color : null,
                d.Stage != null ? (int?)d.Stage.Order : null,
                d.Stage != null ? (int?)d.Stage.IsWon : null,
                d.Stage != null ? (int?)d.Stage.IsLost : null))
            .ToListAsync();

        return Results.Ok(deals);
    }

    static async Task<IResult> CreateDeal(CreateDealDto body, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.ContactId))
            return Results.BadRequest(new { error = "Titulo y contacto son requeridos" });

        var stageId = body.StageId;
        if (string.IsNullOrEmpty(stageId))
        {
            var first = await db.PipelineStages.OrderBy(s => s.Order).FirstOrDefaultAsync();
            stageId = first?.Id;
        }

        if (string.IsNullOrEmpty(stageId))
            return Results.BadRequest(new { error = "No hay etapas de pipeline configuradas" });

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deal = new Deal
        {
            Id = Guid.NewGuid().ToString(),
            Title = body.Title,
            Value = body.Value ?? 0,
            StageId = stageId,
            ContactId = body.ContactId,
            ExpectedClose = body.ExpectedClose,
            Probability = Math.Clamp(body.Probability ?? 0, 0, 100),
            Notes = body.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        return Results.Created($"/api/deals/{deal.Id}", ToDto(deal));
    }

    static async Task<IResult> GetDealById(string id, AppDbContext db)
    {
        var deal = await db.Deals.FindAsync(id);
        return deal is null ? Results.NotFound() : Results.Ok(ToDto(deal));
    }

    static async Task<IResult> UpdateDeal(string id, UpdateDealDto body, AppDbContext db)
    {
        var deal = await db.Deals.FindAsync(id);
        if (deal is null) return Results.NotFound();

        if (body.Title is not null) deal.Title = body.Title;
        if (body.ContactId is not null) deal.ContactId = body.ContactId;
        if (body.StageId is not null) deal.StageId = body.StageId;
        if (body.Value is not null) deal.Value = body.Value.Value;
        if (body.Probability is not null) deal.Probability = Math.Clamp(body.Probability.Value, 0, 100);
        if (body.ExpectedClose is not null) deal.ExpectedClose = body.ExpectedClose;
        if (body.Notes is not null) deal.Notes = body.Notes;
        deal.UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await db.SaveChangesAsync();
        return Results.Ok(ToDto(deal));
    }

    static async Task<IResult> DeleteDeal(string id, AppDbContext db)
    {
        var deal = await db.Deals.FindAsync(id);
        if (deal is null) return Results.NotFound();

        db.Deals.Remove(deal);
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    static DealDto ToDto(Deal d) => new(
        d.Id, d.Title, d.Value, d.StageId, d.ContactId,
        d.ExpectedClose, d.Probability, d.Notes, d.CreatedAt, d.UpdatedAt);
}
