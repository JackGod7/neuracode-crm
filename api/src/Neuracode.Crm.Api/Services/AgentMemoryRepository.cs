using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;
using Neuracode.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Services;

public sealed class AgentMemoryRepository(AppDbContext db) : IAgentMemoryRepository
{
    public async Task<AgentMemoryData> GetAsync(string waId, CancellationToken ct = default)
    {
        var row = await db.AgentMemories.FirstOrDefaultAsync(m => m.WaId == waId, ct);
        if (row is null) return AgentMemoryData.Empty;
        return AgentMemoryData.FromJson(row.Data) ?? AgentMemoryData.Empty;
    }

    public async Task UpsertAsync(string waId, AgentMemoryData data, CancellationToken ct = default)
    {
        var row = await db.AgentMemories.FirstOrDefaultAsync(m => m.WaId == waId, ct);
        var json = data.ToJson();
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (row is null)
        {
            db.AgentMemories.Add(new AgentMemory { WaId = waId, Data = json, UpdatedAt = now });
        }
        else
        {
            row.Data = json;
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
