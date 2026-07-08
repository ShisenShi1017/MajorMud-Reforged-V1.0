# Runtime-0013B — Ability Engine

## Purpose

Runtime-0013B promotes ability flags into a first-class runtime subsystem. The engine supports ability catalog loading, grant/revoke/query operations, temporary expiration, stack counting, requirement checks, and provenance traces.

## Important Accuracy Boundary

This milestone does **not** claim hidden classic ability formulas are fully known. It implements deterministic state handling for ability flags so classes, races, quests, items, spells, scripts, doors, NPC services, and world triggers can all check the same runtime state.

## Added Files

- `Runtime/abilities/ability_engine.py`
- `Runtime/abilities/__init__.py`
- `Sim/validators/validate_ability_quest_engine.py`

## Supported Ability Sources

- permanent
- temporary
- quest
- item
- class
- race
- script
- validator/test

## Persistence Implication

Future save/load must persist active ability grants and their provenance. Temporary grants should persist their expiration tick or be intentionally recalculated by the runtime system that owns them.
