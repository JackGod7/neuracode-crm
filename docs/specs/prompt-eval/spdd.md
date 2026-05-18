# SPDD — Prompt Eval Framework (LLM-as-Judge)

**Status**: implementing  
**Date**: 2026-05-18  
**Owner**: Jack Aguilar  
**Track**: both (T1 base, T2 reusable per tenant)

---

## Requirements

1. Ejecutar automáticamente con `dotnet test` cuando `ANTHROPIC_API_KEY` está seteado; skip silencioso si no.
2. Evaluar respuestas del agente en ≥8 escenarios reales de clientes peruanos.
3. LLM-as-Judge: Haiku puntúa cada respuesta en 3 dimensiones: naturalidad (1-5), precisión (1-5), deflectado (bool).
4. Assert: naturalidad ≥ umbral, precisión ≥ umbral, deflection = false cuando corresponde.
5. Escenarios definidos en JSON — agregar nuevos sin tocar código.
6. Motor genérico: `businessPrompt` inyectable → reutilizable para cualquier cliente.
7. Multi-turn: enviar N mensajes por turno (debounce los agrupa), esperar respuesta, continuar.

---

## Entities

| Entidad | Campos |
|---------|--------|
| `EvalTurn` | `Messages: string[]`, `Name: string` |
| `EvalExpected` | `MinNaturalidad: int`, `MinPrecision: int`, `DeflectionForbidden: bool` |
| `EvalScenario` | `Name`, `Description`, `Turns: EvalTurn[]`, `Expected: EvalExpected` |
| `JudgeVerdict` | `Naturalidad: int`, `Precision: int`, `Deflectado: bool`, `Razon: string` |
| `LlmJudge` | Llama Haiku, retorna `JudgeVerdict` |

---

## Approach

```
JSON scenarios
     ↓
[Theory] MemberData (carga estática)
     ↓ por escenario
Para cada turno:
  1. POST mensajes rápido (debounce los agrupa)
  2. Esperar debounce (50ms) + agente (≤15s) + buffer
  3. Leer /chat endpoint → último outbound
     ↓
LlmJudge.EvaluateAsync(conversación, reply, deflectionForbidden)
  → Haiku retorna JSON {naturalidad, precision, deflectado, razon}
     ↓
Assert scores ≥ umbral
```

---

## Structure

```
api/tests/Neuracode.Crm.Tests/
  EvalTests/
    EvalTypes.cs                   ← domain types
    LlmJudge.cs                    ← Haiku judge
    PromptEvalTests.cs             ← [Theory] xUnit
    scenarios/
      accesorios-para-el.json      ← 8 escenarios cliente peruano
docs/specs/prompt-eval/spdd.md
```

---

## Operations

**O1 — LlmJudge.EvaluateAsync**  
Pre: `agentReply` no nulo, `apiKey` presente  
Input: `conversation: string`, `agentReply: string`, `deflectionForbidden: bool`  
Output: `JudgeVerdict?` (null si falla)  
Post: timeout 10s, no propaga excepción

**O2 — PromptEvalTests.RunTurns**  
Pre: `HasApiKey`, factory con `CapturingWhatsAppService` + real `AgentService`  
Input: `EvalScenario`  
Output: `(lastReply: string?, fullConversation: string)`  
Post: waId único por escenario (sin colisión entre tests)

---

## Norms

- **N1**: Skip silencioso si no hay `ANTHROPIC_API_KEY` — nunca falla en CI sin key.
- **N2**: Judge usa `claude-haiku-4-5-20251001` fijo — independiente del `AGENT_MODEL` env var.
- **N3**: JSON es autoridad de escenarios — cero cambios de código para agregar/modificar.
- **N4**: Delay por turno = 7000ms (debounce 50ms + API ≤5s + buffer). Configurable.
- **N5**: `deflectionForbidden=true` → agente no puede decir "asesor te contactará" para info general.

---

## Safeguards

- **S1**: Judge falla/timeout → test falla con mensaje claro, no skip.
- **S2**: `agentReply == null` → fallo inmediato, sin llamar al judge.
- **S3**: Cada test usa `waId` único — sin interferencia entre escenarios paralelos.
- **S4**: `RealAgentFactory` hereda `AGENT_DEBOUNCE_MS=50` de `NeuracodeFactory` — tests rápidos sin 4s de espera.
