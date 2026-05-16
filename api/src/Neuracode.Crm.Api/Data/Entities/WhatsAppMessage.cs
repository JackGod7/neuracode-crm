namespace Neuracode.Crm.Api.Data.Entities;

public class WhatsAppMessage
{
    public string Id { get; set; } = null!;
    public string WaId { get; set; } = null!;
    public string Wamid { get; set; } = null!;
    public string Direction { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int CreatedAt { get; set; }
}
