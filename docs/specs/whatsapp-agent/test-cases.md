# Casos de Prueba — Agente WhatsApp Accesorios Para Él

Evaluación del agente IA en producción (`crm-api-production-dd1c.up.railway.app`).  
Número WA: +51 997 055 975 | Modelo: Claude Haiku 4.5

---

## Resultados reales (sesión 2026-05-17)

### PROMPT V1 — Genérico (descartado)

| # | Cliente | Agente | Resultado | Problema |
|---|---------|--------|-----------|----------|
| T01 | "Hola" | "¡Hola! Bienvenido a nuestro servicio de atención al cliente..." | ⚠️ PARCIAL | Sin identidad de marca |
| T02 | "Qué eres?" | "Soy tu asistente de atención al cliente..." | ✅ OK | Deflecta correctamente |
| T03 | "Puedes programar en python?" | "Soy el asistente de atención al cliente..." | ✅ OK | Ignora off-topic, redirige |
| T04 | "O en qué estás programado? Qué lenguaje" | *(sin respuesta)* | ❌ FALLO | Dos msgs simultáneos → solo 1 outbound |
| T05 | "Eres un agente ia con un CRM?" | "Soy un asistente de atención al cliente..." | ✅ OK | No revela stack |
| T06 | "Usas claude?" | "Soy un asistente diseñado para ayudarte..." | ✅ OK | No revela modelo |

### PROMPT V2 — Joyería (activo)

| # | Cliente | Agente | Resultado | Notas |
|---|---------|--------|-----------|-------|
| T07 | "hola" | "Hola Jack, bienvenido a Accesorios Para Él, te ayudo con información sobre nuestras colecciones de brazaletes y pulseras..." | ✅ OK | Nombra la tienda, menciona categorías |
| T08 | "ayuda con las joyas, qué me recomiendas como anillo de compromiso tienes?" | "...contamos con brazaletes y pulseras premium en colecciones METROPOLE, BOSS, E.ARMANI y CROCODILE... Combos Love para parejas. Un asesor puede contactarte..." | ⚠️ PARCIAL | No aclara que no venden anillos; debería corregir la expectativa |

---

## Casos pendientes de prueba

### Catálogo y productos
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T09 | "¿cuánto cuesta la pulsera BOSS?" | Rango S/. 149–607, asesor da precio exacto | ⬜ pendiente |
| T10 | "¿tienen anillos de compromiso?" | Aclarar que venden brazaletes/pulseras, no anillos | ⬜ pendiente |
| T11 | "busco algo para regalar a mi pareja" | Mencionar Combos Love para parejas | ⬜ pendiente |
| T12 | "¿tienen en color negro?" | Confirmar finishes disponibles (plateado, dorado, negro, verde) | ⬜ pendiente |
| T13 | "¿qué diferencia hay entre METROPOLE y BOSS?" | Derivar a asesor (agente no tiene detalle técnico) | ⬜ pendiente |

### Logística
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T14 | "¿hacen envíos a Trujillo?" | Sí, todo Perú, 2-3 días hábiles | ⬜ pendiente |
| T15 | "¿envían a Chile?" | Sí, envíos internacionales | ⬜ pendiente |
| T16 | "¿cuánto cuesta el envío?" | Derivar a asesor (precio no está en prompt) | ⬜ pendiente |

### Pagos y proceso de compra
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T17 | "¿cómo pago?" | Yape, Plin, transferencia, tarjeta, contra entrega | ⬜ pendiente |
| T18 | "¿puedo apartar?" | Sí, con S/. 40 de adelanto | ⬜ pendiente |
| T19 | "¿aceptan efectivo?" | Sí, contra entrega | ⬜ pendiente |

### Postventa
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T20 | "¿puedo cambiar si no me gusta?" | Cambios hasta 15 días desde la compra | ⬜ pendiente |
| T21 | "me llegó un producto dañado" | Derivar a asesor con urgencia | ⬜ pendiente |

### Horario
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T22 | "¿están abiertos los domingos?" | Sí, todos los días 9am–7pm | ⬜ pendiente |
| T23 | "¿a qué hora abren?" | 9am, todos los días | ⬜ pendiente |

### Escalación y handoff
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T24 | "quiero hablar con una persona" | ESCALAR → sin outbound → contacto con botHandling=false | ⬜ pendiente |
| T25 | "¡son una estafa!" | Respuesta empática + escalar | ⬜ pendiente |
| T26 | POST /handoff → nuevo mensaje | Agente no responde (botHandling=false) | ✅ probado en tests |
| T27 | POST /bot-resume → nuevo mensaje | Agente responde de nuevo | ✅ probado en tests |

### Robustez
| # | Input del cliente | Comportamiento esperado | Estado |
|---|------------------|------------------------|--------|
| T28 | Dos mensajes en < 2s | Al menos 1 respuesta, sin duplicados | ⬜ pendiente (T04 falló) |
| T29 | Mensaje muy largo (+500 chars) | Respuesta normal, no colapsa | ⬜ pendiente |
| T30 | Mensaje en inglés | Responde en español | ⬜ pendiente |
| T31 | Solo emojis "🔥🙏" | Respuesta coherente | ⬜ pendiente |
| T32 | "¿usas Claude?" / "¿usas IA?" | No revela modelo ni stack | ✅ OK (T06) |

---

## Defectos conocidos

| ID | Descripción | Prioridad | Fix sugerido |
|----|-------------|-----------|--------------|
| BUG-01 | Msgs simultáneos → solo 1 outbound | Alta | Mutex por contactId en HandleInbound |
| BUG-02 | No aclara productos fuera de catálogo (ej: anillos) | Media | Agregar al prompt lista de lo que NO se vende |
| BUG-03 | Respuestas V1 genéricas sin identidad de marca | Cerrado | Resuelto con PROMPT V2 |

---

## Mejoras pendientes (roadmap agente)

| Nivel | Feature | Impacto |
|-------|---------|---------|
| L2 | Memoria entre sesiones (nombre, historial compras) | Alto |
| L3 | Tool use: consultar stock/precio en tiempo real | Alto |
| L4 | Flujo guiado: saludo → califica → ofrece → cierra | Muy alto |
| L5 | Handoff inteligente con resumen para el asesor | Alto |
