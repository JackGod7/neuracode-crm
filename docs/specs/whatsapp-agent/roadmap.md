# WhatsApp Agent — Intelligence Roadmap

**Objetivo**: de FAQ-responder (L1) a agente que cierra ventas y se siente humano.

---

## L1 — FAQ Agent (LIVE 2026-05-18)

- Prompt fijo con catálogo + política de "Accesorios Para Él"
- Escalación `ESCALAR` / handoff / bot-resume
- Idempotencia wamid, mutex por waId, timeout 3s
- Últimas 5 actividades como contexto conversacional
- 139 tests pasando, producción Railway

---

## L2 — Memoria conversacional persistente (PRÓXIMO)

**Qué resuelve**: el agente olvida todo entre conversaciones. No sabe quién es el cliente, qué vio, ni en qué estado está.

**Implementación**:
- Tabla nueva `agent_memory` (waId TEXT PK, data JSON, updated_at INTEGER)
- Post-response: segundo LLM call (Haiku) extrae JSON: `{ name, budget, interested_in, sales_state, notes }`
- Inyectar en prompt como bloque `Memoria del cliente: {json}` antes del system prompt
- Startup migration: `CREATE TABLE IF NOT EXISTS agent_memory (...)`

**Tests**:
- Cliente menciona nombre en msg 1 → agente lo usa en msg 5
- `sales_state = considering` persiste entre sesiones
- Fallo de extracción no propaga excepción (agente sigue funcionando sin memoria)

**Esfuerzo**: ~2-3 días. Track: both.

---

## L3 — Tool use stock/precio real

**Qué resuelve**: agente no sabe precios exactos, los deriva al asesor innecesariamente. Pierde ventas.

**Implementación**:
- Anthropic Messages API con `tools` parameter
- Tools: `get_product_info(sku)` → `{ price_range, colors, stock, discount }`
- Datasource: tabla `products` o `crm_settings["catalog"]` JSON
- Responde precio exacto; solo deriva si stock = 0 o consulta muy específica

**Tests**:
- "¿cuánto el BOSS?" → tool call → responde rango exacto sin asesor
- Tool error → agente degrada a "un asesor te dará el precio exacto"

**Esfuerzo**: ~3-4 días. Track: both.

---

## L4 — Flujo guiado de ventas (state machine)

**Qué resuelve**: agente siempre responde como FAQ-bot. No califica, no recomienda, no cierra.

**Estados**: `greeting → discovery → recommendation → objection_handling → close → handoff`

**Implementación**:
- Estado persiste en `agent_memory.data.sales_state`
- Cada turn: LLM clasifica transición de estado + prompt varía por estado
- `greeting`: cálido, pregunta qué busca
- `discovery`: califica presupuesto, ocasión, destinatario
- `recommendation`: propone 1-2 SKUs específicos con beneficios
- `objection_handling`: precio, dudas, comparaciones
- `close`: CTA asertivo — "¿te lo enviamos a tu dirección hoy?"
- `handoff`: resumen 3-bullet para asesor humano

**Tests**: conversación end-to-end 6 turns llega a `close` con cliente interesado (prompt-eval).

**Esfuerzo**: ~5-6 días. Track: both.

---

## L5 — Tono humano

**Qué resuelve**: respuestas robóticas predecibles. Siempre "Hola Jack, bienvenido a Accesorios Para Él..."

**Cambios**:
- 5 variantes de saludo por contexto (primera vez, retorno, mañana/tarde, casual, formal)
- Usar nombre del cliente (de WA profile o `agent_memory.name`), no hardcoded
- 1 emoji ocasional cuando el cliente usa emojis (toggle en business prompt)
- Typing indicator 1-2s antes de send (Meta `typing_on` action)

**Test prompt-eval**: batch 30 conversaciones, score "feels human" 1-5 con LLM-as-judge. Target: ≥4.

**Esfuerzo**: ~2 días. Track: both.

---

## L6 — Handoff inteligente con resumen

**Qué resuelve**: cuando escala, asesor humano no tiene contexto. Lee conversación desde cero.

**Implementación**:
- Cuando `ESCALAR`: agente genera resumen 3-bullet (LLM call): qué quiere, qué vio, objeciones
- Resumen guardado como `Activity` tipo `handoff_summary`
- Aparece en UI contacto cuando llegue el sprint de frontend

**Esfuerzo**: ~1 día. Track: both.

---

## Sprint order

1. ✅ L1 FAQ Agent (live)
2. **L2 Memoria conversacional** ← próximo sprint
3. **L5 Tono humano** + prompt-eval batch
4. L3 Tool use stock/precio
5. L4 State machine
6. L6 Handoff summary
7. Frontend UI (sprint dedicado posterior)
