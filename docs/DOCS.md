# Docs Governance — Neuracode CRM

Reglas de qué va dónde. Sin esto, docs crecen sin orden y nadie los lee.

---

## Estructura canónica

```
docs/
├── CONTEXT.md          ← domain model + backlog vivo (única fuente de verdad de negocio)
├── AGENTS.md           ← team-1, agent roles, escalation ladder
├── DOCS.md             ← este archivo (governance)
├── adr/                ← decisiones arquitectónicas costosas de revertir
│   └── NNNN-nombre.md
├── specs/              ← specs de features funcionales
│   ├── _template.md    ← REASONS canvas base
│   └── <feature>/
│       ├── spdd.md     ← spec principal (REASONS canvas)
│       └── features/   ← archivos .feature (Gherkin, opcional)
├── guides/             ← how-to operacionales (deploy, configuración, runbooks)
│   └── deploy-vps.md
├── track1-ghl/         ← SOPs Track 1 reseller
├── track2-hipaa/       ← compliance Track 2 HIPAA
├── openapi.yaml        ← contrato REST (generado o mantenido manualmente)
└── postman/            ← colecciones de prueba manual
```

---

## Regla de oro: ¿qué archivo crear?

| Situación | Archivo |
|-----------|---------|
| Nuevo feature funcional (endpoint, agent L-X, tabla) | `docs/specs/<feature>/spdd.md` |
| Decisión técnica costosa de revertir (DB engine, auth, deploy) | `docs/adr/NNNN-nombre.md` |
| Cómo operar/deployar algo (pasos manuales, runbook) | `docs/guides/<tema>.md` |
| SOP comercial Track 1 | `docs/track1-ghl/<nombre>.md` |
| Compliance Track 2 | `docs/track2-hipaa/<nombre>.md` |
| Cambio en dominio / backlog | `docs/CONTEXT.md` (editar, no crear) |
| Nada de lo anterior | No crear doc. El código y los commits son la doc. |

---

## Naming de carpetas en `specs/`

### Agent intelligence (niveles L)

Los features de inteligencia del agente WhatsApp siguen la escalera L:

```
docs/specs/
├── whatsapp-agent/          ← sistema base (L1) + Gherkin + test-cases
├── agent-l2-memory/         ← L2: memoria conversacional persistente  [Implemented]
├── agent-l3-tool-use/       ← L3: tool use Anthropic (precios/stock)  [Implemented]
├── agent-l4-state-machine/  ← L4: flujo guiado de ventas              [Pending]
├── agent-l5-human-tone/     ← L5: tono humano, typing delay           [Pending]
└── agent-l6-handoff/        ← L6: resumen handoff a asesor            [Pending]
```

Convención: `agent-lX-<descripcion-corta>/spdd.md`

### Features de plataforma (sin L)

```
docs/specs/
├── wa-conversation-window/  ← ventana 24h WA                [Implemented]
├── prompt-eval/             ← framework LLM-as-Judge        [Implemented]
├── products-catalog/        ← tabla products + CRUD         [Draft]
└── track2-voice/            ← Retell AI + HIPAA             [Draft]
```

Convención: `<dominio-kebab>/spdd.md`

### Nombres prohibidos

```
❌ feature-23    ❌ PRODUCTS    ❌ jack_new_idea    ❌ temp    ❌ misc
```

Cada carpeta contiene exactamente un `spdd.md`. Archivos adicionales solo si son necesarios:
- `.feature` — Gherkin para tests de contrato
- `roadmap.md` — planificación interna (no spec, no ADR)
- `test-cases.md` — casos de prueba detallados

---

## Lifecycle de un spec

```
Draft → Approved → Implemented → Superseded
```

| Estado | Significado |
|--------|------------|
| `Draft` | Escribiéndose, no implementar aún |
| `Approved` | Jack revisó y aprobó, listo para implementar |
| `Implemented` | Código en main, tests passing |
| `Superseded` | Reemplazado por otro spec (poner referencia al nuevo) |

Regla: **nunca borrar** un spec implementado o superseded — es historia del sistema.

---

## Numeración de ADRs

Formato: `NNNN-kebab-case.md` donde NNNN es secuencial desde 0001.

```
docs/adr/
├── 0001-dotnet10-migration.md
├── 0002-sqlite-local-first.md
└── 0003-strangler-fig.md
```

Cada ADR tiene secciones fijas: Context / Decision / Consequences / Status.

---

## CONTEXT.md — reglas de edición

- **Backlog vivo**: mover items entre En progreso / Pendiente / Completado en cada sprint.
- **Completado**: incluir fecha `(YYYY-MM-DD)` al mover.
- **Deuda técnica**: sección propia, no mezclar con backlog de features.
- No agregar detalle de implementación — ese detalle va en `specs/`.

---

## Lo que NO va en docs/

- Código de ejemplo (va en tests)
- Decisiones reversibles sin costo (va en el PR description)
- TODOs temporales (van en CONTEXT.md backlog o en el código como `// TODO: ticket-X`)
- Credenciales, tokens, secrets (nunca en repo)
