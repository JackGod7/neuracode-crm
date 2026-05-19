using Microsoft.EntityFrameworkCore;
using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Api.Services;

public sealed class ConversationWindowService(AppDbContext db) : IConversationWindowService
{
    public async Task<ConversationWindow> GetWindowAsync(string waId)
    {
        var lastInboundTs = await db.WhatsAppMessages
            .Where(m => m.WaId == waId && m.Direction == WaDirection.Inbound)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (int?)m.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastInboundTs is null)
            return new ConversationWindow(waId, false, null, null);

        var lastInboundAt = DateTimeOffset.FromUnixTimeSeconds(lastInboundTs.Value).UtcDateTime;
        var expiresAt = lastInboundAt.AddHours(24);
        var now = DateTime.UtcNow;
        var isOpen = now < expiresAt;
        var secondsRemaining = isOpen ? (int)(expiresAt - now).TotalSeconds : (int?)null;

        return new ConversationWindow(waId, isOpen, isOpen ? expiresAt : null, secondsRemaining);
    }
}
