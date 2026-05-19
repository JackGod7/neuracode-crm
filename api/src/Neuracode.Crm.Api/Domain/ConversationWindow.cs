namespace Neuracode.Crm.Api.Domain;

public record ConversationWindow(string WaId, bool IsOpen, DateTime? ExpiresAt, int? SecondsRemaining);
