using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public interface IMemoryExtractorService
{
    Task<AgentMemoryData?> ExtractAsync(
        string userMessage,
        string agentResponse,
        AgentMemoryData existing,
        CancellationToken ct = default);
}
