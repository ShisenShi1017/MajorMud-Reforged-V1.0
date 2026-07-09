# CodeUpdate 0102 — Magic Reconstruction Branch

Status: **PASS_WITH_LIMITATIONS**  
Canonical rule: **Librarium remains source of truth.** Runtime formulas and spell behavior were not replaced.

## Scope completed

This branch performed the complete Magic subsystem pass in one package: inventory, relationship mapping, evidence reconciliation, formula/behavior review, probe creation, runtime traceability review, promotion review, documentation, validation, and handoff.

## Evidence inventory

- Raw magic-related evidence hits: **63401**
- Prioritized evidence hits written to index: **1400**
- Source files with magic hits: **847**
- Relationship edges generated: **6149**

### Hits by source
- mmud_explorer: 60
- repo: 313
- repo_strings: 1027

### Hits by category
- buff_debuff: 441
- damage_healing: 992
- magic_core: 1400
- mana_cost: 1342
- resistance_saves: 289
- scroll_item_magic: 294
- spell_execution: 1054
- status_conditions: 337

## Runtime review

Magic was reconstructed as a subsystem branch, but no exact Classic spell execution routine was promoted. The current evidence supports broad categories—spell casting, mana, damage/healing, resistance/saves, duration effects, status magic, and item casting—but not exact equations, ordering, tick semantics, or edge cases.

Runtime/module discovery found **2** magic/spell-named source candidates. These are indexed for follow-up, but they do not by themselves prove Classic spell behavior.

## Probe status

Seven magic probes were defined. They are intended to become deterministic simulator/regression tests once the generated runtime exposes authoritative spell execution hooks.

```json
[
  {
    "probe_id": "MAGIC-0102-RUNTIME-MODULE-DISCOVERY",
    "status": "executed",
    "magic_runtime_file_candidates": [
      "Sim/validators/validate_spells_status.py",
      "Runtime/spells/spell_runtime.py"
    ],
    "candidate_count": 2,
    "classic_accuracy": "not_classic_claimed"
  },
  {
    "probe_id": "MAGIC-0102-PROBE-HARNESS-STATUS",
    "status": "scaffold_created",
    "executed_runtime_behavior": "no authoritative magic runtime execution was promoted",
    "classic_accuracy": "unverified"
  }
]
```

## Promotion decision

- Magic mechanics promoted to Verified: **0**
- Runtime formulas/behaviors replaced: **0**
- Reason: Magic still lacks exact Classic evidence for cast order, mana costs, saves/resists, duration/stacking, spell scaling, item charge semantics, and status-effect interactions.

## Remaining magic gaps
- **MAGIC-GAP-0102-SPELL-CAST-ORDER** (critical): Classic order for target validation, mana consumption, fizzle/interruption, resistance, effect application, and output emission
- **MAGIC-GAP-0102-MANA-COSTS** (critical): Exact mana costs or formulas per spell, class/race modifiers, failure cost rules, rounding
- **MAGIC-GAP-0102-RESIST-SAVES** (high): Resistance/save equation, random range, stat/level weights, immunity ordering, partial resist rules
- **MAGIC-GAP-0102-DAMAGE-HEALING** (high): Damage/healing formulas, caster scaling, target mitigation, caps/floors, rounding
- **MAGIC-GAP-0102-DURATION-TICKS** (high): Duration units, host tick decrement, tick clipping proof, refresh/expire output order
- **MAGIC-GAP-0102-STACKING** (high): Same-family buff/debuff stacking, replacement, strongest-wins, additive, and cancellation rules
- **MAGIC-GAP-0102-STATUS-MAGIC** (medium): Blind/stun/sleep/silence/poison/disease magical application/removal order and combat interactions
- **MAGIC-GAP-0102-ITEM-CASTING** (medium): Scroll/wand/staff/potion cast path, charge consumption timing, failure behavior, class restrictions

## Next recommended branch

Continue to **Progression Reconstruction** if following subsystem order. If staying on Magic, the next pass should target exact spell records/tables and locate any original WCC/WBB spell routines or imported database fields that define mana cost, duration, target type, and resistance behavior.
