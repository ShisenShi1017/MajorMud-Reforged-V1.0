# Runtime-0017 — Gameplay Completion & Classic Validation Readiness

Runtime-0017 does not claim that every Classic MajorMUD mechanic is fully known. It confirms that the runtime architecture can execute a six-stage gameplay pass without failures while preserving unknowns for evidence-backed validation.

## Six gameplay stages
1. Character lifecycle
2. Exploration and movement
3. NPC interaction
4. Combat loop
5. Progression
6. Long-term play and save/load

## Validation result
See `Reports/gameplay_completion_report_0017.json`.

## Known unknowns
- Exact Classic combat formulas
- Exact spell/resist formulas
- Exact EXP/leveling behavior
- Exact class/race passives
- Ability-flag-backed quest mapping
- Special exits and special room behavior

## Quest validation
Quest validation continues next. `Data/quests.json` is intentionally not populated with invented quest data.
