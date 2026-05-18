# Agents Registry — Neuracode CRM

## Workflow obligatorio (todo feature)

```
1. SPDD spec  →  docs/specs/<feat>/spdd.md  (planner / Opus)
2. Test rojo   →  xUnit failing, no código aún
3. Green       →  código mínimo para pasar test
4. Refactor    →  clean code + design patterns
5. Pre-PR      →  reviewer + verify-app
```

## team-1 (sprint pipeline completo)

Definición en `.claude/teams/team-1.md`. Orquesta planner → coder → reviewer → verify-app en secuencia.

Invocar: `Spawn team-1 para este feature: <descripción>`

## Agents

| Agent | Role | Trigger | Model |
|-------|------|---------|-------|
| `planner` | SPDD spec. No code. | Feature nueva, cambio cross-module, ADR | Opus 4.7 |
| `coder` | TDD desde spec aprobado | Tras spec approved | Sonnet 4.6 |
| `reviewer` | Diff: tests, PHI, naming, contracts, design | Antes de commit | Sonnet 4.6 |
| `security-reviewer` | Auth/PII/injection | PHI exposure, auth seam, endpoint público | Sonnet 4.6 |
| `verify-app` | dotnet test + npm lint | Pre-PR | Haiku 4.5 |
| `code-simplifier` | Dup/complexity/dead code | Tras impl, antes de PR. Diff > 150 líneas | Haiku 4.5 |

## Model tier

| Phase | Model |
|-------|-------|
| Plan, ADR, architecture | Opus 4.7 (`/model opus`) |
| Implementation, refactor | Sonnet 4.6 (default) |
| Lint, format, mechanical edits | Haiku 4.5 (`/model haiku`) |

## Agent files

Archivos en `.claude/agents/`. Cargados por Claude Code automáticamente.

- `.claude/agents/planner.md`
- `.claude/agents/coder.md`
- `.claude/agents/reviewer.md`
- `.claude/agents/verify-app.md`
- `.claude/agents/code-simplifier.md`
- `.claude/teams/team-1.md`
