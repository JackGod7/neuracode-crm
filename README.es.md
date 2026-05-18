<div align="center">

# Neuracode CRM

### CRM WhatsApp-nativo para PYMEs Hispanic US + LATAM

**Producto comercial de [Neuracode](https://neuracode.pe) — uso interno y clientes.**

</div>

---

## Qué es

CRM local-first con agente IA de WhatsApp. Los mensajes inbound crean contactos automáticamente, el agente responde en tiempo real (FAQ, catálogo, política), y escala al asesor humano con un solo endpoint.

**En producción**: `crm-api-production-dd1c.up.railway.app` — agente WhatsApp para [Accesorios Para Él](https://accesoriosparael.store), tienda de joyería en Perú.

---

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 10 Minimal API + EF Core 10 |
| Base de datos | SQLite (T1) / SQLCipher (T2 HIPAA) |
| Frontend | Next.js 16 + React 19 + Tailwind v4 |
| Agente IA | Claude Haiku 4.5 (WhatsApp) |
| WhatsApp | Meta Cloud API v25.0 |
| Deploy | Railway (Docker) / Scalahosting VPS (T2) |

---

## Correr local (desarrolladores Neuracode)

```bash
# Backend — :5000
dotnet run --project api/src/Neuracode.Crm.Api

# Frontend — :3000
npm install && npm run dev

# Tests (139)
dotnet test
```

---

## Variables de entorno (producción)

| Variable | Descripción |
|----------|-------------|
| `META_VERIFY_TOKEN` | Token verificación webhook Meta |
| `META_ACCESS_TOKEN` | Token permanente Meta Cloud API |
| `META_PHONE_NUMBER_ID` | ID número de teléfono Meta Business |
| `META_APP_SECRET` | Secreto HMAC-SHA256 |
| `ANTHROPIC_API_KEY` | Claude Haiku 4.5 para agente WA |
| `AGENT_BUSINESS_PROMPT` | Prompt del negocio (override del default) |

---

## Dual-Track

| Track | Mercado | Precio |
|-------|---------|--------|
| T1 | Retail, restaurantes, agencias | $1,200–2,500/mes |
| T2 | Clínicas, dental, pharma (HIPAA) | $2,500–7,500/mes |

Mismo código base. Feature flags activan seams T2 (auth, SQLCipher, audit log). Ver [`docs/CONTEXT.md`](docs/CONTEXT.md).

---

## Tests

```bash
dotnet test  # 139 tests: unit + contract + concurrencia + Gherkin
```

---

## Docs

- Domain + backlog: [`docs/CONTEXT.md`](docs/CONTEXT.md)
- Specs: `docs/specs/<feature>/spdd.md`
- ADRs: `docs/adr/`
- Roadmap agente: [`docs/specs/whatsapp-agent/roadmap.md`](docs/specs/whatsapp-agent/roadmap.md)

---

**Neuracode CRM** — Construido y operado por Neuracode (Jack Aguilar / Atlantic City).  
Propietario. Todos los derechos reservados.
