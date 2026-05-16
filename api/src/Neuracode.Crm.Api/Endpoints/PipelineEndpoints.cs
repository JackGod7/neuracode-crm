using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class PipelineEndpoints
{
    public static IEndpointRouteBuilder MapPipelineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/pipeline", GetPipeline).WithTags("pipeline");
        app.MapPut("/api/pipeline", UpdatePipeline).WithTags("pipeline");
        return app;
    }

    static async Task<IResult> GetPipeline(AppDbContext db)
    {
        var stages = await db.PipelineStages
            .OrderBy(s => s.Order)
            .ToListAsync();

        var deals = await db.Deals
            .Select(d => new PipelineDealDto(
                d.Id, d.Title, d.Value, d.StageId, d.ContactId,
                d.ExpectedClose, d.Probability, d.Notes, d.CreatedAt, d.UpdatedAt,
                d.Contact != null ? d.Contact.Name : null,
                d.Contact != null ? d.Contact.Temperature : null))
            .ToListAsync();

        var result = stages.Select(s => new PipelineStageDto(
            s.Id, s.Name, s.Order, s.Color, s.IsWon, s.IsLost,
            deals.Where(d => d.StageId == s.Id).ToList()))
            .ToList();

        return Results.Ok(result);
    }

    static async Task<IResult> UpdatePipeline(PipelinePutDto body, AppDbContext db)
    {
        // Move deal
        if (!string.IsNullOrEmpty(body.DealId) && !string.IsNullOrEmpty(body.StageId))
        {
            var deal = await db.Deals.FindAsync(body.DealId);
            if (deal is null) return Results.NotFound();

            deal.StageId = body.StageId;
            deal.UpdatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.SaveChangesAsync();

            return Results.Ok(new DealDto(
                deal.Id, deal.Title, deal.Value, deal.StageId, deal.ContactId,
                deal.ExpectedClose, deal.Probability, deal.Notes, deal.CreatedAt, deal.UpdatedAt));
        }

        // Bulk stage replacement
        if (body.Stages is { Count: > 0 })
        {
            var hasDeals = await db.Deals.AnyAsync();
            if (hasDeals)
                return Results.BadRequest(new { error = "No se pueden reemplazar etapas cuando hay deals activos. Elimina los deals primero." });

            await using var tx = await db.Database.BeginTransactionAsync();
            db.PipelineStages.RemoveRange(db.PipelineStages);
            await db.SaveChangesAsync();

            var newStages = body.Stages.Select(s => new PipelineStage
            {
                Id = Guid.NewGuid().ToString(),
                Name = s.Name,
                Order = s.Order,
                Color = s.Color ?? "#64748b",
                IsWon = s.IsWon == true ? 1 : 0,
                IsLost = s.IsLost == true ? 1 : 0
            }).ToList();

            db.PipelineStages.AddRange(newStages);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var result = await db.PipelineStages
                .OrderBy(s => s.Order)
                .ToListAsync();

            return Results.Ok(result);
        }

        return Results.BadRequest(new { error = "Request invalido" });
    }
}
