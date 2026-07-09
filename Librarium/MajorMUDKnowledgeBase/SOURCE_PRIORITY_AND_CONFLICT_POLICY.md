# Source Priority and Conflict Policy

Generated: 2026-07-09T18:07:36Z

## Priority order

1. Original MajorMUD runtime observations/logs, when available.
2. Nightmare Redux, treated as the leading maintained reverse-engineered implementation.
3. MBBSEmu, treated as independent emulator evidence.
4. MMUD Explorer, treated as primary data-structure and field-interpretation evidence.
5. PHP Monster Simulator, treated as historical behavior evidence and regression comparison.

## Conflict handling

When sources disagree, the project does not erase the older interpretation. It records the conflict, applies source priority to choose a working hypothesis, and blocks `classic_verified` status until corroborated by original observations or independent agreement.

## Current unresolved conflicts

- `CONFLICT_COMBAT_DR_001` — Physical Damage Reduction: open_leading_nightmare_redux
  - Prefer Nightmare Redux 2025 over historical Monster Sim when unsupported by original observations; seek MBBSEmu/runtime corroboration before classic-verified.
- `CONFLICT_COMBAT_DODGE_ORDER_001` — Dodge/Evasion order: open_needs_order_confirmation
  - Nightmare Redux is leading interpretation; confirm sequence against emulator/log behavior before runtime lock.
- `CONFLICT_BACKSTAB_SPECIAL_DEFENSE_001` — Backstab/special attack defense order: open_needs_specific_scenario_tests
  - Treat as unresolved until cross-source scenario tests exist.
