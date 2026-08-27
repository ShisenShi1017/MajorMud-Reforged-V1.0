# Pass 26 Validation — Update 0297

Validation target: Door Requirements / Trap State / Container Scope.

## Current checks

- Dedicated Pass 26 static validation: **70/70 PASS**.
- Boolean `strength_alternative` decoder correction: **PASS**.
- Numeric Picklocks/Strength requirement pre-gate: **PASS**.
- Normal movement remains independent of action requirements: **PASS**.
- Exact PICKLOCK/BASH success RNG/state transition remains gated: **PASS**.
- Trap state remains UNKNOWN when per-exit Para2 is unavailable: **PASS**.
- Generic `native.world-containers` parity shell removed from ledger/registry: **PASS**.
- Changed C# lexical delimiter/structure checks: **PASS**.
- Changed/current JSON parse checks: **PASS**.
- Combat XP-sharing correction: **16/16 PASS**.
- Combat runtime: **PASS**.
- Combat runtime Part 2: **PASS**.
- Native economy markup: **PASS**.
- Gameplay Group 4: **PASS**.
- Current parity ledger: **73 PASS / 76 STRONG / 28 UNKNOWN = 177**.

## Historical validator notes

The Pass 24 validator passes 52/53; its only failure is the intentionally historical hard-coded current-ledger pointer. The Pass 25 validator passes 55/59; its failures are historical expectations for the prior action-gate text/picklock expression and its 0296 ledger/scope pointers. These are superseded by Pass 26 and are not runtime regressions.

A .NET compiler is not installed in this execution environment, so build validation remains pending until the package is installed in a .NET-capable environment.
