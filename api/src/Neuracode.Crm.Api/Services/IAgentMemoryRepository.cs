using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public interface IAgentMemoryRepository
{
    Task<AgentMemoryData> GetAsync(string waId, CancellationToken ct = default);
    Task UpsertAsync(string waId, AgentMemoryData data, CancellationToken ct = default);
}
