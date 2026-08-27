# Code Update 0278 - Combat Parity Validation

Validation performed against cumulative Pass 7 baseline.

## Passed

- `Tools/Validation/validate_combat_runtime.py`
- `Tools/Validation/validate_combat_runtime_part2.py`
- `Tools/Validation/validate_gameplay_group4.py`
- changed/added C# lexical structure scan
- changed/added JSON parse validation
- 0278 parity-ledger duplicate-ID check
- 0278 new assertion evidence-path check
- frozen score reproduction: 55 PASS / 25 STRONG / 48 UNKNOWN / 128 total
- independent scheduler separation check: 5s physical-combat scheduler and 3s spell/status scheduler
- static combat timing/formula vectors for 5s physical round, 3s spell round, 8..99 hit bounds, inclusive hit/Dodge boundaries, 2x..4x critical range, five-swing cap, STR-requirement attack-energy penalty, encumbrance attack-energy adjustment, and optional Speed branch

## Baseline-identical known validator failures

Gameplay Groups 1-3 fail the same way in Pass 7 and Code Update 0278. Groups 1-2 require generated `Data/Classic` files not carried in this source package. Group 3 additionally retains hard-coded `E:\\MajorMud-Reforged Github` validation paths. These failures are not introduced by Code Update 0278.

`Sim/validators/validate_combat.py` cannot run in this package because its legacy Python `Runtime` module is not present on the packaged import path; the authoritative server combat validators above pass.

## Build-tool limitation

No `dotnet`, `csc`, or `mcs` executable is available in the validation environment. A real .NET compile therefore remains pending; source-structure and runtime-contract validators were used instead.
