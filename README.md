<div align="center">

# Neuracode CRM

### WhatsApp-native CRM for Hispanic US + LATAM SMBs

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Next.js](https://img.shields.io/badge/Next.js-16-black?logo=next.js)](https://nextjs.org/)
[![Claude Haiku](https://img.shields.io/badge/Claude-Haiku_4.5-DA7756)](https://www.anthropic.com/)
[![Railway](https://img.shields.io/badge/Deploy-Railway-0B0D0E?logo=railway)](https://railway.app/)
[![SQLite](https://img.shields.io/badge/SQLite-EF_Core-003B57?logo=sqlite)](https://www.sqlite.org/)

**Commercial product by [Neuracode](https://neuracode.pe) — not for general distribution.**  
Single-tenant CRM with an AI WhatsApp agent that handles leads 24/7 and escalates to a human when needed.

[English](#what-it-is) | [Español](README.es.md)

</div>

---

## What it is

- **WhatsApp-first** — inbound messages auto-create contacts, the AI agent replies in real time, and escalates to a human with one endpoint call.
- **Local-first backend** — .NET 10 Minimal API + SQLite. No managed cloud DB. Data stays on your deploy.
- **Dual-track** — Track 1 for unregulated SMBs (reseller model), Track 2 for HIPAA-regulated clinics/pharma (SQLCipher + BAA chain).

**Live today**: `crm-api-production-dd1c.up.railway.app` — WhatsApp agent for [Accesorios Para Él](https://accesoriosparael.store), jewelry store Peru.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10 Minimal API + EF Core 10 |
| Database | SQLite (T1) / SQLCipher (T2) |
| Frontend | Next.js 16 + React 19 + Tailwind v4 + shadcn/ui |
| AI Agent | Claude Haiku 4.5 (WhatsApp) |
| WhatsApp | Meta Cloud API v25.0 |
| Deploy | Railway (Docker) / Scalahosting VPS (T2) |

---

## Run Local (Neuracode developers)

**Requirements**: .NET 10 SDK, Node.js 20+, SQLite.

```bash
# Backend — :5000
dotnet run --project api/src/Neuracode.Crm.Api

# Frontend — :3000
npm install && npm run dev

# Tests (139 xUnit + Gherkin)
dotnet test
```

Backend reads `ConnectionStrings__DefaultConnection` env var. Defaults to `../../data/crm.db` (auto-created on first run via EnsureCreated + startup migrations).

---

## Production Deploy (Railway)

```bash
# Railway CLI
railway up

# Or: push to main → Railway auto-deploys via Dockerfile.api
git push origin main
```

`railway.toml` configures Docker build + healthcheck (`/healthz`). Persistent volume at `/app/data/crm.db`.

### Required environment variables

| Variable | Required | Description |
|----------|----------|-------------|
| `META_VERIFY_TOKEN` | ✅ | Webhook verification token (set in Meta for Developers) |
| `META_ACCESS_TOKEN` | ✅ | Permanent system user token for Meta Cloud API |
| `META_PHONE_NUMBER_ID` | ✅ | Phone number ID from Meta Business Manager |
| `META_APP_SECRET` | ✅ | App secret for HMAC-SHA256 signature validation |
| `ANTHROPIC_API_KEY` | ✅ | Claude Haiku 4.5 for the WhatsApp AI agent |
| `AGENT_BUSINESS_PROMPT` | ⬜ | Custom system prompt (overrides default jewelry store prompt) |

### WhatsApp webhook setup

1. Register `https://<your-railway-url>/api/webhooks/whatsapp` in Meta for Developers.
2. Set `META_VERIFY_TOKEN` to match `hub.verify_token`.
3. Subscribe to `messages` and `messaging_postbacks` events.

---

## Features (current, backend)

- Contact management: CRUD, lead scoring, temperature (cold/warm/hot), sources
- Pipeline: Kanban stages, deals, probabilities
- Activities: full audit trail (calls, emails, meetings, notes, WhatsApp in/out)
- WhatsApp agent: inbound → AI reply → outbound → activity log; handoff/bot-resume endpoints
- Import/export: CSV batch import with dedup; CSV export for contacts and deals
- Webhook: receive leads from Typeform, Tally, Google Forms, Zapier
- Email digest: daily summary via Resend
- OpenAPI: `/openapi.json` in development

---

## Dual-Track Strategy

| Track | Target | Model | Pricing |
|-------|--------|-------|---------|
| T1 | SMB retail, restaurants, agencies | Neuracode-managed + GHL reseller | $1,200–2,500/mo |
| T2 | Clinics, dental, pharma (HIPAA) | Single-tenant VPS (Scalahosting) + SQLCipher | $2,500–7,500/mo |

Same codebase. Feature flags gate T2 seams (auth, SQLCipher, HIPAA audit log). Details: [`docs/CONTEXT.md`](docs/CONTEXT.md).

---

## Tests

```bash
dotnet test  # 139 tests: unit + contract + concurrency + Gherkin
```

- Contract tests use in-memory SQLite via `Microsoft.AspNetCore.Mvc.Testing`.
- Gherkin scenarios in `WhatsAppGherkinTests.cs` (13 test cases).
- Coverage: WhatsApp agent pipeline, handoff/resume, dedup, concurrency.

---

## Docs

| Document | Location |
|----------|---------|
| Domain model + backlog | [`docs/CONTEXT.md`](docs/CONTEXT.md) |
| Specs (SPDD REASONS canvas) | `docs/specs/<feature>/spdd.md` |
| Architecture decisions | `docs/adr/` |
| Agent intelligence roadmap | [`docs/specs/whatsapp-agent/roadmap.md`](docs/specs/whatsapp-agent/roadmap.md) |
| Claude Code config | [`CLAUDE.md`](CLAUDE.md) |

---

**Neuracode CRM** — Built and operated by Neuracode (Jack Aguilar / Atlantic City).  
Proprietary. All rights reserved.

[README en Español](README.es.md)
