# Track 2 — Voice + HIPAA + Multi-Channel API

**Status**: Draft (TDD pending) | **Date**: 2026-05-03 | **Owner**: Jack Aguilar | **Track**: T2

---

## Motivación

Track 2 atiende clínicas, dental, pharma Hispanic US + LATAM. Estos clientes necesitan:
- Agente de voz (Retell) que crea leads desde llamadas automáticamente
- Historial de conversaciones multi-canal (WhatsApp, SMS, voz) por contacto
- Audit log HIPAA-compliant de cada acceso a datos PHI
- Auth por API key (override de ADR-0004 solo para deploys Track 2)

v1 actual: 23 endpoints, sin auth, sin voz, sin audit. v2: +12 endpoints, feature flags activan seams T2 sin romper T1.

---

## Mecánica

### Feature flags (env vars)

```
TRACK2_VOICE_ENABLED=true
TRACK2_HIPAA_AUDIT_ENABLED=true
TRACK2_API_KEY_AUTH_ENABLED=true
TRACK2_RATE_LIMIT_ENABLED=true
```

T2 deploy: todos `true`. Dev local / Track 1: todos `false`. Mismo binario, mismo namespace `/api/*`.

### Auth (Track 2 only)

- Header: `X-API-Key: <key>` — generada en setup, rotable, stored hashed (bcrypt)
- Scopes: `admin` (full + audit) / `service` (CRM + calls + conversations)
- Webhook inbound: `X-Webhook-Signature` HMAC-SHA256 (Retell, Twilio)
- Webhook outbound: `X-Neuracode-Signature` HMAC firmado por CRM
- Rate limit: 100 req/min por API key, burst 200, 429 + `Retry-After`

### Phone — E.164 obligatorio

Toda llamada normaliza `phone` vía `PhoneNormalizer.ToE164()` antes de lookup de contacto.

### Invariants

1. `POST /api/calls` crea Contact si no existe (lookup por E.164), siempre crea Activity tipo `call`.
2. `intent = appointment_booking` → crea Deal automáticamente.
3. `outcome = transferred_to_human` → dispara webhook outbound a cliente.
4. Todo acceso a campo PHI (phone, email, notes, transcript) genera entrada en audit log.
5. Audit log es immutable — no existe `DELETE /api/audit`.
6. PHI nunca en logs de aplicación — `RedactPhi()` antes de cualquier `logger.Log()`.
7. v1 endpoints no cambian — backwards compatible.
8. `POST /api/webhook` (Track 1) no recibe cambios.

**Out of scope (v2):**
- Multi-tenant (múltiples clientes en un stack) — single-tenant por decisión (ADR-0007)
- `/api/v2/` namespace separado — feature flags en mismo namespace
- Retention automatizada del audit log (cron job externo)

---

## Ejemplos

### Llamada entrante Retell → Contact + Activity

```yaml
POST /api/calls
X-Voice-Provider: retell
X-Webhook-Signature: <hmac-sha256>
X-API-Key: <client-key>

{
  "providerCallId": "call_abc123",
  "phone": "+13055551234",
  "direction": "inbound",
  "startedAt": 1714766400,
  "endedAt": 1714766520,
  "durationSec": 120,
  "transcript": "agent: Hola, soy Maria...\ncaller: Quiero agendar cita",
  "summary": "Caller requested appointment. Scheduled for Thursday 10am.",
  "intent": "appointment_booking",
  "outcome": "completed",
  "sentimentScore": 0.8,
  "actionsTaken": ["scheduled_appointment"]
}

→ 201: { id, contactId, dealId }   // dealId creado por intent=appointment_booking
```

### Enviar mensaje en conversación

```yaml
POST /api/conversations/conv_xyz/messages
{ "body": "Cita confirmada jueves 10am", "channel": "whatsapp", "fromAgent": true }
→ 201: { id, sentAt, deliveredAt? }
```

### Query audit log (admin only)

```yaml
GET /api/audit?from=1714766400&to=1714852800
X-API-Key: <admin-key>
→ 200: [{ id, timestamp, actor, action: "READ_PHI", resource: "contact:abc", phiFields: ["phone","email"] }]
```

### Test agente de voz (sin llamada real)

```yaml
POST /api/agents/agent_dental/test
{ "input": "Hola, quiero agendar limpieza dental" }
→ 200: { "response": "...", "intentDetected": "appointment", "tokens": 150 }
```

---

## Consecuencias

- +12 endpoints nuevos (calls×3, conversations×4, webhooks×3, agents×4 menos audit×1 = ver TDD plan)
- +3 tablas nuevas: `calls`, `conversations`, `messages` (+ `audit_log`, `agent_configs`, `webhook_outbound`)
- `PhoneNormalizer.ToE164()` requerido en todos los adapters de canal
- Cada cliente Track 2 = stack físicamente aislado (VPS Scalahosting, SQLite+SQLCipher, API key única)
- Blast radius de breach = 1 cliente

**TDD target: +35 tests** (v1: 67 → total: ~102)

---

## Decisiones pendientes

| Decisión | Recomendación |
|----------|--------------|
| Conversación auto-create al primer mensaje o explícita | Auto-create |
| Audit log retention: 6 años (HIPAA min) o 7 años (HHS rec) | 7 años |
| LLM provider para agente: Claude vs GPT-4o | Claude (mejor español + constitutional AI) |
| Thread bilingüe (un hilo) o split por idioma | Un hilo (cliente cambia idioma mid-conversation) |

---

## Patrones relacionados

- `docs/openapi.yaml` — contrato v1, base a extender
- ADR-0005 — Dual-track strategy
- ADR-0006 — Retell como voice provider
- ADR-0007 — HIPAA stack (SQLCipher, BAA chain, Scalahosting)
- `docs/specs/project-restructure/spec.md` — rename actual del repo
