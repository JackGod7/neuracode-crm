# [Feature Name]

**Status**: Draft | Approved | Implemented | Superseded
**Date**: YYYY-MM-DD | **Owner**: [nombre] | **Track**: T1 | T2 | both

---

## Requirements — qué debe hacer el sistema

1. Invariante testeable 1
2. Invariante testeable 2
3. ...

**Out of scope:** qué NO entra en este spec (explícito).

---

## Entities — objetos de dominio

| Entidad | Campos clave | Notas |
|---------|-------------|-------|
| `Entity` | field1, field2 | ... |

---

## Approach — cómo funciona

```
Actor
  │
  ▼
Step 1 — breve descripción
  ├─ 1a. sub-paso
  └─ 1b. sub-paso
  │
  ▼
Step 2
```

---

## Structure — árbol de archivos

```
api/src/Neuracode.Crm.Api/
├── Endpoints/FeatureEndpoints.cs
├── Services/IFeatureService.cs
└── Data/Entities/FeatureEntity.cs

api/tests/Neuracode.Crm.Tests/
├── ContractTests/FeatureContractTests.cs
└── UnitTests/FeatureUnitTests.cs
```

---

## Operations — operaciones clave

### O1 — Nombre operación (happy path)
```
PRECONDITION : condición inicial
INPUT        : descripción del input
OUTPUT       : descripción del output
POSTCONDITION: estado final garantizado
```

---

## Norms — restricciones

| # | Norma | Justificación |
|---|-------|--------------|
| N1 | ... | ... |

---

## Safeguards — qué nunca debe ocurrir

| # | Safeguard | Mecanismo |
|---|-----------|-----------|
| S1 | Nunca ... | ... |

---

## Patrones relacionados

- `docs/adr/XXXX-nombre.md`
- `docs/specs/feature-relacionada/spdd.md`
