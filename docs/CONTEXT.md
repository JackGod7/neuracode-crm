# Neuracode CRM — Domain + Backlog

## Identidad

- Producto: CRM local-first para SMB Hispanic US + LATAM
- Empresa: Neuracode (Jack Aguilar / Atlantic City)
- Repo: `neuracode-crm`

---

## Business Model

### Track 1 — GHL Reseller (No regulado)

Reventa GoHighLevel Unlimited + AI Employee. Sin código propio.
- Target: med spas, restaurantes, talleres, inmobiliarias
- Pricing: $1,200–2,500/mes + setup $500–1,500
- Margen: 57–70%
- SOPs: `docs/track1-ghl/`

### Track 2 — HIPAA Custom Stack (Regulado)

Neuracode CRM (.NET 10) + Retell AI + Twilio HIPAA + Scalahosting VPS. Single-tenant Docker por cliente.
- Target: pharma, dental, clínicas Hispanic US + LATAM
- Pricing: $2,500–7,500/mes + setup $3–5k
- Margen: 38–74%
- Compliance: `docs/track2-hipaa/`

Track 1 financia Track 2 cash flow. Revisión trimestral.

---

## Domain Model

### Entities

| Entity | Key fields | Notes |
|--------|-----------|-------|
| `Contact` | name, email, phone, company, source, temperature, score, **wa_id**, **opted_out** | Lead principal. `wa_id` = identificador Meta (más confiable que número). `opted_out` bloquea outbound WA |
| `Deal` | title, value, stageId, contactId, probability | Un Deal por Contact |
| `Activity` | type, description, contactId, dealId, scheduledAt, completedAt, **wamid** | Audit trail obligatorio. `wamid` correlaciona delivery status de WhatsApp |
| `PipelineStage` | name, order, color, isWon, isLost | Configurable por usuario |
| `CrmSettings` | key, value | Config de app (tema, idioma, etc.) |
| `WhatsAppMessage` | wa_id, wamid (UNIQUE), direction, body, status, created_at | Deduplicación de mensajes Meta. `wamid` = `wamid.*` único por mensaje |

### Activity types canónicos

`note` · `call` · `email` · `meeting` · `follow_up` · `whatsapp_inbound` · `whatsapp_outbound`

### Lead Lifecycle

```
Intake (webhook / import / manual)
  → LeadIntake.Build(LeadDraft) → (Contact, Activity)   ← siempre ambos (invariante)
  → Score 0–100 + Temperature (cold/warm/hot)
  → Pipeline stage → Follow-ups → Deal conversion
```

### Lead Temperature (scoring.ts)

| Range | Temperature |
|-------|------------|
| 0–39 | cold |
| 40–69 | warm |
| 70–100 | hot |

### Lead Sources (catálogo canónico — LeadSource.cs)

`webhook` · `import` · `manual` · `whatsapp` · `referral` · `facebook` · `instagram` · `google` · `typeform` · `tally` · `zapier` · `otro`

---

## Architecture Seams by Track

| Seam | Track 1 | Track 2 |
|------|---------|---------|
| Auth | ninguna (ADR-0004) | ninguna, local-only |
| DB encryption | SQLite plain | SQLCipher (ADR-0007) |
| Voice | n/a | Retell AI + Twilio HIPAA (ADR-0006) |
| PHI handling | n/a | `RedactPhi()` en todo log |
| Deploy | local / Docker | Scalahosting VPS single-tenant |

---

## Backlog

### En progreso

- [ ] Templates WhatsApp en español: crear en Meta Business Manager (aprobación 24-48h)
- [ ] GHL reseller SOPs — `docs/track1-ghl/`

### Pendiente — Agent

- [ ] L3 tool use: `get_product_price(sku)`, `check_stock(sku)` via Anthropic tools API — necesario para eval `precio_coleccion_boss` precision≥4
- [ ] L4 state machine: greeting→discovery→recommendation→close — persistir estado en `agent_memory`
- [ ] L5 tono humano: variante de saludos, usar nombre real del cliente, typing delay (Meta API)
- [ ] L6 handoff summary: al ESCALAR, generar resumen 3-bullet → guardar como Activity `handoff_summary`

### Pendiente — Frontend

- [ ] WhatsApp UI: botón "Enviar" + historial en detalle de contacto (wa-window endpoint ✅, UI pendiente)
- [ ] `/wa-test`, `/deploy-railway`, `/spec-new` slash commands en `.claude/commands/`

### Pendiente — Infra

- [ ] GitHub Actions CI: `dotnet test` + `npm run lint` en PR gate (develop→main)
- [ ] License file: cambiar a propietaria (no MIT)
- [ ] Track 2 HIPAA stack bootstrap (ADR-0007)
- [ ] Retell AI voice integration (ADR-0006)
- [ ] SQLCipher swap para Track 2
- [ ] BAA chain documentation (Retell → Neuracode → cliente)
- [ ] Lead deduplication en import path
- [ ] Source validation en ImportEndpoints
- [ ] `next update` Next.js 16.2.4 (security patch)

### Deuda técnica conocida

- [ ] Semaphore leak: `ConcurrentDictionary<string, SemaphoreSlim>` crece sin bound — LRU/TTL eviction
- [ ] Split transaction en `WhatsAppEndpoints.cs`: contact creado sin activities si segundo `SaveChanges` falla
- [ ] `WhatsAppEndpoints.cs` ~450 líneas, God handler (SRP) — extraer `InboundMessageHandler` + `ContactResolver`
- [ ] Magic strings: `AgentSignals` / `WaDirection` constants incompletos

### Completado

- [x] .NET 10 migration (ADR-0001)
- [x] Strangler fig migration (ADR-0003)
- [x] LeadIntake — Contact + Activity atómicos
- [x] LeadSource — catálogo canónico + whatsapp source
- [x] N+1 fix en bulk import
- [x] Dual-track strategy (ADR-0005)
- [x] WhatsApp Business API backend (webhook inbound + outbound + DB schema) (2026-05-18)
- [x] WhatsApp agent jewelry store — "Accesorios Para Él" en producción Railway (2026-05-18)
- [x] BUG-02: prompt clarifica productos fuera de catálogo (2026-05-18)
- [x] FK indexes: activities, deals, whatsapp_messages (2026-05-18)
- [x] Structured logging + correlation IDs (2026-05-18)
- [x] Mutex por waId + UNIQUE index contacts.wa_id (BUG-01) (2026-05-18)
- [x] L2 memoria conversacional: `agent_memory` tabla + extracción JSON post-turn + inyección en prompt (2026-05-18)
- [x] Debounce 4s: agrupa mensajes rápidos en un solo call al agente (2026-05-18)
- [x] GET /api/contacts/{id}/chat — endpoint compacto para leer conversación (2026-05-18)
- [x] LLM-as-Judge eval framework: 8 escenarios JSON + [Theory] xUnit + `LlmJudge` (Haiku) (2026-05-18)
- [x] 24h conversation window: free-text cuando ventana abierta, template si cerrada (2026-05-18)
- [x] Warmth prompt + max_tokens=120 guardrail (reemplaza post-processing frágil) (2026-05-18)
- [x] Process: SPDD gates, TDD pre-commit hook, CLAUDE.md trim, REASONS template (2026-05-18)
