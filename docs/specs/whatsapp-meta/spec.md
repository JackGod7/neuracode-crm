# WhatsApp Business API — Integración CRM

**Status**: Backend completo — pendiente UI (step 8) y aprobación templates | **Date**: 2026-05-09 | **Updated**: 2026-05-10 | **Owner**: Jack Aguilar | **Track**: both

---

## Motivación

Los leads de Neuracode llegan principalmente por WhatsApp. Hoy el proceso es manual: alguien escribe, alguien responde, el contacto se pierde o se agrega tarde al CRM. Esta integración cierra ese ciclo: WhatsApp → CRM automático (inbound), y CRM → WhatsApp para follow-ups y notificaciones (outbound).

Cuenta activa y verificada: **Neuracode** / +51 997 055 975 / calidad GREEN / estado CONNECTED.

---

## Cuenta Meta — Referencia

| Campo | Valor |
|-------|-------|
| Nombre verificado | `Neuracode` |
| Número | +51 997 055 975 |
| `META_PHONE_NUMBER_ID` | `1003010076238371` |
| `META_WABA_ID` | `1612237300105652` |
| Calidad actual | GREEN |
| Estado | CONNECTED |
| API version activa | v25.0 |
| Template activo | `hello_world` (solo prueba, en inglés — inutilizable en prod) |

---

## Políticas y Restricciones (fuente: Meta Developer Docs — 2026-05-09)

### Regla 1 — Opt-in obligatorio ANTES de cualquier contacto

> "Businesses are required to obtain opt-in permission before messaging people on WhatsApp."

- El opt-in NO tiene que mencionar explícitamente WhatsApp — basta "acepta recibir mensajes de nosotros".
- Debe cumplir la ley local del país del contacto.
- El usuario puede revocar el consentimiento en cualquier momento — la empresa debe honrarlo.
- **Llamadas requieren permiso separado** — el opt-in de mensajes NO cubre llamadas. Se necesita enviar un mensaje interactivo `call_permission_request` y esperar aprobación explícita.

**Implementación en CRM:** Al crear un contacto vía webhook de WhatsApp, el acto de escribir al negocio es el opt-in implícito (el usuario inició el contacto). Para outbound frío: formulario de opt-in previo obligatorio.

### Regla 2 — Templates obligatorios para mensajes iniciados por el negocio

Fuera de la ventana de 24 horas (desde el último mensaje del usuario), SOLO se pueden enviar templates aprobados por Meta. Dentro de la ventana de 24h, se puede enviar cualquier tipo de mensaje.

**Tipos de conversación:**

| Tipo | Cuándo | Requiere template | Costo |
|------|--------|------------------|-------|
| `SERVICE` | Usuario inicia, dentro de 24h | No | Gratis dentro de ventana |
| `UTILITY` | Notificaciones transaccionales | Sí | Bajo |
| `MARKETING` | Promociones, ofertas | Sí | Más alto |
| `AUTHENTICATION` | OTP, verificación | Sí | Separado |

### Regla 3 — Precios por mensaje (desde julio 1, 2025)

El modelo anterior era por conversación (24h). **Desde julio 1, 2025: precio por mensaje entregado.**

- Marketing: tarifa más alta (varía por país del destinatario)
- Utility: tarifas reducidas desde julio 2025
- Service (dentro de 24h ventana): GRATIS
- Free tier: ~1,000 conversaciones/mes gratis (herencia del modelo anterior, aún vigente)

**Para el mercado Hispanic US + LATAM:** Los precios varían por país. Perú, México, Colombia son mercados con tarifas LATAM. US tiene tarifa diferente.

### Regla 4 — Límites de calidad y rating

El número tiene un Quality Rating que Meta monitorea:
- `GREEN`: Buena calidad — sin restricciones
- `YELLOW`: Advertencia — reducir volumen de marketing
- `RED`: Suspensión de envío de templates de marketing

Causas de degradación: spam reports, mensajes no solicitados, opt-outs masivos.

**El número actual está en GREEN — mantener así.**

### Regla 5 — Templates requieren aprobación Meta (24–48h)

Antes de poder usar un template en producción:
1. Crear via API o Meta Business Manager
2. Meta revisa: `PENDING` → `APPROVED` o `REJECTED`
3. Templates rechazados no pueden enviarse — son bloqueados a nivel de API

Causas de rechazo: contenido engañoso, falta de ejemplos claros, variables sin sentido.

---

## Mecánica

### Flujo inbound (WhatsApp → CRM)

```
Usuario escribe a +51 997 055 975
        ↓
Meta envía webhook POST a /api/webhooks/whatsapp
        ↓
CRM verifica firma X-Hub-Signature-256 (HMAC-SHA256)
        ↓
CRM extrae: wa_id, display_name, phone, message body
        ↓
¿Existe contacto con ese número?
  NO → crear contacto (source: "whatsapp", temperature: "hot")
       crear actividad (type: "whatsapp_inbound", description: mensaje)
  SÍ → agregar actividad al contacto existente
        ↓
Respuesta: HTTP 200 en < 5 segundos (obligatorio — Meta reintenta si no)
```

**Invariantes:**
1. Webhook debe responder 200 en < 5 segundos — si tarda más, Meta reintenta y duplica
2. Firma `X-Hub-Signature-256` debe validarse en CADA request — rechazar si inválida
3. `META_VERIFY_TOKEN` se usa en verificación inicial GET — debe coincidir exactamente
4. Deduplicar por `message.id` (wamid.*) — Meta puede reenviar el mismo mensaje
5. El `wa_id` es el identificador único del usuario — más confiable que el número formateado

### Flujo outbound (CRM → WhatsApp)

```
Evento CRM (nuevo lead / follow-up vencido / acción manual)
        ↓
CRM selecciona template según contexto
        ↓
POST https://graph.facebook.com/v25.0/{PHONE_NUMBER_ID}/messages
        ↓
Respuesta: { message_status: "accepted" | "held_for_quality_assessment" | "paused" }
        ↓
Webhook de estado: delivered → read (actualiza actividad en CRM)
```

**Invariantes:**
1. Solo enviar templates aprobados fuera de la ventana de 24h
2. Dentro de ventana de 24h: texto libre permitido, pero registrar como actividad
3. Guardar `wamid` de cada mensaje enviado para tracking de estado
4. Respetar opt-out: si usuario responde "STOP" o bloquea → marcar contacto `opted_out: true`
5. `held_for_quality_assessment`: esperar, no reintentar inmediatamente

**Out of scope:** grupos de WhatsApp, llamadas, catálogos de productos, pagos WhatsApp Pay.

---

## Templates a crear (mercado Hispanic US + LATAM)

Idioma: `es_MX`. Variables numeradas `{{1}}`, `{{2}}` (requerido por Meta). Categoría `UTILITY` donde sea posible — menor costo que `MARKETING`. Aprobación Meta: 24–48h tras envío.

### 1. `crm_bienvenida` — UTILITY

**Cuerpo:**
```
Hola {{1}}, gracias por contactar a {{2}}.

Hemos registrado tu consulta y uno de nuestros asesores te responderá pronto.

Tu número de seguimiento es: *{{3}}*.
```

**Variables de ejemplo (obligatorias al crear en Meta):**
- `{{1}}` → `Juan`
- `{{2}}` → `Neuracode`
- `{{3}}` → `1042`

---

### 2. `crm_seguimiento` — UTILITY

**Cuerpo:**
```
Hola {{1}}, tu caso {{2}} en {{3}} está activo desde el {{4}}.

Un asesor revisará tu solicitud y te contactará pronto. Si necesitas asistencia inmediata, responde este mensaje.
```

**Variables de ejemplo:**
- `{{1}}` → `Juan`
- `{{2}}` → `#1042`
- `{{3}}` → `Neuracode`
- `{{4}}` → `10/05/2026`

> Reescrito 2026-05-11: versión anterior recategorizada MARKETING por Meta ("¿Tienes disponibilidad?" = engagement). Nueva versión es transaccional pura (referencia caso + número).

---

### 3. `crm_propuesta_lista` — MARKETING

**Encabezado (texto):** `Tu propuesta está lista ✓`

**Cuerpo:**
```
Hola {{1}}, tu propuesta de {{2}} está lista para revisión.

📋 Descripción: {{3}}
💰 Inversión: {{4}}
📅 Válida hasta: {{5}}

Responde este mensaje para coordinar los detalles o solicitar ajustes.
```

**Variables de ejemplo:**
- `{{1}}` → `Juan`
- `{{2}}` → `Neuracode`
- `{{3}}` → `Implementación CRM`
- `{{4}}` → `S/ 2,500`
- `{{5}}` → `20/05/2026`

---

### 4. `crm_recordatorio_cita` — UTILITY

**Cuerpo:**
```
Recordatorio de cita — {{1}}

📅 Fecha: {{2}}
🕐 Hora: {{3}}
📍 Modalidad: {{4}}

Responde *CONFIRMAR* o *CANCELAR* para gestionar tu reserva.
```

**Pie de página:** `{{5}} · Powered by Neuracode CRM`

**Variables de ejemplo:**
- `{{1}}` → `Neuracode`
- `{{2}}` → `12/05/2026`
- `{{3}}` → `10:00 AM`
- `{{4}}` → `Videollamada (Google Meet)`
- `{{5}}` → `Neuracode`

---

## Ejemplos — Payloads reales

### Webhook inbound (POST de Meta)

```json
{
  "object": "whatsapp_business_account",
  "entry": [{
    "id": "1612237300105652",
    "changes": [{
      "value": {
        "messaging_product": "whatsapp",
        "metadata": {
          "display_phone_number": "+51 997 055 975",
          "phone_number_id": "1003010076238371"
        },
        "contacts": [{
          "profile": { "name": "Juan Pérez" },
          "wa_id": "51987654321"
        }],
        "messages": [{
          "from": "51987654321",
          "id": "wamid.HBgNNTQ5NzQ3MjQ5NzQ3MjQ5NQA=",
          "timestamp": "1746820800",
          "text": { "body": "Hola, quiero información sobre sus servicios" },
          "type": "text"
        }]
      },
      "field": "messages"
    }]
  }]
}
```

### Verificación de webhook (GET de Meta)

```
GET /api/webhooks/whatsapp
  ?hub.mode=subscribe
  &hub.verify_token=neuracode-crm-webhook-2026
  &hub.challenge=1234567890

Response: 1234567890 (solo el challenge, HTTP 200)
```

### Envío de template outbound

```bash
POST https://graph.facebook.com/v25.0/1003010076238371/messages
Authorization: Bearer {META_ACCESS_TOKEN}
Content-Type: application/json

{
  "messaging_product": "whatsapp",
  "recipient_type": "individual",
  "to": "51987654321",
  "type": "template",
  "template": {
    "name": "crm_seguimiento",
    "language": { "code": "es" },
    "components": [{
      "type": "body",
      "parameters": [
        { "type": "text", "text": "Juan" },
        { "type": "text", "text": "María García" },
        { "type": "text", "text": "Neuracode" },
        { "type": "text", "text": "08/05/2026" }
      ]
    }]
  }
}
```

### Respuesta exitosa de envío

```json
{
  "contacts": [{ "input": "51987654321", "wa_id": "51987654321" }],
  "messages": [{
    "id": "wamid.HBgNNTQ5NzQ3...",
    "message_status": "accepted",
    "messaging_product": "whatsapp"
  }]
}
```

---

## Consecuencias

**Positivo:**
- Leads de WhatsApp entran al CRM automáticamente — cero fricción
- Follow-ups desde el CRM sin salir de la interfaz
- Historial de conversación WhatsApp visible en el timeline del contacto
- Número ya verificado y en GREEN — listo para producción

**Negativos / Trade-offs:**
- Opt-in obligatorio para outbound frío — no se puede hacer cold messaging
- Templates necesitan aprobación Meta 24–48h — no es inmediato
- Precios por mensaje desde julio 2025 — monitorear costos al escalar
- `message_status: "held_for_quality_assessment"` puede ocurrir si el algoritmo de Meta detecta patrones inusuales — no hay control sobre esto
- Token actual es temporal (24h) — necesita token de sistema permanente para producción

**Deuda técnica introducida:**
- Necesita tabla `whatsapp_messages` en DB para deduplicación por `wamid`
- Necesita campo `wa_id` y `opted_out` en tabla `contacts`
- Necesita campo `wamid` en tabla `activities` para correlacionar estado de entrega

---

## Implementación — Orden de pasos

1. ✅ **Crear token de sistema permanente** — hecho 2026-05-10
2. ⏳ **Crear templates en español** — creados via API 2026-05-11, pendiente aprobación Meta
   - `crm_bienvenida` → ID `932571776404193`, UTILITY, PENDING
   - `crm_seguimiento` → ID `1540799197475075`, UTILITY, PENDING (recreado 2026-05-11 con texto transaccional)
   - `crm_propuesta_lista` → ID `1295681522630409`, MARKETING, PENDING
   - `crm_recordatorio_cita` → ID `2136280823824071`, UTILITY, PENDING
   - Nota: Meta auto-recategorizó `crm_seguimiento` original a MARKETING → eliminado y reescrito con referencia a caso/número para cumplir criterio UTILITY
3. ✅ **Endpoint webhook inbound** `POST /api/webhooks/whatsapp` — hecho, con HMAC-SHA256
4. ✅ **Endpoint verificación** `GET /api/webhooks/whatsapp` — hecho
5. ✅ **Migración DB** — `wa_id`, `opted_out` en contacts; `wamid` en activities; tabla `whatsapp_messages`
6. ✅ **Registrar webhook** — registrado 2026-05-11 via serveo.net tunnel
   - URL: `https://52464e9dd9cbfc10-38-224-48-22.serveousercontent.com/api/webhooks/whatsapp`
   - Suscrito a: `messages` + `message_template_status_update` + otros
   - Flujo inbound probado: mensaje WhatsApp → contacto creado en DB → actividad registrada ✓
   - Nota: URL serveo es temporal (sesión). En producción: URL fija del servidor
7. ✅ **Servicio outbound** `WhatsAppService.SendTemplateAsync()` + `POST /api/contacts/{id}/whatsapp/send`
8. ⬜ **UI en detalle de contacto**: botón "Enviar WhatsApp" + historial de mensajes

---

## Patrones relacionados

- `docs/specs/project-restructure/spec.md` — contexto arquitectura CRM
- `docs/adr/` — decisión de stack .NET + Next.js
- `docs/CONTEXT.md` — perfiles de cliente A/B/C
