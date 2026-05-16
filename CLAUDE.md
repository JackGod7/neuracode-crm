# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Neuracode CRM

Local-first CRM. .NET 10 + Next.js 16 + SQLite. Dos deploy profiles:
- Track 1: local-only, no auth, no PHI (ADR-0004)
- Track 2: HIPAA-compliant single-tenant por cliente (ADR-0007)

Mismo código. Profile gatea qué seams se activan.

## Run

```
dotnet run --project api/src/Neuracode.Crm.Api        # :5000
npm run dev                                            # :3000
dotnet test                                            # backend
npm run lint                                           # frontend
```

## Rules

1. Spec antes de código. `docs/specs/<feat>/spec.md` siempre.
2. TDD: red → green → refactor. xUnit + FluentAssertions.
3. ADR para decisiones costosas de revertir. `docs/adr/`.
4. Términos de `CONTEXT.md`. Si falta uno, agrégalo antes de usarlo.
5. Cada feature declara track: T1, T2, o both.
6. No premature abstraction. Extract en la tercera repetición. Límites de archivo: código ≤300 líneas (warn) / ≤500 max → split obligatorio. Docs `.md` ≤150 (warn) / ≤250 max → split obligatorio. Hook PostToolUse enforza automáticamente.
7. PHI nunca en logs. Track 2 → `RedactPhi()` siempre.
8. Next.js 16: convención `proxy.ts` (no `middleware.ts`). Lee `node_modules/next/dist/` antes de tocar APIs Next.

## Stack

.NET 10 Minimal API · EF Core 10 · SQLite (+ SQLCipher T2) · Next.js 16 · React 19 · Tailwind v4 · shadcn/ui · Drizzle (SSR only)

## Model tier (Claude Code)

- Plan, architecture, ADR review → Opus 4.7 (`/model opus`)
- Implementation, refactor → Sonnet 4.6 (default)
- Lint, format, test scaffolds, mechanical edits → Haiku 4.5 (`/model haiku`)

## Agents — cuándo activar

| Agente | Activar cuando |
|--------|---------------|
| `verify-app` | Antes de cualquier PR. Si tests o lint fallan tras implementación. |
| `code-simplifier` | Tras implementación antes de PR. Si diff > 150 líneas. |
| `planner` | Feature nueva, cambio cross-module, cualquier ADR. |
| `reviewer` | Diff listo para merge. |

Invocar: `use subagents` en el prompt → Claude lanza en paralelo.
Si implementación se estanca → volver a plan mode (Shift+Tab×2), no seguir empujando.
Si intento falla → `/rewind` (descarta intento, preserva context) antes de corregir.

## Pointers

- Domain + backlog: `docs/CONTEXT.md`
- Agents registry: `docs/AGENTS.md`
- ADRs: `docs/adr/`
- Specs: `docs/specs/<feature>/spec.md`
- Track 1 SOPs: `docs/track1-ghl/`
- Track 2 compliance: `docs/track2-hipaa/`
- End-user setup: `docs/guides/setup.md`
