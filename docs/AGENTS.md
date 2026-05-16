# Agents Registry — Neuracode CRM

## Workflow

```
/model opus  → planner  → spec en docs/specs/<feat>/spec.md
/model haiku  (default) → coder   → TDD red→green→refactor
/model haiku → reviewer → diff check, issues only
```

## Registry

| Agent | Role | Trigger | Model |
|-------|------|---------|-------|
| `planner` | Produce spec. No code. | Feature nueva, antes de tocar código | Opus 4.7 |
| `coder` | TDD desde spec aprobado | Después de spec approved | Sonnet 4.6 |
| `reviewer` | Diff review: tests, PHI, naming, contracts | Antes de commit | Haiku 4.5 |
| `security-reviewer` | Auth/PII/injection check | PHI exposure, auth seam, endpoint público | Sonnet 4.6 |
| `database-reviewer` | Schema Drizzle + EF Core | Cualquier cambio a `schema.ts` o migración | Haiku 4.5 |
| `prod-saas-team` | Orchestrator: PRD→TDD→review→security pipeline | Features complejas end-to-end | mixed |

## Agent Prompts

Archivos en `.claude/agents/`. Cargados por Claude Code automáticamente.

- `.claude/agents/planner.md`
- `.claude/agents/coder.md`
- `.claude/agents/reviewer.md`
- `.claude/agents/prod-saas-team.md` (ya existente)
