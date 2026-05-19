# L3 — Tool Use: Product Price + Stock Lookup

**Status**: Draft | **Date**: 2026-05-18 | **Owner**: Jack Aguilar | **Track**: T1+T2

## Requirements

1. Agent calls `get_product_price(sku)` when client asks price of specific collection
2. Agent calls `check_stock(sku)` when client asks availability
3. Product catalog stored as JSON in `CrmSettings` key `product_catalog`; fallback to hardcoded default
4. Tool use loop: max 1 round-trip (1 tool call → 1 final answer)
5. Tests: unit tests for catalog parsing + tool dispatch (no API key required)

## Entities

| Name | Fields |
|------|--------|
| `ProductEntry` | Sku, Name, PriceMin, PriceMax, Currency, Category, InStock |
| `ProductCatalog` | static: Default[], Parse(json), Find(catalog,sku), ExecuteToolCall(name,input,catalog) |

## Approach

```
client msg → AgentService.HandleAsync
  → first Anthropic call (tools=[get_product_price, check_stock])
  → if stop_reason=="tool_use":
      → ExecuteToolCall per tool_use block
      → second Anthropic call with tool_results
  → extract text from final response
```

## Structure

```
Domain/ProductCatalog.cs          ← NEW: model + static catalog + tool dispatch
Services/AgentService.cs          ← UPDATE: tool definitions + loop
Endpoints/WhatsAppEndpoints.cs    ← UPDATE: load catalog, pass to AgentContext
UnitTests/ProductCatalogTests.cs  ← NEW: parse + find + execute unit tests
```

## Operations

**get_product_price(sku)**
- Pre: sku is one of METROPOLE, BOSS, E.ARMANI, CROCODILE, ANGEL_EYES, COMBOS_LOVE
- Input: `{"sku": "BOSS"}`
- Output: `{"sku":"BOSS","name":"Brazalete BOSS","price_min":279,"price_max":389,"currency":"PEN","in_stock":true}`
- Post: agent uses price to answer client

**check_stock(sku)**
- Output: `{"sku":"BOSS","in_stock":true,"name":"Brazalete BOSS"}`

## Norms

N1. Tool use loop max 1 round-trip — prevent runaway API calls
N2. Unknown SKU → `{"error":"Producto 'X' no encontrado"}` — agent handles gracefully
N3. CrmSettings key `product_catalog` stores JSON array of ProductEntry — operator-updateable

## Safeguards

S1. Tool dispatch wrapped in try/catch — returns error JSON, never throws
S2. Tool use only triggered when catalog != null or Default used — always available
