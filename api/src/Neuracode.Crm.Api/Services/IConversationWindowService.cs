using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public interface IConversationWindowService
{
    Task<ConversationWindow> GetWindowAsync(string waId);
}
