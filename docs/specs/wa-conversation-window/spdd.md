# WhatsApp 24h Conversation Window

**Status**: Draft  
**Date**: 2026-05-18 | **Owner**: Jack Aguilar | **Track**: both

---

## Requirements — qué debe hacer el sistema

1. El sistema sabe si la ventana de 24h está abierta para un `waId` dado, consultando el último mensaje **inbound** en `whatsapp_messages`.
2. Cuando el CRM envía un mensaje proactivo (manual desde UI o follow-up automatizado) y la ventana está abierta → usar `SendTextAsync` (texto libre, costo ~60% menor).
3. Cuando la ventana está cerrada → usar `SendTemplateAsync` (comportamiento actual).
4. El agente conversacional (`AgentService`) no necesita cambio — siempre responde a un inbound, ventana siempre abierta por definición.
5. La UI (`WhatsAppSendDialog`) muestra: si ventana abierta → campo texto libre + templates; si cerrada → solo templates.
6. El endpoint `GET /api/contacts/{id}/wa-window` expone estado de ventana (abierta/cerrada + tiempo restante) para que la UI decida.

**Out of scope:**
- Cambio en el flujo del agente conversacional (ya dentro de ventana).
- Envío de media (images, docs) — futuro L5.
- Apertura proactiva de ventana via template + follow-up automático (L4 state machine).

---

## Entities — objetos de dominio

| Entidad | Campos clave | Notas |
|---------|-------------|-------|
| `ConversationWindow` | `WaId`, `IsOpen: bool`, `ExpiresAt: DateTime?`, `SecondsRemaining: int?` | DTO — no persiste, derivado de `WhatsappMessages` |
| `WhatsappMessage` (existente) | `Direction`, `Timestamp`, `WaId` | Fuente de verdad: último inbound |

No se agrega tabla nueva. `last_customer_message_at` se deriva con query sobre `whatsapp_messages`.

---

## Approach — cómo funciona

```
[CRM UI — botón Enviar]
  │
  ▼
GET /api/contacts/{id}/wa-window
  │
  ├─ IsOpen = true  → muestra textarea libre + templates
  └─ IsOpen = false → muestra solo templates
  │
  ▼
POST /api/contacts/{id}/wa-send
  │  body: { type: "text"|"template", content, templateName? }
  │
  ├─ type="text" + ventana abierta  → SendTextAsync()
  ├─ type="text" + ventana cerrada  → 400 "window closed, use template"
  └─ type="template"               → SendTemplateAsync() (sin restricción)
```

**Derivar ventana:**
```sql
SELECT MAX(created_at) FROM whatsapp_messages
WHERE wa_id = @waId AND direction = 'Inbound'
-- ventana abierta si MAX(created_at) > UtcNow - 24h
```

---

## Structure — árbol de archivos

```
api/src/Neuracode.Crm.Api/
├── Services/
│   ├── IConversationWindowService.cs   ← nueva interfaz
│   └── ConversationWindowService.cs    ← implementación
├── Endpoints/
│   └── WhatsAppEndpoints.cs            ← 2 endpoints nuevos: GET window, POST send
└── Domain/
    └── ConversationWindow.cs           ← DTO record

api/tests/Neuracode.Crm.Tests/
└── UnitTests/
    └── ConversationWindowServiceTests.cs

src/components/contacts/
└── WhatsAppSendDialog.tsx              ← lógica condicional texto libre / templates
```

---

## Operations — operaciones clave

### O1 — ConversationWindowService.GetWindowAsync

```
PRECONDITION : waId no nulo, AppDbContext disponible
INPUT        : waId: string
OUTPUT       : ConversationWindow { IsOpen, ExpiresAt, SecondsRemaining }
POSTCONDITION: sin side-effects; solo lectura DB
```

### O2 — GET /api/contacts/{id}/wa-window

```
PRECONDITION : contactId válido, contact.WaId presente
INPUT        : route param id (int)
OUTPUT       : 200 { isOpen: bool, expiresAt: string?, secondsRemaining: int? }
              | 404 si contacto no existe
POSTCONDITION: nunca lanza excepción al caller
```

### O3 — POST /api/contacts/{id}/wa-send

```
PRECONDITION : contactId válido, contact.WaId presente
INPUT        : { type: "text"|"template", content?: string, templateName?: string, params?: string[] }
OUTPUT       : 200 { messageId }
             | 400 "window closed" si type="text" + ventana cerrada
             | 400 "opt-out" si contact.OptedOut
             | 422 si templateName requerido pero ausente
POSTCONDITION: mensaje registrado en whatsapp_messages con dirección Outbound
```

---

## Norms — restricciones

| # | Norma | Justificación |
|---|-------|--------------|
| N1 | `GetWindowAsync` hace 1 sola query SQL — no carga historial completo | Performance — contactos con miles de mensajes |
| N2 | `SecondsRemaining` calculado con `DateTime.UtcNow` server-side | Evitar clock skew entre cliente y servidor |
| N3 | `type="text"` con ventana cerrada → 400, no 500, no silencioso | Meta rechazaría el send; mejor fallar explícito antes |
| N4 | Opt-out verificado antes de window check — misma prioridad que hoy | N/A — ya existente, mantener orden |
| N5 | `WhatsAppSendDialog` no llama `GET /wa-window` en cada keystroke — solo al abrir el modal | Evitar spam de requests |

---

## Safeguards — qué nunca debe ocurrir

| # | Safeguard | Mecanismo |
|---|-----------|-----------|
| S1 | Nunca enviar texto libre fuera de ventana | O3 valida `IsOpen` antes de llamar `SendTextAsync` |
| S2 | Nunca enviar a contacto con opt-out | Check `contact.OptedOut` en O3, mismo que hoy |
| S3 | Agente conversacional no afectado | Agente llama `SendTextAsync` directamente, no pasa por O3 |
| S4 | Sin breaking change en `SendTemplateAsync` / `SendTextAsync` | Nuevos endpoints encapsulan la lógica — servicios existentes no cambian |

---

## Tests mínimos (TDD red primero)

```
✓ ventana abierta si último inbound hace 23h59m
✓ ventana cerrada si último inbound hace 24h01m
✓ ventana cerrada si no hay mensajes inbound (contacto nuevo)
✓ O3 retorna 400 si type=text + ventana cerrada
✓ O3 retorna 400 si contact.OptedOut (independiente de ventana)
✓ O3 enruta a SendTextAsync si ventana abierta + type=text
✓ O3 enruta a SendTemplateAsync si type=template (sin importar ventana)
✓ GET /wa-window retorna 404 si contacto no existe
```

---

## Patrones relacionados

- `docs/specs/whatsapp-agent/spdd.md` — agente conversacional (no afectado)
- `docs/specs/whatsapp-agent/roadmap.md` — posición: entre L2 y L5
- Relacionado con L5 (tono humano) — texto libre habilita respuestas más naturales en follow-ups
