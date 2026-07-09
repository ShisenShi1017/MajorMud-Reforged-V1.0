# CodeUpdate 0103 — Progression Reconstruction Branch

Status: **PASS_WITH_LIMITATIONS**  
Canonical rule: **Librarium remains source of truth.** Runtime progression formulas, tables, and behavior were not replaced.

## Scope completed

This branch performed the complete Progression subsystem pass in one package: inventory, relationship mapping, evidence reconciliation, formula/behavior review, probe creation, runtime traceability review, promotion review, documentation, validation, and handoff.

Progression scope includes XP, levels, attributes, race/class advancement, skills/abilities, HP/mana/movement growth, death penalties, and advancement rewards.

## Evidence inventory

- Raw progression-related evidence hits: **100981**
- Prioritized evidence hits written to index: **1500**
- Source files with progression hits: **2428**
- Relationship edges generated: **7902**

### Hits by source
- mmud_explorer: 7
- repo: 7
- repo_strings: 1486

### Hits by category
- advancement_rewards: 291
- attributes_stats: 1392
- classes_races: 422
- death_penalty: 1261
- hp_mp_mv: 1406
- progression_tables: 1403
- skills_abilities: 280
- xp_leveling: 1447

## Runtime review

Progression was reconstructed as a subsystem branch, but no exact Classic progression routine was promoted. The current evidence supports broad categories—XP/leveling, attribute/stat model, race/class progression, skills/abilities, resource growth, tables/caps, death penalties, and rewards—but not exact equations, threshold tables, dice/rounding behavior, ordering, or edge cases.

Runtime/module discovery found **6** progression-named source candidates. These are indexed for follow-up, but they do not by themselves prove Classic progression behavior.

## Probe status

Eight progression probes were defined. They are intended to become deterministic simulator/regression tests once generated runtime exposes authoritative progression hooks.

```json
[
  {
    "probe_id": "PROG-0103-RUNTIME-MODULE-DISCOVERY",
    "status": "executed",
    "progression_runtime_file_candidates": [
      "Core/Logging/LogLevel.cs",
      "Apps/Source/Reforged.Server/ClassicParityAudit.cs",
      "Apps/Source/Reforged.Server/ClassicParityAuditModels.cs",
      "Runtime/leveling.py",
      "Sim/validators/validate_exp_leveling.py",
      "Sim/validators/validate_exp_leveling.py"
    ],
    "candidate_count": 6,
    "classic_accuracy": "not_classic_claimed"
  },
  {
    "probe_id": "PROG-0103-PROBE-HARNESS-STATUS",
    "status": "scaffold_created",
    "executed_runtime_behavior": "no authoritative progression runtime execution was promoted",
    "classic_accuracy": "unverified"
  }
]
```

## Promotion decision

- Progression mechanics promoted to Verified: **0**
- Runtime formulas/behaviors replaced: **0**
- Reason: Progression still lacks exact Classic evidence for XP thresholds/awards, level-up ordering, attribute/resource growth, race/class caps, skill/ability training gates, death penalties, and reward order.

## Remaining progression gaps
- **PROG-GAP-0103-XP-THRESHOLDS** (critical): Exact Classic XP threshold table/formula, per-class/per-race differences, boundary behavior, caps, overflow handling
- **PROG-GAP-0103-XP-AWARDS-SHARING** (critical): Kill XP award formula, party sharing, level range modifiers, contribution rules, penalties/bonuses
- **PROG-GAP-0103-STAT-GROWTH** (critical): Attribute gain formulas/tables, random/dice use, racial/class caps, ordering and rounding
- **PROG-GAP-0103-HP-MANA-MOVE-GROWTH** (high): HP/mana/movement growth per level/class/race/stat, dice/table constants, caps and rounding
- **PROG-GAP-0103-RACE-CLASS-CAPS** (high): Exact race/class base modifiers, caps, allowed combinations, advancement restrictions, special cases
- **PROG-GAP-0103-SKILL-ABILITY-TRAINING** (high): Skill/ability prerequisites, level gates, trainers, costs, caps, quest/flag interactions, training order
- **PROG-GAP-0103-DEATH-PENALTY** (medium): Death XP loss, level loss, recovery/resurrection behavior, floor/cap edge cases
- **PROG-GAP-0103-ADVANCEMENT-REWARD-ORDER** (medium): Ordering of stat/resource gains, ability/spell grants, titles, messages, quest reward progression and output

## Next recommended branch

Continue to **NPC Reconstruction** if following subsystem order. If staying on Progression, the next pass should target original/imported advancement tables and locate any WCC/WBB routines or database fields that define XP thresholds, class/race caps, and stat/resource growth.
