# WhatsApp AI Agent — SPDD

**Status**: spec | **Date**: 2026-05-17 | **Owner**: Jack Aguilar | **Track**: both
**Format**: Fowler SPDD REASONS canvas (martinfowler.com/articles/structured-prompt-driven/)

---

## Requirements — qué debe hacer el sistema

1. **Respuesta automática FAQ**: ante un mensaje inbound WA de un contacto con `bot_handling = true`, el agente responde de forma autónoma preguntas frecuentes sobre productos, precios, envíos, pagos, separados y política de cambios.
2. **Escalación limpia**: si el agente retorna `ESCALAR` (string literal) o `null`, el webhook no envía ningún mensaje al cliente; el asesor humano interviene manualmente.
3. **Handoff / bot-resume**: el asesor puede desactivar el bot por contacto (`POST /handoff`) y reactivarlo (`POST /bot-resume`) en cualquier momento.
4. **Idempotencia de webhook**: el sistema ignora mensajes con `wamid` ya procesado; retorna `200 OK` sin efectos secundarios.
5. **Opt-out honrado**: si `contact.opted_out = true`, el sistema no envía ningún mensaje outbound, independientemente de `bot_handling`.
6. **Degradación silenciosa**: sin `ANTHROPIC_API_KEY` configurada, el agente retorna `null` y el CRM funciona igual que antes (sin IA).
7. **Sin emojis, máximo 2 oraciones**: toda respuesta del agente cumple estas restricciones de estilo para Accesorios Para Él.
8. **Timeout de 3 s**: si Claude API no responde en 3 segundos, el agente retorna `null`; el webhook igual retorna `200`.
9. **Actividad outbound registrada**: cada respuesta exitosa del agente genera una actividad `whatsapp_outbound` en DB con el texto enviado.
10. **Contexto conversacional**: el agente recibe las últimas 5 actividades del contacto como historial para mantener coherencia en la conversación.

---

## Entities — objetos de dominio

| Entidad | Campos clave | Notas |
|---------|-------------|-------|
| `Contact` | `id`, `name`, `waId`, `botHandling`, `optedOut` | `waId` = identificador único Meta (ej. `51987654321`). `botHandling` controla si el bot responde. |
| `Activity` | `id`, `contactId`, `type`, `description`, `wamid`, `createdAt` | Tipos relevantes: `whatsapp_inbound`, `whatsapp_outbound`. `wamid` = ID único de mensaje Meta para deduplicación. |
| `AgentContext` | `contactId`, `waId`, `name`, `message`, `recentMessages`, `businessPrompt` | Payload inmutable que se pasa al agente. Nunca muta tras construcción. |
| `WaId` | string | Número de teléfono en formato E.164 sin `+`, asignado por Meta. Identifica al remitente inbound. |
| `BotHandling` | bool (`true`/`false`) | `true` = bot activo (default para contactos WA). `false` = humano en control. |
| `Wamid` | string | ID único de mensaje WhatsApp (`wamid.*`). Columna UNIQUE en `WhatsAppMessage`. Garantiza idempotencia. |
| `WhatsAppMessage` | `waId`, `wamid`, `direction`, `body`, `status`, `createdAt` | Tabla de deduplicación. `direction`: `inbound`/`outbound`. |
| `CrmSettings` | `key`, `value` | Almacena `BUSINESS_PROMPT` configurable (v2). Por ahora hardcoded en `AgentService`. |

---

## Approach — cómo funciona

```
Cliente WA
    │
    ▼
POST /api/webhooks/whatsapp          (Meta → CRM)
    │
    ├─ 1. Verificar firma HMAC-SHA256
    ├─ 2. Extraer waId, wamid, body del payload Meta
    ├─ 3. Idempotencia: ¿wamid ya existe en WhatsAppMessage? → 200 OK, fin
    ├─ 4. Upsert Contact (waId) → si nuevo: name=waId, botHandling=true
    ├─ 5. Insertar Activity whatsapp_inbound + WhatsAppMessage
    │
    ├─ 6. Guard: contact.BotHandling && agentService.IsConfigured && !contact.OptedOut
    │       └─ false → fin (asesor atiende)
    │
    ├─ 7. Construir AgentContext (últimas 5 actividades como recentMessages)
    ├─ 8. AgentService.HandleAsync(ctx) con CancellationToken 3 s
    │       ├─ response = "ESCALAR" → retornar null
    │       ├─ response = null/timeout → retornar null
    │       └─ response = texto → continuar
    │
    ├─ 9. WhatsAppService.SendTextAsync(waId, responseText)
    │       └─ falla → log Warning, NO propagar
    │
    └─ 10. Insertar Activity whatsapp_outbound + WhatsAppMessage
           └─ 200 OK siempre
```

**Model**: `claude-haiku-4-5-20251001` (~$0.001/msg). Elegido por latencia baja y costo mínimo para FAQ.

**Prompt del sistema (Accesorios Para Él)**:
```
Eres el asistente de ventas de Accesorios Para Él (accesoriosparael.store),
tienda de joyería y accesorios en Perú. Respondes SOLO en español, sin emojis,
con máximo 2 oraciones por respuesta.

Productos disponibles:
- Brazaletes/pulseras para caballero: METROPOLE, BOSS, E.ARMANI, CROCODILE
- Brazaletes/pulseras para dama: ANGEL EYES
- Combos para parejas: Combos Love (brazalete caballero + pulsera dama)
- Precios: S/. 149–607, hasta 63% de descuento

Política de la tienda:
- Envíos: todo Perú + internacional; 2-3 días hábiles a provincias
- Pagos: Yape, Plin, transferencia bancaria, tarjeta de crédito/débito, contra entrega
- Separados: depósito mínimo S/. 40 confirma la compra
- Cambios: hasta 15 días calendario desde la compra
- Atención: todos los días de 9am a 7pm

Si el cliente dice "quiero hablar con alguien", "quiero un asesor", o similar →
responde ESCALAR (solo esa palabra, nada más).

Si la pregunta está fuera de tu alcance (productos no disponibles, temas técnicos,
preguntas no relacionadas) → redirecciona amablemente a los productos disponibles.
```

---

## Structure — estructura de código

```
api/src/Neuracode.Crm.Api/
├── Agents/
│   ├── IAgentService.cs          # interface: HandleAsync + IsConfigured
│   └── AgentService.cs           # implementación Claude Haiku, timeout 3s
├── Endpoints/
│   └── WhatsAppEndpoints.cs      # POST /webhooks/whatsapp (hook agente aquí)
│                                 # POST /contacts/{id}/handoff
│                                 # POST /contacts/{id}/bot-resume
├── Services/
│   └── WhatsAppService.cs        # SendTextAsync() método nuevo
└── Data/
    └── Migrations/               # bot_handling column + WhatsAppMessage table

api/tests/Neuracode.Crm.Api.Tests/
├── Agents/
│   └── AgentServiceTests.cs      # unit: timeout, null, ESCALAR, respuesta válida
├── Endpoints/
│   ├── WhatsAppWebhookTests.cs   # integration: wamid dup, handoff guard, opt-out
│   ├── HandoffEndpointTests.cs   # integration: handoff/resume, 404
│   └── BotResumeEndpointTests.cs
└── Fakes/
    └── FakeAgentService.cs       # stub configurable para integration tests
```

---

## Operations — operaciones clave

### O1 — Procesar mensaje inbound (happy path)
```
PRECONDITION : wamid no existe, contact.BotHandling=true, API key configurada
INPUT        : payload Meta con entry[].changes[].value.messages[]
OUTPUT       : Activity inbound + Activity outbound + WhatsApp reply enviado
POSTCONDITION: WhatsAppMessage con wamid=X existe (deduplicación futura garantizada)
```

### O2 — Escalación por solicitud de humano
```
PRECONDITION : contact.BotHandling=true, cliente dice "quiero hablar con alguien"
INPUT        : mensaje inbound
OUTPUT       : Activity inbound guardada; AgentService retorna ESCALAR → null
POSTCONDITION: NO Activity outbound. Asesor ve el mensaje sin respuesta automática.
```

### O3 — Handoff manual
```
PRECONDITION : contact existe, contact.BotHandling=true
INPUT        : POST /api/contacts/{id}/handoff
OUTPUT       : { success: true, botHandling: false }
POSTCONDITION: contact.BotHandling=false; mensajes siguientes no activan agente
```

### O4 — Bot-resume
```
PRECONDITION : contact existe, contact.BotHandling=false
INPUT        : POST /api/contacts/{id}/bot-resume
OUTPUT       : { success: true, botHandling: true }
POSTCONDITION: contact.BotHandling=true; mensajes siguientes activan agente
```

### O5 — Deduplicación wamid
```
PRECONDITION : WhatsAppMessage con wamid=X ya existe en DB
INPUT        : POST /api/webhooks/whatsapp con mismo wamid
OUTPUT       : HTTP 200 OK, sin efectos secundarios
POSTCONDITION: ninguna Activity duplicada, ningún mensaje enviado al cliente
```

### O6 — Timeout de agente
```
PRECONDITION : Claude API tarda >3 s
INPUT        : mensaje inbound
OUTPUT       : CancellationTokenSource cancela la tarea; agente retorna null
POSTCONDITION: Activity inbound guardada; NO outbound; webhook retorna 200
```

---

## Norms — restricciones

| # | Norma | Justificación |
|---|-------|--------------|
| N1 | Timeout 3 s hacia Claude API | Meta requiere HTTP 200 en <5 s; dejamos 2 s de margen para DB + WA send |
| N2 | Máximo 2 oraciones por respuesta | Legibilidad en WhatsApp; acordado con cliente Accesorios Para Él |
| N3 | Sin emojis en respuestas del agente | Tono profesional del negocio |
| N4 | Solo español en respuestas | Mercado peruano; aunque el cliente escriba en otro idioma |
| N5 | `AgentService.HandleAsync` nunca lanza excepción | Contrato de interface; fallas se loguean y retornan null |
| N6 | `ESCALAR` como string literal de retorno → equivale a null para el caller | Permite al agente señalizar escalación explícita sin romper el contrato |
| N7 | `bot_handling = true` solo para contactos creados por webhook WA | Contactos manuales no reciben bot por defecto |
| N8 | PHI nunca en logs (Track 2) | `RedactPhi()` antes de cualquier log que incluya `message` o `body` |
| N9 | System prompt no contiene precios unitarios por SKU | Evitar conflictos con catálogo; asesor cierra la venta |
| N10 | Webhook retorna 200 siempre, incluso ante fallo de agente o fallo de send | Requisito Meta: errores 5xx causan reintentos infinitos |

---

## Safeguards — qué nunca debe ocurrir

| # | Safeguard | Mecanismo |
|---|-----------|-----------|
| S1 | Nunca enviar respuesta después de handoff | Guard `contact.BotHandling` evaluado DESPUÉS de cargar contacto desde DB, no desde caché |
| S2 | Nunca procesar wamid duplicado | `WhatsAppMessage.wamid` columna UNIQUE + check antes de toda lógica de negocio |
| S3 | Nunca propagar excepción desde `AgentService` | try/catch total en `HandleAsync`; log + return null |
| S4 | Nunca enviar a contacto con `opted_out = true` | Guard explícito antes de llamar agente Y antes de `SendTextAsync` |
| S5 | Nunca enviar mensaje fuera de ventana 24 h | `WhatsAppService.SendTextAsync` solo disponible en ventana de sesión; templates para fuera de ventana (fuera de scope de este sprint) |
| S6 | Nunca hardcodear API key | Leer de `IConfiguration["ANTHROPIC_API_KEY"]`; si null → `IsConfigured = false` |
| S7 | Nunca loguear el body completo del mensaje en Track 2 | `RedactPhi(message)` antes del log |
| S8 | Nunca crear Activity outbound sin un send exitoso | El orden es: send → si ok → insertar Activity. Si send falla, no registrar outbound falsa. |
