# WhatsApp Template Library — Reference

## Qué es

Meta ofrece una biblioteca de plantillas pre-revisadas. Al agregarlas a tu WABA, la aprobación es **inmediata o en horas** (vs 24–48h para templates personalizados nuevos), porque el contenido ya pasó los filtros de Meta.

## URLs de acceso

```
# WABA producción (1612237300105652)
https://business.facebook.com/latest/whatsapp_manager/template_library/?business_id=750987417816588&tab=template-library&asset_id=1612237300105652

# WABA test (1249235120170408)
https://business.facebook.com/latest/whatsapp_manager/template_library/?business_id=750987417816588&tab=template-library&asset_id=1249235120170408
```

## Cómo usar

1. Entrar a la URL de tu WABA (producción o test)
2. Filtrar por categoría (Utility / Marketing / Authentication) e idioma (es_MX)
3. Click en plantilla → "Usar plantilla"
4. Personalizar variables y nombre
5. Enviar para revisión → aprobación inmediata/rápida

## Estrategia para el CRM

En lugar de esperar aprobación de los 4 templates personalizados creados via API, buscar en la biblioteca templates equivalentes y adaptarlos:

| Nuestro template | Buscar en biblioteca |
|-----------------|---------------------|
| `crm_bienvenida` | "welcome", "confirmation", "greeting" — categoría Utility |
| `crm_seguimiento` | "follow_up", "check_in" — categoría Utility |
| `crm_propuesta_lista` | "offer", "quote_ready" — categoría Marketing |
| `crm_recordatorio_cita` | "appointment_reminder" — categoría Utility |

## Templates propios ya creados (pendientes de aprobación)

Creados via API el 2026-05-11 en WABA producción `1612237300105652`:

| Nombre | ID | Categoría | Estado |
|--------|----|-----------|--------|
| `crm_bienvenida` | 932571776404193 | UTILITY | PENDING |
| `crm_seguimiento` | 2083504005630507 | UTILITY | PENDING |
| `crm_propuesta_lista` | 1295681522630409 | MARKETING | PENDING |
| `crm_recordatorio_cita` | 2136280823824071 | UTILITY | PENDING |

Si la aprobación tarda, usar equivalentes de la biblioteca como fallback.

## Verificar estado via API

```bash
. .env.local && curl -s \
  "https://graph.facebook.com/v25.0/${META_WABA_ID}/message_templates?fields=name,status,id&limit=20" \
  -H "Authorization: Bearer ${META_ACCESS_TOKEN}"
```

## Nota sobre verificación de negocio

Business verification rechazada → aprobación de templates personalizados puede tardar más. Templates de biblioteca tienen mayor prioridad en revisión automática.

Para re-intentar verificación: Meta Business Manager → Security Center → Business Verification → subir documentos nuevos (RUC SUNAT, extracto bancario, o carta membrete empresa).
