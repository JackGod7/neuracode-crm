# Neuracode CRM — System Architecture

**Owner**: Jack Aguilar | **Version**: 1.0 | **Date**: 2026-05-19

---

## Vista general

```
┌─────────────────────────────────────────────────────────────────────┐
│                        CLIENTE FINAL                                │
│                  (usuario WhatsApp del negocio)                     │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ WhatsApp message
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  META WHATSAPP BUSINESS API                         │
│              (webhook → crm-api-production.railway.app)             │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ POST /api/webhooks/whatsapp
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     APPLICATION LAYER                               │
│                   .NET 10 CRM API (Railway)                         │
│                                                                     │
│  ┌─────────────────┐   ┌──────────────────┐   ┌─────────────────┐  │
│  │ WhatsApp        │   │  Agent Service   │   │  CRM Endpoints  │  │
│  │ Endpoints       │──▶│  (Orchestrator)  │   │  /contacts      │  │
│  │ /webhooks/wa    │   │                  │   │  /deals         │  │
│  │ /wa-send        │   │  - Debounce 4s   │   │  /activities    │  │
│  │ /chat           │   │  - Memory inject │   │  /pipeline      │  │
│  └────────┬────────┘   │  - Catalog inject│   └─────────────────┘  │
│           │            └───────┬──────────┘                         │
│           │                    │ Anthropic API call                  │
│           │                    ▼                                     │
│  ┌────────▼────────────────────────────────┐                        │
│  │              DATA LAYER                 │                        │
│  │           SQLite + EF Core              │                        │
│  │                                         │                        │
│  │  contacts  │  activities  │  deals      │                        │
│  │  wa_msgs   │  agent_memory│  products   │                        │
│  │  crm_settings (catalog JSON override)   │                        │
│  └─────────────────────────────────────────┘                        │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            ▼               ▼               ▼
┌───────────────┐  ┌────────────────┐  ┌──────────────────┐
│  ANTHROPIC    │  │  META API      │  │  CRM FRONTEND    │
│  Claude Haiku │  │  (send reply)  │  │  Next.js 16      │
│  (inference)  │  │                │  │  (operador UI)   │
└───────────────┘  └────────────────┘  └──────────────────┘
```

---

## Capas del sistema

### L1 — Canal: Meta WhatsApp Business API
- Recibe mensajes de clientes finales
- Entrega respuestas del agente
- Gestiona templates (ventana 24h)
- Config: `META_PHONE_NUMBER_ID`, `META_WHATSAPP_TOKEN`, `META_VERIFY_TOKEN`

### L2 — Aplicación: .NET 10 CRM API
- Única fuente de verdad del negocio
- Maneja idempotencia (wamid UNIQUE), debounce, mutex por waId
- Orquesta el ciclo: inbound → agente → outbound → actividad
- Expone REST API para frontend y webhooks para Meta

### L3 — Inteligencia: Agente WhatsApp (Claude Haiku)
- Modelo: `claude-haiku-4-5-20251001`
- Contexto inyectado: memoria L2 + catálogo de productos + historial
- Niveles de inteligencia:
  - **L1** FAQ responder + escalación (live)
  - **L2** Memoria conversacional persistente (live)
  - **L3** Precios exactos por catálogo (live)
  - **L4** Flujo guiado de ventas (pending)
  - **L5** Tono humano + variación (pending)
  - **L6** Handoff con resumen (pending)
- Config: `ANTHROPIC_API_KEY`, `AGENT_MODEL`, `AGENT_MAX_TOKENS`

### L4 — Datos: SQLite + EF Core
- Local-first: DB vive en el container (Railway Volume)
- Entidades: Contact, Deal, Activity, WhatsAppMessage, AgentMemory, Product
- Single-tenant por diseño (T2 HIPAA compliance)

### L5 — Operador: Next.js 16 Dashboard
- UI para Jack / operador del negocio
- Vista de contactos, pipeline, conversaciones WA
- Pendiente: auth, WhatsApp send UI, catálogo UI

---

## Modelo de deployment

### Por cliente (single-tenant)

```
GitHub: JackGod7/neuracode-crm  ← UN repo, código compartido
              │
              ├──▶ Railway Project: "accesorios-para-el"
              │         ├── Service: crm-api
              │         │     Builder: Dockerfile.api
              │         │     Vars: META_*, ANTHROPIC_*, AGENT_BUSINESS_PROMPT
              │         └── Service: crm-frontend (futuro)
              │               Builder: Dockerfile.frontend
              │
              └──▶ Railway Project: "cliente-b"  (futuro)
                        ├── Service: crm-api
                        │     Builder: Dockerfile.api
                        │     Vars: META_* propias del cliente B
                        └── Service: crm-frontend (futuro)
```

**Personalización por cliente = variables de entorno, no código:**

| Variable | Qué configura |
|----------|--------------|
| `AGENT_BUSINESS_PROMPT` | Identidad del negocio, catálogo, política |
| `META_PHONE_NUMBER_ID` | Número WA del cliente |
| `META_WHATSAPP_TOKEN` | Token Meta del cliente |
| `ANTHROPIC_API_KEY` | Key Anthropic (puede ser la misma Neuracode) |

---

## CI/CD Pipeline (target)

```
git push origin main
        │
        ▼
GitHub Actions
        ├── dotnet test (190 tests)
        ├── npm run lint
        └── ✅ pass → trigger Railway deploy
                │
                ▼
        Railway build (Dockerfile.api)
                │
                ▼
        crm-api live (~4 min)
```

**Estado actual**: Railway deploy manual (webhook roto). GitHub Actions pendiente.

---

## Decisiones arquitectónicas clave

| Decisión | Elección | Razón |
|----------|---------|-------|
| Runtime | .NET 10 | Performance, typing, HIPAA tooling |
| DB | SQLite | Local-first, zero-ops, single-tenant |
| AI | Claude Haiku | Costo bajo, latencia aceptable, español nativo |
| Deploy | Docker + Railway | Simplicidad, single-tenant por container |
| Auth | Ninguna (hoy) | Producto interno; auth = sprint dedicado |
| WA channel | Meta Business API directa | Sin intermediarios, control total |

Ver ADRs en `docs/adr/` para cada decisión detallada.

---

## Pointers

- Domain model: `docs/CONTEXT.md`
- Governance docs: `docs/DOCS.md`
- Agent specs: `docs/specs/agent-l*/spdd.md`
- Deploy guide: `docs/guides/deploy-vps.md`
