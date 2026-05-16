namespace Neuracode.Crm.Api.Domain;

public static class LeadSource
{
    private static readonly Dictionary<string, string> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["web"] = "Sitio web",
        ["referral"] = "Referido",
        ["linkedin"] = "LinkedIn",
        ["evento"] = "Evento",
        ["otro"] = "Otro",
        ["import"] = "Importado",
        ["webhook"] = "Webhook",
        ["ads"] = "Publicidad",
        ["whatsapp"] = "WhatsApp",
    };

    public static IReadOnlyCollection<string> AllCodes => Catalog.Keys;

    public static bool IsValid(string? code) =>
        code is not null && Catalog.ContainsKey(code);

    public static string GetLabel(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return Catalog.TryGetValue(code, out var label) ? label : code;
    }

}
