# Validation-0005 — Formula Extraction and Verification

Result: **PASS_WITH_FINDINGS**

## Computed metrics

- Files scanned: **454**
- Formula source files: **296**
- Formula assertions extracted: **238039**
- Formula systems evaluated: **10**
- Exact classic equations verified: **0**
- Potential formula conflict candidates: **575**
- Canonical `/Data` overwritten: **No**

## Formula systems evaluated

- **Combat** — Derived / confidence 85 / assertions 30091
- **Regeneration** — Derived / confidence 85 / assertions 23516
- **Experience Leveling** — Derived / confidence 85 / assertions 17493
- **Spell** — Derived / confidence 85 / assertions 27473
- **Loot** — Derived / confidence 85 / assertions 27265
- **Economy** — Derived / confidence 85 / assertions 27034
- **Spawn** — Derived / confidence 85 / assertions 15502
- **Movement** — Derived / confidence 85 / assertions 25672
- **Ai** — Derived / confidence 85 / assertions 29773
- **Status** — Derived / confidence 85 / assertions 14220

## Codex architecture decisions included

This pass includes `Librarium/DecisionRecords/ADR_CANONICAL_LIBRARIUM_FOR_CODEX.json`. Codex should treat the Librarium as the canonical source of truth. `/Data` should become generated runtime output once the Librarium compiler/export layer is implemented.

## Important finding

This pass extracted formula source evidence and dependency assertions. It did **not** promote exact classic equations. Exact formulas still require reconciliation against implementation sources such as MMExplorer, MBBSEmu, reference artifacts, and runtime traces.
