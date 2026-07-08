# Runtime-0011 — Spell, Buff/Debuff, and Status Effect Runtime

## Purpose
Runtime-0011 adds the first executable spell runtime layer to MajorMUD Reforged.

This milestone does **not** claim verified Classic MajorMUD spell formulas. It creates a clean runtime surface that can execute deterministic Reforged spell behavior while preserving explicit provenance for formulas that still need evidence.

## Included runtime systems

- Spell casting request/result model
- Mana and CP cost hooks
- Spell success trace using formula provenance
- Damage spell pipeline
- Healing spell pipeline
- Buff/debuff/poison status application
- Status ticking
- Status expiration and purge
- Structured spell event output
- Validation coverage for cast, heal, status lifecycle, and /Data spell loading

## Formula status

Current executable spell formulas are tagged as:

```text
reforged_formula
```

Classic behaviors still marked UNKNOWN:

- exact spell success formulas
- exact mana/CP cost behavior
- exact resistance/save tables
- exact duration timing
- buff/debuff stacking rules
- poison/disease/status tick cadence

## Next milestone
Runtime-0012 should integrate command processing and a continuous offline game loop so movement, combat, regen, spells, loot, and NPC services can execute in a single runtime tick cycle.
