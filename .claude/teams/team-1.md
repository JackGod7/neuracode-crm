# team-1 — Sprint Pipeline

Sprint pipeline end-to-end: SPDD spec → TDD implementation → review → verify.

## Agents

| Role | Agent | When |
|------|-------|------|
| Spec | `planner` | Feature nueva antes de código |
| Code | `coder` | Tras spec aprobado, TDD red→green |
| Review | `reviewer` | Diff listo para merge |
| Verify | `verify-app` | Pre-PR: dotnet test + npm lint |
| Simplify | `code-simplifier` | Tras implementación si diff > 150 líneas |

## Usage

```
Spawn team-1 para este feature: <descripción del feature>
```

Claude orquesta los agentes en secuencia: planner → coder → reviewer → verify-app.
