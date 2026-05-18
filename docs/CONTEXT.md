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

- [ ] WhatsApp AI agent — L2 memoria conversacional (`docs/specs/whatsapp-agent/roadmap.md`)
  - [ ] Tabla `agent_memory` (waId, key, value, updated_at)
  - [ ] Extracción JSON post-turn (segundo LLM call)
  - [ ] Inyectar contexto en prompt
- [ ] Templates WhatsApp en español: crear en Meta Business Manager (aprobación 24-48h)
- [ ] GHL reseller SOPs — `docs/track1-ghl/`

### Pendiente

- [ ] WhatsApp UI: botón "Enviar" + historial en detalle de contacto (frontend, sprint futuro)
- [ ] Track 2 HIPAA stack bootstrap (ADR-0007)
- [ ] Retell AI voice integration (ADR-0006)
- [ ] SQLCipher swap para Track 2
- [ ] BAA chain documentation (Retell → Neuracode → cliente)
- [ ] Lead deduplication en import path
- [ ] Source validation en ImportEndpoints
- [ ] `next update` a 16.2.4 (security patch mayo 2026)

### Deuda técnica conocida

- [ ] Semaphore leak: `ConcurrentDictionary<string, SemaphoreSlim>` crece sin bound — implementar LRU/TTL eviction
- [ ] Split transaction en `WhatsAppEndpoints.cs`: contact creado sin activities si segundo `SaveChanges` falla — wrappear en transacción
- [ ] Magic strings: `AgentSignals` / `WaDirection` constants incompletos — extraer todos los string literals
- [ ] `WhatsAppEndpoints.cs` 310 líneas, God handler (SRP violation) — extraer `InboundMessageHandler` + `ContactResolver`
- [ ] `UnitTest1.cs` — dead code, eliminar

### Completado

- [x] .NET 10 migration (ADR-0001)
- [x] Strangler fig migration (ADR-0003)
- [x] LeadIntake — Contact + Activity atómicos
- [x] LeadSource — catálogo canónico + whatsapp source
- [x] N+1 fix en bulk import
- [x] Dual-track strategy (ADR-0005)
- [x] WhatsApp Business API backend (webhook inbound + outbound + DB schema + 139 tests) (2026-05-18)
- [x] WhatsApp agent jewelry store prompt — "Accesorios Para Él" en producción Railway (2026-05-18)
- [x] BUG-02: prompt clarifica productos fuera de catálogo (anillos, etc.) (2026-05-18)
- [x] FK indexes: activities.contact_id, activities.deal_id, deals.contact_id, deals.stage_id, whatsapp_messages.wa_id (2026-05-18)
- [x] Structured logging + correlation IDs para WhatsApp pipeline (2026-05-18)
- [x] Mutex por waId + UNIQUE index contacts.wa_id (BUG-01 concurrencia) (2026-05-18)
