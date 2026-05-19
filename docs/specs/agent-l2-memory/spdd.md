# Agent Memory — L2 Conversational Memory

**Status**: Draft | **Date**: 2026-05-18 | **Owner**: Jack Aguilar | **Track**: both

---

## Requirements — qué debe hacer el sistema

1. El agente recuerda datos extraídos de conversaciones anteriores: nombre real del cliente, productos de interés, presupuesto mencionado, estado de venta.
2. La memoria persiste entre sesiones (conversaciones separadas en el tiempo).
3. Post-response: un segundo LLM call (Haiku) extrae JSON estructurado del turn y actualiza la memoria.
4. La memoria se inyecta en el system prompt como bloque `Memoria del cliente:` antes del contexto conversacional.
5. Fallo de extracción no propaga excepción — agente sigue funcionando sin memoria (degradación silenciosa).
6. Fallo de escritura a DB no bloquea el response al cliente.
7. Formato de memoria: JSON flat `{ name, budget, interested_in, sales_state, notes }`.
8. `sales_state` valores iniciales: `browsing | considering | ready_to_buy` (L4 state machine extenderá esto).
9. La extracción no revela al cliente que existe memoria — solo el agente la usa internamente.
10. Test: cliente menciona su nombre en msg 1 → agente lo usa en msg 5 (misma o diferente sesión).

**Out of scope:**
- L4 state machine completa (solo `sales_state` persiste como seed para L4)
- Tool use / consulta de stock (L3)
- UI para ver/editar memoria del cliente (sprint frontend)

---

## Entities — objetos de dominio

| Entidad | Campos clave | Notas |
|---------|-------------|-------|
| `AgentMemory` | `WaId` (PK), `Data` (JSON), `UpdatedAt` (INTEGER) | Una fila por cliente WA. JSON schema: `AgentMemoryData`. |
| `AgentMemoryData` | `Name?`, `Budget?`, `InterestedIn[]`, `SalesState`, `Notes?` | Inmutable tras construcción — siempre reemplazar, nunca mutar. |
| `AgentContext` | Existente + `Memory?` (AgentMemoryData) | Añadir campo opcional sin romper contrato existente. |
| `IAgentMemoryRepository` | `GetAsync(waId)`, `UpsertAsync(waId, data)` | Abstracción para testear sin DB. |

### AgentMemoryData schema

```json
{
  "name": "Juan",
  "budget": "S/. 200-300",
  "interested_in": ["BOSS", "Combos Love"],
  "sales_state": "considering",
  "notes": "compra para regalo de cumpleaños, plazo 1 semana"
}
```

---

## Approach — cómo funciona

```
POST /api/webhooks/whatsapp
  │
  ├─ [existente L1] dedup wamid, upsert contact, guard BotHandling
  │
  ├─ 1. IAgentMemoryRepository.GetAsync(waId)  ← nueva
  │       └─ null si primera vez → AgentMemoryData.Empty
  │
  ├─ 2. Construir AgentContext con memory inyectada
  │       └─ businessPrompt += "\n\nMemoria del cliente:\n" + memory.ToPromptString()
  │
  ├─ 3. AgentService.HandleAsync(ctx)  ← igual que L1
  │       └─ response = texto | ESCALAR | null
  │
  ├─ 4. WhatsAppService.SendTextAsync(waId, response)  ← igual que L1
  │
  └─ 5. [nuevo, fire-and-forget] ExtractMemoryAsync(ctx, response)
          ├─ LLM call (Haiku, timeout 2s): extrae JSON de {userMsg + agentResponse}
          ├─ Merge con memory existente (campos no-null sobrescriben)
          └─ IAgentMemoryRepository.UpsertAsync(waId, merged)
              └─ excepción → log Warning, no propagar
```

**Two-call architecture**: call 1 = agente responde cliente (3s timeout), call 2 = extractor (2s timeout, fire-and-forget en background task).

---

## Structure — árbol de archivos

```
api/src/Neuracode.Crm.Api/
├── Agents/
│   ├── IAgentService.cs               (existente, no cambia)
│   ├── AgentService.cs                (existente, recibe AgentContext actualizado)
│   ├── AgentContext.cs                (agregar campo Memory?)
│   ├── IAgentMemoryRepository.cs      (nuevo)
│   ├── AgentMemoryRepository.cs       (nuevo, EF Core → AgentMemory entity)
│   └── MemoryExtractorService.cs      (nuevo, segundo LLM call)
├── Data/
│   ├── Entities/
│   │   └── AgentMemory.cs             (nuevo entity)
│   └── AppDbContext.cs                (agregar DbSet<AgentMemory>)
├── Domain/
│   └── AgentMemoryData.cs             (nuevo record inmutable)
└── Endpoints/
    └── WhatsAppEndpoints.cs           (inyectar IAgentMemoryRepository, orquestar)

api/tests/Neuracode.Crm.Tests/
├── UnitTests/
│   └── MemoryExtractorTests.cs        (nuevo: extracción JSON, degradación silenciosa)
├── ContractTests/
│   └── AgentMemoryContractTests.cs    (nuevo: webhook → memoria persiste → siguiente webhook usa memoria)
└── Fakes/
    └── FakeAgentMemoryRepository.cs   (nuevo, in-memory dict)
```

---

## Operations — operaciones clave

### O1 — Primera conversación (sin memoria previa)
```
PRECONDITION : AgentMemory row no existe para waId
INPUT        : mensaje inbound "Hola"
OUTPUT       : respuesta normal sin memoria; extractor corre en background → crea row
POSTCONDITION: AgentMemory row existe con data parcial (name si mencionado, sales_state="browsing")
```

### O2 — Conversación de retorno (con memoria)
```
PRECONDITION : AgentMemory row existe con name="Juan", sales_state="considering", interested_in=["BOSS"]
INPUT        : mensaje inbound "¿sigue disponible el BOSS?"
OUTPUT       : agente saluda por nombre, refuerza interés en BOSS, sugiere avanzar
POSTCONDITION: AgentMemory actualizado con notes + posible sales_state="ready_to_buy"
```

### O3 — Extractor falla (degradación silenciosa)
```
PRECONDITION : Claude API no responde en 2s para extractor
INPUT        : cualquier mensaje inbound
OUTPUT       : agente responde normalmente (call 1 ya completó)
POSTCONDITION: memoria no actualizada; NO excepción; log Warning "Memory extraction timeout"
```

### O4 — Handoff (memoria preserva estado)
```
PRECONDITION : contact.BotHandling=false (post-handoff)
INPUT        : nuevo mensaje inbound
OUTPUT       : agente NO responde (BotHandling guard existente)
POSTCONDITION: memoria no actualiza (extractor no corre si no hubo response)
```

---

## Norms — restricciones

| # | Norma | Justificación |
|---|-------|--------------|
| N1 | Extractor corre AFTER send, no before | Cliente no debe esperar el segundo LLM call |
| N2 | Timeout extractor 2s (menor que L1 3s) | Background task, no bloquea webhook response |
| N3 | Merge: campos non-null sobrescriben, null preserva valor anterior | No perder datos ya extraídos si turn no los menciona |
| N4 | `AgentMemoryData` es record inmutable | Nunca mutar — siempre new instance en merge |
| N5 | JSON schema máximo 500 tokens en prompt | Evitar degradar latencia del agente principal |
| N6 | Una fila por waId (upsert, no insert) | Un cliente = una memoria centralizada |
| N7 | PHI en memoria → Track 2: RedactPhi() antes de logs de extracción | name/budget/notes son potencialmente PHI |

---

## Safeguards — qué nunca debe ocurrir

| # | Safeguard | Mecanismo |
|---|-----------|-----------|
| S1 | Nunca bloquear response del agente esperando extractor | Extractor en `Task.Run` o `_ = ExtractAsync(...)` post-send |
| S2 | Nunca propagar excepción del extractor | try/catch total en `ExtractMemoryAsync`, log Warning |
| S3 | Nunca exponer `AgentMemoryData` raw en API response | Tabla interna, sin endpoint público |
| S4 | Nunca escribir memoria si no hubo response exitoso | Extractor solo se llama si `agentReply != null` |
| S5 | Nunca corromper memoria con JSON inválido | Deserialización con `try/catch`; si falla → no hacer upsert |

---

## Patrones relacionados

- `docs/specs/whatsapp-agent/spdd.md` — L1 base (AgentContext, IAgentService)
- `docs/specs/whatsapp-agent/roadmap.md` — roadmap completo L1-L6
- `docs/adr/` — ADR si el segundo LLM call eleva costo significativamente
