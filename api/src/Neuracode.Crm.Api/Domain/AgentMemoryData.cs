using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuracode.Crm.Api.Domain;

public sealed record AgentMemoryData(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("budget")] string? Budget,
    [property: JsonPropertyName("interested_in")] IReadOnlyList<string> InterestedIn,
    [property: JsonPropertyName("sales_state")] string SalesState,
    [property: JsonPropertyName("notes")] string? Notes)
{
    public static readonly AgentMemoryData Empty =
        new(null, null, [], "browsing", null);

    public bool IsEmpty =>
        Name is null && Budget is null && InterestedIn.Count == 0 && Notes is null;

    public AgentMemoryData MergeWith(AgentMemoryData extracted) => new(
        Name: extracted.Name ?? Name,
        Budget: extracted.Budget ?? Budget,
        InterestedIn: extracted.InterestedIn.Count > 0
            ? InterestedIn.Union(extracted.InterestedIn).Distinct().ToList()
            : InterestedIn,
        SalesState: extracted.SalesState != "browsing" ? extracted.SalesState : SalesState,
        Notes: extracted.Notes ?? Notes);

    public string ToPromptString()
    {
        if (IsEmpty) return "{}";
        var parts = new List<string>();
        if (Name is not null) parts.Add($"nombre: {Name}");
        if (Budget is not null) parts.Add($"presupuesto: {Budget}");
        if (InterestedIn.Count > 0) parts.Add($"interesado en: {string.Join(", ", InterestedIn)}");
        parts.Add($"estado venta: {SalesState}");
        if (Notes is not null) parts.Add($"notas: {Notes}");
        return string.Join(" | ", parts);
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static AgentMemoryData? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<AgentMemoryData>(json); }
        catch { return null; }
    }
}
