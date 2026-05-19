# Products Catalog

**Status**: Draft
**Date**: 2026-05-18 | **Owner**: Jack Aguilar | **Track**: both

---

## Requirements — qué debe hacer el sistema

1. Tabla `products` en DB almacena el catálogo real del cliente (sku, nombre, precio, stock, imagen).
2. El agente consulta precio exacto por SKU vía tool `get_product_price` → query a DB (no hardcoded).
3. El agente puede devolver `image_url` por producto para futura integración de media WA.
4. Operadores CRUD el catálogo vía REST (`/api/products`).
5. Si la tabla `products` está vacía, el sistema semilla desde `ProductCatalog.Default` (backward compat).
6. `CrmSettings["product_catalog"]` JSON queda deprecated; se migra a tabla en este sprint.

**Out of scope:**
- UI de catálogo en frontend (sprint futuro)
- Multi-tenant: una tabla por instancia Docker (single-tenant by design)
- Imágenes media WA (solo almacenar `image_url`; envío = feature L4-media)
- Gestión de variantes / atributos (color, talla) — catálogo plano por ahora

---

## Entities — objetos de dominio

| Entidad | Campos clave | Notas |
|---------|-------------|-------|
| `Product` | `sku` (UNIQUE), `name`, `price_min`, `price_max`, `currency`, `category`, `in_stock`, `image_url?`, `created_at`, `updated_at` | SKU = clave de negocio. `image_url` nullable hasta sprint L4-media |

### SKUs canónicos (semilla inicial — "Accesorios Para Él")

| SKU | Nombre | PriceMin | PriceMax | Categoría |
|-----|--------|----------|----------|-----------|
| METROPOLE | Brazalete METROPOLE | 149 | 249 | caballero |
| BOSS | Brazalete BOSS | 279 | 389 | caballero |
| E.ARMANI | Brazalete E.ARMANI | 349 | 449 | caballero |
| CROCODILE | Brazalete CROCODILE | 449 | 607 | caballero |
| ANGEL_EYES | Pulsera ANGEL EYES | 149 | 249 | dama |
| COMBOS_LOVE | Combo Love (brazalete + pulsera) | 249 | 449 | parejas |

---

## Approach — cómo funciona

```
Operador
  │
  ▼
POST /api/products  →  Product guardado en DB (validado)
  │
  ▼
Agente recibe pregunta de precio
  │
  ▼
Tool: get_product_price(sku="BOSS")
  │
  ▼
ProductRepository.FindAsync(sku)  →  DB query (indexed por sku)
  │
  ├─ Encontrado  →  {sku, name, price_min, price_max, currency, in_stock, image_url}
  └─ No encontrado  →  {error: "producto no encontrado"}
  │
  ▼
Segunda API call (Anthropic)  →  Respuesta con precio real al cliente
```

**Semilla (startup):**
```
AppDbContext.Products.Any() == false
  → seed ProductCatalog.Default → 6 productos insertados
```

---

## Structure — árbol de archivos

```
api/src/Neuracode.Crm.Api/
├── Domain/
│   ├── Product.cs                  ← EF entity (reemplaza ProductEntry para persistencia)
│   └── ProductCatalog.cs           ← mantener Default[] para seed; ExecuteToolCall → llama repo
├── Endpoints/
│   └── ProductEndpoints.cs         ← CRUD /api/products
└── Data/
    └── AppDbContext.cs             ← + DbSet<Product> Products

api/tests/Neuracode.Crm.Tests/
└── UnitTests/
    └── ProductEndpointTests.cs     ← CRUD + validación + seed behavior
```

---

## Operations — operaciones clave

### O1 — Listar productos
```
PRECONDITION : ninguna
INPUT        : GET /api/products
OUTPUT       : Product[] ordenados por category, price_min asc
POSTCONDITION: ninguna
```

### O2 — Crear producto
```
PRECONDITION : sku no existe en DB
INPUT        : POST /api/products {sku, name, price_min, price_max, currency, category, in_stock, image_url?}
OUTPUT       : 201 Created + Product
POSTCONDITION: Product en DB, sku UNIQUE constraint cumplido
```

### O3 — Actualizar producto
```
PRECONDITION : sku existe
INPUT        : PUT /api/products/{sku} {campos a actualizar}
OUTPUT       : 200 OK + Product actualizado
POSTCONDITION: updated_at renovado
```

### O4 — Tool get_product_price (agente)
```
PRECONDITION : tool_choice=any activo en AgentService
INPUT        : sku string (case-insensitive)
OUTPUT       : {sku, name, price_min, price_max, currency, in_stock, image_url?} o {error}
POSTCONDITION: ninguna (read-only)
```

### O5 — Semilla en startup
```
PRECONDITION : Products table vacía (primera vez o DB nueva)
INPUT        : IStartupFilter.Configure()
OUTPUT       : 6 productos insertados desde ProductCatalog.Default
POSTCONDITION: Products.Count >= 6
```

---

## Norms — restricciones

| # | Norma | Justificación |
|---|-------|--------------|
| N1 | `sku` UNIQUE en DB (case-insensitive collation) | Clave de negocio, duplicados rompen el tool dispatch |
| N2 | `price_min` ≤ `price_max` siempre | Invariante de negocio, validar en endpoint |
| N3 | `currency` = "PEN" por default; no bloquear otros | Futuro: USD para clientes internacionales |
| N4 | `image_url` debe ser HTTPS si se provee | Meta API rechaza URLs no-HTTPS para media |
| N5 | Seed idempotente: insertar solo si tabla vacía | Evita duplicados en restart del container |
| N6 | `ProductCatalog.Default` se mantiene como seed source | Única fuente de los 6 SKUs canónicos |

---

## Safeguards — qué nunca debe ocurrir

| # | Safeguard | Mecanismo |
|---|-----------|-----------|
| S1 | Nunca exponer tabla de productos a usuarios WA | Tool solo devuelve campos de lectura; endpoints CRUD sin auth pública (solo internal/admin) |
| S2 | Nunca responder precio sin consultar DB | `tool_choice=any` + tool `get_product_price` obligatorio vía Anthropic API |
| S3 | Nunca insertar `image_url` con esquema HTTP o localhost | Validación en `ProductEndpoints.cs` antes de `SaveChanges` |
| S4 | Nunca borrar un producto con `DELETE` — solo marcar `in_stock=false` | Audit trail: el SKU puede estar en actividades históricas |

---

## Patrones relacionados

- `docs/specs/agent-l3-tool-use/spdd.md` — tool use loop que llama este catálogo
- `docs/adr/` — decisión de SQLite local-first (single-tenant by design)
- `api/src/Neuracode.Crm.Api/Domain/ProductCatalog.cs` — fuente actual (hardcoded → migrar a DB)
