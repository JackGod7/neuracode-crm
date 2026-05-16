# WhatsApp Business Webhooks — Reference

Source: https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview/  
API version: v25.0

---

## Overview

Webhooks deliver JSON payloads to your server for:
- Inbound messages (text, media, interactive replies, etc.)
- Outbound message status updates (sent → delivered → read / failed)
- Account-level events (account_alerts, account_update, etc.)

Payloads up to **3 MB**. Meta retries on non-200 responses with decreasing frequency for **up to 7 days**.

---

## Webhook Verification (GET)

When you register or update a webhook, Meta sends a GET request to verify the endpoint.

```
GET <callback_url>
  ?hub.mode=subscribe
  &hub.verify_token=<your_verify_token>
  &hub.challenge=<random_string>
```

**Server must:**
1. Confirm `hub.mode == "subscribe"`
2. Confirm `hub.verify_token` matches your configured token
3. Respond HTTP 200 with body = `hub.challenge` (plain text, no JSON)

**Our implementation:** `GET /api/webhooks/whatsapp` in `WhatsAppEndpoints.cs`

---

## Signature Validation (POST)

Every inbound POST includes header:

```
X-Hub-Signature-256: sha256=<hex_digest>
```

Validation:
1. Compute `HMAC-SHA256(raw_request_body, META_APP_SECRET)`
2. Hex-encode the digest
3. Compare with value after `sha256=` prefix (constant-time comparison)
4. Reject with 403 if mismatch

**Our implementation:** Skip if `META_APP_SECRET` not configured (dev mode).

---

## Payload Structure

```json
{
  "object": "whatsapp_business_account",
  "entry": [
    {
      "id": "<WABA_ID>",
      "changes": [
        {
          "field": "messages",
          "value": {
            "messaging_product": "whatsapp",
            "metadata": {
              "display_phone_number": "+51997055975",
              "phone_number_id": "1003010076238371"
            },
            "contacts": [
              {
                "profile": { "name": "Juan Pérez" },
                "wa_id": "51965132214"
              }
            ],
            "messages": [
              {
                "from": "51965132214",
                "id": "wamid.HBgLNTE5NjUxMzIyMTQVAgARGBI...",
                "timestamp": "1747008000",
                "type": "text",
                "text": { "body": "Hola, quiero información" }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

---

## Message Types

| `type` | Extra field | Notes |
|--------|-------------|-------|
| `text` | `text.body` | Plain text |
| `image` | `image.id`, `image.mime_type`, `image.sha256`, `image.caption` | Media ID → download separately |
| `audio` | `audio.id`, `audio.mime_type`, `audio.voice` | `voice: true` = voice note |
| `video` | `video.id`, `video.mime_type`, `video.caption` | |
| `document` | `document.id`, `document.filename`, `document.mime_type`, `document.caption` | |
| `sticker` | `sticker.id`, `sticker.mime_type`, `sticker.animated` | |
| `location` | `location.latitude`, `location.longitude`, `location.name`, `location.address` | |
| `contacts` | `contacts[]` | vCard array |
| `reaction` | `reaction.message_id`, `reaction.emoji` | Emoji reaction to a message |
| `interactive` | `interactive.type`, `interactive.button_reply` / `interactive.list_reply` | User tapped a button |
| `order` | `order.catalog_id`, `order.product_items[]` | Catalog purchase |
| `system` | `system.type`, `system.body` | Customer changed number, etc. |
| `unsupported` | — | Unknown type; send fallback reply |

---

## Status Updates

Status webhooks arrive in the same `entry[].changes[].value` but use a `statuses` array instead of `messages`:

```json
{
  "statuses": [
    {
      "id": "wamid.HBgL...",
      "status": "delivered",
      "timestamp": "1747008010",
      "recipient_id": "51965132214",
      "conversation": {
        "id": "<conv_id>",
        "origin": { "type": "service" }
      },
      "pricing": {
        "billable": true,
        "pricing_model": "CBP",
        "category": "service"
      }
    }
  ]
}
```

Status flow: `sent` → `delivered` → `read`  
On failure: `failed` with `errors[]` array:

```json
"errors": [
  {
    "code": 131026,
    "title": "Message undeliverable",
    "message": "...",
    "error_data": { "details": "..." },
    "href": "https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes/"
  }
]
```

---

## Conversation Categories (Pricing)

| Category | Triggered by |
|----------|-------------|
| `service` | User messages your business (customer-initiated) |
| `authentication` | OTP / auth templates |
| `marketing` | Promotional outbound templates |
| `utility` | Transactional outbound templates |
| `referral_conversion` | Click-to-WhatsApp ad |

Free tier: first 1,000 service conversations/month.

---

## Required HTTP Response

- **Verification GET:** `200 OK` + `hub.challenge` as body
- **All POST webhooks:** `200 OK` within **20 seconds** (empty body OK)
- If your handler needs >20s: respond 200 immediately, process async

---

## Key Fields Reference

### `metadata`
| Field | Description |
|-------|-------------|
| `display_phone_number` | Human-readable business number |
| `phone_number_id` | ID used in send-message API calls |

### `message`
| Field | Type | Description |
|-------|------|-------------|
| `id` | string | `wamid.*` — unique message ID for deduplication |
| `from` | string | Sender's wa_id (phone without `+`) |
| `timestamp` | string | Unix epoch string |
| `type` | string | See Message Types table |
| `context` | object | Present if reply; `context.id` = original wamid |
| `errors` | array | Present on `unsupported` type |

### `contact`
| Field | Description |
|-------|-------------|
| `wa_id` | Phone number (no `+`), matches `message.from` |
| `profile.name` | Display name from WhatsApp profile |

---

## Wamid Deduplication

Meta may deliver the same webhook more than once (at-least-once delivery). Always store `wamid` and check before processing:

```sql
-- Our schema: whatsapp_messages.wamid has UNIQUE constraint
INSERT INTO whatsapp_messages (wamid, ...) ...
-- If duplicate → UNIQUE violation → ignore, return 200
```

---

## Error Codes Reference

Common codes sent in `statuses[].errors[].code`:

| Code | Meaning |
|------|---------|
| 130472 | User's number not on WhatsApp |
| 131026 | Message undeliverable (blocked / unreachable) |
| 131047 | Re-engagement message required (24h window expired) |
| 131051 | Unsupported message type |
| 131052 | Media download failed |
| 131053 | Media upload failed |
| 132000 | Template parameter count mismatch |
| 132001 | Template not found or approved |
| 133004 | Server temporarily unavailable |

Full list: https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes/

---

## Webhook Registration (via Meta Dashboard)

1. Meta for Developers → App → WhatsApp → Configuration
2. **Callback URL:** `https://<your-domain>/api/webhooks/whatsapp`
3. **Verify Token:** value of `META_VERIFY_TOKEN` env var
4. Subscribe to fields: `messages`
5. Click **Verify and Save**

For local dev: use [ngrok](https://ngrok.com/) or [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/) to expose localhost.

---

## Our Implementation Map

| Spec step | File | Status |
|-----------|------|--------|
| GET verify | `WhatsAppEndpoints.cs` → `MapGet` | ✅ |
| POST inbound + HMAC | `WhatsAppEndpoints.cs` → `MapPost` | ✅ |
| wamid dedup | `AppDbContext.cs` UNIQUE index on `wamid` | ✅ |
| Find-or-create contact by `wa_id` | `WhatsAppEndpoints.cs` | ✅ |
| Outbound send template | `WhatsAppService.cs` | ✅ |
| Status webhook handling | Not yet implemented | ⬜ |
| Media download | Not yet implemented | ⬜ |
