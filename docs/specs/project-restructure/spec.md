# Project Restructure — Neuracode CRM

**Status**: Approved | **Date**: 2026-05-09 | **Owner**: Jack Aguilar | **Track**: both

---

## Motivación

El repo se llama `auto-crm`. Es un producto de Neuracode vendido a clientes reales con 3 perfiles:
- **Perfil A** — sin sistema, 100 leads/día vía WhatsApp + Instagram
- **Perfil B** — CRM pagado (HubSpot/Zoho/Pipedrive, exact TBD)
- **Perfil C** — sistema propio custom

La estructura actual (`src/`, `auto-crm-api/`) no comunica la arquitectura dual frontend/backend, ni la marca del producto. Un intern que abre el repo no sabe dónde está nada. Antes de construir features, el orden es prerequisito.

---

## Mecánica

Un solo commit de rename puro. Cero cambios funcionales.

### Naming map

| Actual | Nuevo |
|--------|-------|
| Root dir (OS, manual) | `auto-crm/` → `neuracode-crm/` |
| `src/` | `web/` |
| `auto-crm-api/` | `api/` |
| `api/src/AutoCrm.Api/` | `api/src/Neuracode.Crm.Api/` |
| `api/tests/AutoCrm.Tests/` | `api/tests/Neuracode.Crm.Tests/` |
| Namespace `AutoCrm.Api` | `Neuracode.Crm.Api` (global replace) |
| `package.json` name | `neuracode-crm-web` |
| Docker service `crm` | `crm-web` |
| Docker service `api-net` | `crm-api` |

### Pasos (orden estricto)

1. `git mv src web`
2. `git mv auto-crm-api api`
3. Rename dirs internos .NET: `AutoCrm.Api` → `Neuracode.Crm.Api`, `AutoCrm.Tests` → `Neuracode.Crm.Tests`
4. Rename `.csproj` + solution file
5. Replace global `AutoCrm` → `Neuracode.Crm` en todos los `.cs`
6. `docker-compose.yml` — service names + build contexts
7. Dockerfiles — build paths
8. `package.json` — name field
9. `drizzle.config.ts`, `next.config.ts`, `tsconfig.json` — paths
10. `.env.example` — agregar vars de todas las integraciones
11. `CLAUDE.md`, `CONTEXT.md` — paths y branding

### `.env.example` final

```bash
# Meta (Instagram + Facebook)
META_APP_ID=
META_APP_SECRET=
META_VERIFY_TOKEN=

# Retell Voice
RETELL_API_KEY=
RETELL_WEBHOOK_SECRET=

# Twilio (Track 2 HIPAA)
TWILIO_ACCOUNT_SID=
TWILIO_AUTH_TOKEN=

# AI
ANTHROPIC_API_KEY=

# Email
RESEND_API_KEY=
DIGEST_EMAIL=
DIGEST_FROM=
```

---

## Ejemplos

**Antes:**
```
auto-crm/
├── src/app/page.tsx
├── auto-crm-api/src/AutoCrm.Api/Program.cs
└── docker-compose.yml  →  service: crm / api-net
```

**Después:**
```
neuracode-crm/
├── web/app/page.tsx
├── api/src/Neuracode.Crm.Api/Program.cs
└── docker-compose.yml  →  service: crm-web / crm-api
```

**Intern llega al repo nuevo:**
- `web/` → "aquí está el frontend"
- `api/` → "aquí está el backend"
- `docs/` → "aquí está todo lo que necesito leer"

---

## Consecuencias

**Positivo:**
- Naming refleja marca real del producto
- Estructura comunica arquitectura sin explicación
- Base limpia para agregar `agents/` (WhatsApp, Retell, Instagram) como carpeta hermana futura

**Negativo / Trade-offs:**
- Cualquier script externo o CI que referencie `auto-crm-api/` o `src/` se rompe — revisar antes del commit
- Root dir rename es manual (OS, fuera del repo)

**No cambia:**
- Funcionalidad, tests, datos, docker volumes
- `.framework-stack.json` y `.mac-baseline.json` — los consumen comandos activos, se quedan

---

## Patrones relacionados

- `docs/adr/0001-migrate-backend-to-dotnet-10.md` — contexto de la decisión .NET
- `docs/adr/0005-dual-track-product-strategy.md` — por qué Neuracode CRM existe
- `CONTEXT.md` → Domain model, perfiles A/B/C, backlog
