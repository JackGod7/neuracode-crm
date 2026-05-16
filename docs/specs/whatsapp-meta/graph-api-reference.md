# Meta Graph API — Reference

Source: https://developers.facebook.com/docs/graph-api  
Version: v25.0

---

## Qué es

La Graph API es la interfaz principal para leer y escribir en el grafo social de Meta. Toda interacción con WhatsApp Business (envío de mensajes, gestión de templates, webhooks) usa Graph API como capa de transporte.

---

## Base URL y Versioning

```
https://graph.facebook.com/v25.0/{node}/{edge}
```

- **Node**: objeto individual (phone number, WABA, message template)
- **Edge**: colección relacionada a un nodo (`/message_templates`, `/messages`, `/phone_numbers`)
- **Version**: `v25.0` — siempre fijar versión explícita. Sin versión → usa versión mínima compatible (puede romper)

Versiones disponibles: v19.0 en adelante. v25.0 es la más reciente (mayo 2026).

---

## Autenticación

Todas las requests requieren access token. Tres métodos:

### Query param (no recomendado en producción)
```
GET /v25.0/me?access_token=EAAVdp...
```

### Authorization header (recomendado)
```
Authorization: Bearer EAAVdp...
```

### App token (solo endpoints sin contexto de usuario)
```
Authorization: Bearer {app_id}|{app_secret}
```

### Tipos de token

| Tipo | Duración | Uso |
|------|----------|-----|
| User token | ~2 meses (renovable) | Dev, testing |
| System User token | No expira (hasta revocar) | Producción |
| App token | No expira | Server-to-server sin usuario |
| Page token | Configurable | Pages API |

**Para WhatsApp Business en producción: System User token.**

Verificar token: `GET /debug_token?input_token=TOKEN&access_token=APP_ID|APP_SECRET`

---

## Endpoints WhatsApp Cloud API

### Enviar mensaje
```
POST /v25.0/{phone_number_id}/messages
Authorization: Bearer {token}
Content-Type: application/json
```

### Listar templates
```
GET /v25.0/{waba_id}/message_templates?fields=name,status,language,category
Authorization: Bearer {token}
```

### Crear template
```
POST /v25.0/{waba_id}/message_templates
Authorization: Bearer {token}
Content-Type: application/json
```

### Eliminar template
```
DELETE /v25.0/{waba_id}/message_templates?name={template_name}
Authorization: Bearer {token}
```

### Listar números de teléfono del WABA
```
GET /v25.0/{waba_id}/phone_numbers?fields=display_phone_number,id,verified_name,quality_rating
Authorization: Bearer {token}
```

### Descargar media (imágenes, audio, docs recibidos vía webhook)
```
GET /v25.0/{media_id}              → retorna URL temporal (5 min)
GET {url_temporal}                 → descarga binario
Authorization: Bearer {token}      → requerido en ambas calls
```

---

## Formato de Request/Response

### Request
```json
{
  "messaging_product": "whatsapp",
  "recipient_type": "individual",
  "to": "51987654321",
  "type": "template",
  "template": {
    "name": "crm_seguimiento",
    "language": { "code": "es_MX" },
    "components": [{
      "type": "body",
      "parameters": [
        { "type": "text", "text": "Juan" },
        { "type": "text", "text": "María" },
        { "type": "text", "text": "Neuracode" },
        { "type": "text", "text": "10/05/2026" }
      ]
    }]
  }
}
```

### Response exitoso
```json
{
  "messaging_product": "whatsapp",
  "contacts": [{ "input": "51987654321", "wa_id": "51987654321" }],
  "messages": [{
    "id": "wamid.HBgNNTE5ODc2NTQzMjE...",
    "message_status": "accepted"
  }]
}
```

`message_status` posibles: `accepted` | `held_for_quality_assessment` | `paused`

---

## Estructura de Errores

```json
{
  "error": {
    "message": "descripción legible",
    "type": "OAuthException | GraphMethodException | ...",
    "code": 100,
    "error_subcode": 33,
    "error_user_title": "título para el usuario",
    "error_user_msg": "mensaje para el usuario",
    "fbtrace_id": "AYhGqZ_72Lk8..."
  }
}
```

### Códigos de error frecuentes

| Code | Subcode | Significado |
|------|---------|-------------|
| 100 | 33 | Objeto no existe o sin permisos |
| 100 | 2388299 | Variable al inicio/final del template |
| 131058 | — | hello_world solo desde test numbers |
| 190 | — | Token inválido o expirado |
| 190 | 460 | Token expirado |
| 190 | 463 | Token expirado (sesión) |
| 200 | — | Sin permisos para este endpoint |
| 4 | — | Rate limit de app alcanzado |
| 17 | — | Rate limit de usuario alcanzado |
| 10 | — | Permiso no otorgado (scope faltante) |
| 341 | — | Rate limit de marketing (demasiados mensajes) |

---

## Paginación

Respuestas con múltiples registros incluyen cursor-based pagination:

```json
{
  "data": [...],
  "paging": {
    "cursors": {
      "before": "QVFIU...",
      "after": "QVFIU..."
    },
    "next": "https://graph.facebook.com/v25.0/..."
  }
}
```

Parámetros: `?limit=25&after={cursor}` o `?limit=25&before={cursor}`

---

## Field Expansion

Limitar campos retornados y expandir relaciones en una sola call:

```
GET /v25.0/{waba_id}/phone_numbers
  ?fields=id,display_phone_number,quality_rating,verified_name
  &limit=10
```

Nested expansion:
```
GET /v25.0/{waba_id}
  ?fields=id,name,phone_numbers{id,display_phone_number}
```

---

## Batch Requests

Hasta 50 requests en una sola call HTTP:

```
POST /v25.0/
Authorization: Bearer {token}
Content-Type: application/json

{
  "batch": [
    { "method": "GET", "relative_url": "me" },
    { "method": "GET", "relative_url": "{waba_id}/message_templates?limit=5" }
  ]
}
```

---

## Rate Limits

| Nivel | Límite |
|-------|--------|
| App-level | 200 calls/hora por usuario |
| Business messaging | Depende del tier del número |
| Template creation | 100 templates/hora por WABA |
| Message sending | Depende de quality rating y tier |

Headers de respuesta cuando se acerca el límite:
- `X-App-Usage: {"call_count":28,"total_time":25,"total_cputime":25}`
- `X-Business-Use-Case-Usage`: específico a WhatsApp

---

## Permisos (Scopes) Relevantes para WhatsApp

| Scope | Para qué |
|-------|---------|
| `whatsapp_business_messaging` | Enviar/recibir mensajes |
| `whatsapp_business_management` | Gestionar templates, números, WABA |
| `business_management` | Acceso a Business Manager |

Verificar scopes activos: Token Debugger → sección "Ámbitos".

---

## IDs del proyecto Neuracode

| Recurso | ID |
|---------|-----|
| App | `1510338083784314` |
| WABA producción | `1612237300105652` |
| Phone Number ID producción | `1003010076238371` |
| Número producción | `+51997055975` |
| Test WABA | `1249235120170408` |
| Test Phone Number ID | `1066995643160409` |
| Test Number | `+1 555-179-0688` |

---

## Referencia rápida — WhatsApp Message Types (send)

```json
// Texto libre (dentro de ventana 24h)
{ "type": "text", "text": { "body": "Hola, ¿en qué te ayudo?" } }

// Template (fuera de ventana 24h o primero contacto)
{ "type": "template", "template": { "name": "crm_bienvenida", "language": { "code": "es_MX" }, "components": [...] } }

// Imagen
{ "type": "image", "image": { "id": "{media_id}", "caption": "..." } }

// Documento
{ "type": "document", "document": { "id": "{media_id}", "filename": "propuesta.pdf" } }

// Reacción
{ "type": "reaction", "reaction": { "message_id": "wamid.xxx", "emoji": "👍" } }
```
