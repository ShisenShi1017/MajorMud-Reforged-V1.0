# CodeUpdate 0101 — Combat Reconstruction Branch

Status: **PASS_WITH_LIMITATIONS**  
Canonical rule: **Librarium remains source of truth.** Runtime formulas were not replaced.

## Scope completed

This branch performed the complete Combat subsystem pass in one package: inventory, relationship mapping, evidence reconciliation, formula/behavior review, probe creation/execution, runtime traceability review, promotion review, documentation, validation, and handoff.

## Evidence inventory

- Raw combat-related evidence hits: **44050**
- Prioritized evidence hits written to index: **1200**
- Source files with combat hits: **630**
- Relationship edges generated: **2771**

### Hits by source
- mmud_explorer: 314
- repo: 90
- repo_strings: 796

### Hits by category
- armor_defense: 148
- attack_hit: 358
- combat: 421
- critical_backstab: 315
- damage: 697
- death_xp: 363
- status_effects: 65
- weapon_skill: 404

## Runtime review

The existing runtime combat engine is deterministic and explicitly labels formula output as `not_classic_claimed` / `reforged_formula`. That is correct and should remain unchanged until Classic equations are proven.

Current runtime formulas reviewed:

- `reforged_hit_chance_v1`
- `reforged_damage_v1`
- `resolve_attack`
- `run_combat_round`
- `run_duel`

These provide useful simulator scaffolding and trace output, but they are not promoted to Classic truth.

## Probe execution

Limited runtime probes were executed where possible. They validate deterministic Reforged trace behavior, not Classic correctness.

```json
[
  {
    "probe_id": "COMBAT-0101-RUNTIME-ATTACK-TRACE",
    "status": "executed",
    "classic_accuracy": "not_classic_claimed",
    "hit": true,
    "attack_roll": 18,
    "hit_chance": 95.0,
    "damage": 9,
    "trace_count": 2
  },
  {
    "probe_id": "COMBAT-0101-RUNTIME-DUEL-DETERMINISM",
    "status": "executed",
    "classic_accuracy": "not_classic_claimed",
    "rounds": 2,
    "event_count": 6,
    "mode": "reforged_formula"
  }
]
```

## Promotion decision

- Formulas promoted to Verified: **0**
- Runtime formulas replaced: **0**
- Reason: Combat math still lacks exact Classic evidence for hit chance, damage ordering, armor order, backstab/critical handling, rounding, minimum damage, and timing.

## Remaining combat gaps
- **COMBAT-GAP-0101-HIT-FORMULA** (critical): Classic hit chance equation, random roll range, clamps, level/stat/skill weights, miss/fumble rules
- **COMBAT-GAP-0101-DAMAGE-FORMULA** (critical): Classic weapon roll, stat scaling, skill/class bonuses, resistance ordering, rounding/floor
- **COMBAT-GAP-0101-ARMOR-ORDER** (high): Whether armor modifies hit chance, damage, both, and exact order relative to buffs/resists
- **COMBAT-GAP-0101-ROUND-TIMING** (high): Combat cadence/initiative tie between host scheduling and MajorMUD combat routine
- **COMBAT-GAP-0101-BACKSTAB-CRIT** (high): Exact prereqs, multiplier/additive model, failure/miss behavior, rounding
- **COMBAT-GAP-0101-STATUS-ORDER** (medium): Ordering of poison/stun/blind/paralyze with combat action resolution
- **COMBAT-GAP-0101-DEATH-XP-ORDER** (medium): Exact death event to XP/loot/quest trigger order

## Next combat-specific work

The next Combat branch should be an execution-oriented proof pass: locate the highest-confidence Classic combat routine evidence from imported WCC/WBB artifacts and run the generated probes against any executable or reproducible runtime trace available. Until that happens, combat remains reconstructed and test-ready, but not Classic-verified.
