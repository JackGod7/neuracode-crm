using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuracode.Crm.Api.Domain;

public sealed record ProductEntry(
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("price_min")] int PriceMin,
    [property: JsonPropertyName("price_max")] int PriceMax,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("in_stock")] bool InStock);

public static class ProductCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public const string SettingsKey = "product_catalog";

    public static readonly ProductEntry[] Default =
    [
        new("METROPOLE",   "Brazalete METROPOLE",              149, 249, "PEN", "caballero", true),
        new("BOSS",        "Brazalete BOSS",                   279, 389, "PEN", "caballero", true),
        new("E.ARMANI",    "Brazalete E.ARMANI",               349, 449, "PEN", "caballero", true),
        new("CROCODILE",   "Brazalete CROCODILE",              449, 607, "PEN", "caballero", true),
        new("ANGEL_EYES",  "Pulsera ANGEL EYES",               149, 249, "PEN", "dama",      true),
        new("COMBOS_LOVE", "Combo Love (brazalete + pulsera)", 249, 449, "PEN", "parejas",   true),
    ];

    public static ProductEntry[] Parse(string json)
    {
        try { return JsonSerializer.Deserialize<ProductEntry[]>(json, Opts) ?? Default; }
        catch { return Default; }
    }

    public static ProductEntry? Find(IEnumerable<ProductEntry> catalog, string sku) =>
        catalog.FirstOrDefault(p => p.Sku.Equals(sku.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase)
                                 || p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));

    public static string ExecuteToolCall(string toolName, JsonElement input, ProductEntry[] catalog)
    {
        try
        {
            return toolName switch
            {
                "get_product_price" => GetPrice(input, catalog),
                "check_stock" => GetStock(input, catalog),
                _ => """{"error":"herramienta no reconocida"}"""
            };
        }
        catch { return """{"error":"error interno al consultar catálogo"}"""; }
    }

    private static string GetPrice(JsonElement input, ProductEntry[] catalog)
    {
        if (!input.TryGetProperty("sku", out var skuEl)) return """{"error":"sku requerido"}""";
        var sku = skuEl.GetString() ?? "";
        var p = Find(catalog, sku);
        if (p is null) return $$$"""{"error":"Producto '{{{sku}}}' no encontrado en catálogo"}""";
        return $$$"""{"sku":"{{{p.Sku}}}","name":"{{{p.Name}}}","price_min":{{{p.PriceMin}}},"price_max":{{{p.PriceMax}}},"currency":"{{{p.Currency}}}","in_stock":{{{p.InStock.ToString().ToLower()}}},"category":"{{{p.Category}}}"}""";
    }

    private static string GetStock(JsonElement input, ProductEntry[] catalog)
    {
        if (!input.TryGetProperty("sku", out var skuEl)) return """{"error":"sku requerido"}""";
        var sku = skuEl.GetString() ?? "";
        var p = Find(catalog, sku);
        if (p is null) return $$$"""{"error":"Producto '{{{sku}}}' no encontrado"}""";
        return $$$"""{"sku":"{{{p.Sku}}}","name":"{{{p.Name}}}","in_stock":{{{p.InStock.ToString().ToLower()}}}}""";
    }
}
