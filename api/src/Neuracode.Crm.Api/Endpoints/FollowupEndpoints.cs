using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class FollowupEndpoints
{
    public static IEndpointRouteBuilder MapFollowupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/followups", GetFollowups).WithTags("followups");
        return app;
    }

    static async Task<IResult> GetFollowups(AppDbContext db)
    {
        var pending = await db.Activities
            .Where(a => a.CompletedAt == null)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new FollowupItemDto(
                a.Id, a.Type, a.Description, a.ContactId, a.DealId,
                a.ScheduledAt, a.CompletedAt, a.CreatedAt,
                a.Contact != null ? a.Contact.Name : null,
                a.Contact != null ? a.Contact.Company : null))
            .ToListAsync();

        var nowTs = (long)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startOfDay = (nowTs / 86400) * 86400;
        var endOfDay = startOfDay + 86400;

        var buckets = new FollowupBucketsDto(
            Overdue: pending.Where(f => f.ScheduledAt.HasValue && f.ScheduledAt.Value < startOfDay).ToList(),
            Today: pending.Where(f => f.ScheduledAt.HasValue && f.ScheduledAt.Value >= startOfDay && f.ScheduledAt.Value < endOfDay).ToList(),
            Upcoming: pending.Where(f => f.ScheduledAt.HasValue && f.ScheduledAt.Value >= endOfDay).ToList(),
            Unscheduled: pending.Where(f => !f.ScheduledAt.HasValue).ToList()
        );

        return Results.Ok(buckets);
    }
}
