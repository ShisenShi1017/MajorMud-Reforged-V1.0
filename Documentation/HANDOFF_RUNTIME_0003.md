# MajorMUD Reforged Runtime-0003 Handoff

## Milestone
Runtime-0003 — Character Snapshot + Equipment Stat Calculator

## What changed
- Added `Runtime/character_snapshot.py`.
- Added `Runtime/equipment_stats.py`.
- Added `Sim/validators/validate_snapshot_equipment.py`.
- Updated the verification registry to run snapshot/equipment validation.
- Kept formulas evidence-gated: no hidden classic item effects or ability semantics are promoted.

## Validation result
- Data load: PASS
- Reference integrity: PASS
- Formula engine: PASS
- Schema adapters: PASS
- Character snapshot + equipment explicit-field aggregation: PASS
- Runtime placeholders: UNKNOWN by design

## Important limits
The equipment calculator only aggregates explicit canonical fields already present in `/Data/items.json`:
- minHit
- maxHit
- accuracy
- speed
- armorClassBonus
- damageResistanceBonus
- weight
- cost

It intentionally does not infer:
- ability semantics
- equipment slot legality
- class/race restrictions
- classic AC/DR formulas beyond explicit fields
- derived character stats

## Next recommended milestone
Runtime-0004 — EXP + Leveling Calculator Foundation.
