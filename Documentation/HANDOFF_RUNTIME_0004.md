# Handoff — Runtime-0004 EXP + Leveling

## Starting point
Runtime-0004 is cumulative from Runtime-0003.

## Added in this milestone
- EXP/leveling runtime module
- Level calculation from current EXP
- CP award calculation from `/Data/leveling.json`
- Class/race EXP modifier lookup from canonical `/Data`
- Validator coverage for EXP curve, modifier resolution, snapshot application, and classic-unknown preservation

## Validation result
- Data load: PASS
- Reference integrity: PASS
- Formula engine: PASS
- Schema adapters: PASS
- Character snapshot/equipment: PASS
- EXP/leveling: PASS
- Remaining runtime modules: UNKNOWN by design

## Next recommended milestone
Runtime-0005 — HP/Mana/CP + Regeneration Calculator Foundation.

## Important rule
Do not present the Reforged EXP curve as classic MajorMUD behavior. It is a working runtime formula until classic evidence replaces or confirms it.
