# WhatsApp Agent IA

**Status**: spec — pendiente implementación | **Date**: 2026-05-16 | **Owner**: Jack Aguilar | **Track**: both

---

## Motivación

El webhook inbound ya crea el contacto, pero el asesor responde manualmente. Con 200+ msgs/día eso no escala. El agente automatiza FAQ y captura leads, escalando solo cuando no puede responder. El CRM funciona sin IA (sin `ANTHROPIC_API_KEY`) — el agente es opt-in.

---

## Componentes

### 1 — `IAgentService` + `AgentService`

```
AgentService.HandleAsync(AgentContext ctx) → string? response
```

- Retorna texto → bot responde vía WhatsApp
- Retorna `null` → escalación (humano atiende)
- Sin `ANTHROPIC_API_KEY` → retorna `null` silenciosamente
- Timeout: 4 segundos (Meta requiere 200 en <5s)
- Model: `claude-haiku-4-5-20251001` (~$0.001/msg)
- Never throws — siempre retorna `string?`

**AgentContext:**
```csharp
record AgentContext(
    string ContactId, string WaId, string Name,
    string Message, IReadOnlyList<string> RecentMessages,
    string BusinessPrompt);
```

### 2 — `bot_handling` en contacts

- `true` = bot responde (default para contactos WA)
- `false` = humano tomó control
- Contacto manual (no WA) → default `false`

### 3 — Hook en webhook inbound

Después de guardar actividad inbound, si `contact.BotHandling && agentService.IsConfigured`:

1. Llamar `HandleAsync(ctx)` con últimas 5 actividades como contexto
2. Si respuesta != null → `WhatsAppService.SendTextAsync()` → actividad `whatsapp_outbound`
3. Si null → no enviar (asesor ve mensaje sin respuesta automática)
4. Fallo en send → log, no propagar (webhook debe devolver 200 igual)

### 4 — Nuevos endpoints

```
POST /api/contacts/{id}/handoff     → bot_handling = false → 200
POST /api/contacts/{id}/bot-resume  → bot_handling = true  → 200
```

Ambos devuelven `{ success: true, botHandling: bool }`.

### 5 — `WhatsAppService.SendTextAsync()`

Nuevo método para texto libre (dentro de ventana 24h, gratis):

```
POST https://graph.facebook.com/v25.0/{PHONE_NUMBER_ID}/messages
{ type: "text", text: { body: "..." } }
```

---

## Flujos

```
FAQ automático:
  "¿envían a Arequipa?" → Claude → "Sí, 2-3 días hábiles." → whatsapp_outbound

Lead calificado:
  "¿cuánto cuesta la pulsera BOSS?" → Claude → detecta intención
  → responde precio + "Un asesor te contactará" → notifica asesor

Escalación:
  "quiero hablar con alguien" → Claude → null
  → no respuesta automática → asesor atiende manualmente

Humano en control (bot_handling=false):
  webhook guarda actividad inbound → NO llama AgentService
  → asesor responde desde CRM libremente
```

---

## Invariantes

1. `AgentService` never throws — siempre `string?`
2. Timeout 4s máx hacia Claude API
3. Sin API key → modo silencioso, CRM funciona igual
4. Toda respuesta del agent → actividad `whatsapp_outbound` en DB
5. `bot_handling` default: `true` para WA contacts, `false` para manuales
6. Webhook devuelve 200 aunque agent o send fallen

---

## Implementación — orden

1. ⬜ Migración DB: `contacts.bot_handling INTEGER NOT NULL DEFAULT 0`
2. ⬜ `IAgentService` + `AgentService` (Claude Haiku, 4s timeout)
3. ⬜ `WhatsAppService.SendTextAsync(string waId, string text)`
4. ⬜ Hook en `WhatsAppEndpoints.cs` POST inbound
5. ⬜ `POST /api/contacts/{id}/handoff` + `/bot-resume`
6. ⬜ Tests (rojo → verde → refactor)
7. ⬜ System prompt piloto joyería (ver sección System Prompt)

---

## System prompt piloto — joyería

```
Eres el asistente de ventas de {BUSINESS_NAME}. Respondes en español,
de forma amable y concisa. Puedes responder sobre: horarios, métodos de
pago, envíos a todo Perú (2-3 días hábiles), política de devoluciones
(7 días), y rango de precios general.

Si el cliente pregunta precio específico de un producto o quiere
coordinar una compra → responde que un asesor le contactará pronto
y devuelve null internamente para escalar.

Si el cliente solicita hablar con una persona → devuelve null.
Máximo 2 oraciones por respuesta.
```

Configurable en Settings v2 (no en este sprint).

---

## Deuda introducida

- `contacts.bot_handling` — migración additive (Program.cs patrón existente)
- `WhatsAppService.SendTextAsync()` — método nuevo, misma clase
- System prompt hardcoded — externalizar en Settings v2
