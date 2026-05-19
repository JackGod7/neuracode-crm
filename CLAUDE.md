# Neuracode CRM

Producto comercial Neuracode. Local-first, WhatsApp-native, dual-track (T1 reseller / T2 HIPAA).

## Run

```
dotnet run --project api/src/Neuracode.Crm.Api    # :5000
npm run dev                                        # :3000
dotnet test                                        # 139 tests
npm run lint                                       # frontend
```

## Rules

1. SPDD antes de código — `docs/specs/<feat>/spdd.md` (REASONS canvas).
2. TDD: rojo → green → refactor. Pre-commit gate corre `dotnet test` completo.
3. ADR para decisiones costosas de revertir — `docs/adr/`.
4. Cada feature declara track: T1, T2, o both.
5. PHI nunca en logs. Track 2 → `RedactPhi()` siempre.
6. Next.js 16: usar `proxy.ts` no `middleware.ts`.

## Pointers

- Domain + backlog: `docs/CONTEXT.md`
- Docs governance + naming: `docs/DOCS.md`
- Agents + team-1: `docs/AGENTS.md`
- Specs: `docs/specs/<feat>/spdd.md` (ver convención en `docs/DOCS.md`)
- ADRs: `docs/adr/`
- Track 1 SOPs: `docs/track1-ghl/`
- Track 2 compliance: `docs/track2-hipaa/`
