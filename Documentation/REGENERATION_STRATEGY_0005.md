# Runtime-0005 — HP/Mana/CP + Regeneration Calculator Foundation

## Purpose

Runtime-0005 adds the first vital-pool and regeneration calculation layer to the runtime project.

This milestone is intentionally a foundation, not a claim of exact classic MajorMUD parity.

## Added

- `Runtime/regeneration.py`
  - `calculate_vital_pools(...)`
  - `calculate_regeneration_tick(...)`
  - `apply_vital_pools_to_snapshot(...)`
  - `apply_regeneration_tick_to_snapshot(...)`
- `Sim/validators/validate_regeneration.py`
- Verification registry integration
- Updated suite milestone marker to Runtime-0005

## Formula Mode

All new formulas are tagged:

```text
mode = reforged_formula
classic_accuracy = not_classic_claimed
```

The formulas are deterministic and traceable so they can be replaced or calibrated later when stronger classic evidence is available.

## Reforged v1 Vital Pool Design

HP uses:

- character level
- HEA-derived bonus
- class defaults or class fields when present
- race bonuses when present
- equipment totals when supplied

Mana uses:

- character level
- INT/WIL-derived bonuses
- class defaults or class fields when present
- race bonuses when present
- equipment totals when supplied

CP uses:

- explicit leveling CP when available
- level fallback when explicit CP is absent
- equipment totals when supplied

## Reforged v1 Regeneration Design

A regeneration tick uses:

- level
- HEA for HP regeneration
- WIL for mana regeneration
- resting multiplier
- in-combat penalty
- poison/bleed HP reduction placeholder
- equipment regen bonuses when supplied

## Evidence Rule

No exact classic HP, mana, CP, or regeneration formula has been promoted by this milestone.

Classic formula status remains:

```text
UNKNOWN
```

Future passes should compare against MMExplorer calculator outputs, MBBSEmu behavior, or runtime logs before any classic formula is marked verified.
