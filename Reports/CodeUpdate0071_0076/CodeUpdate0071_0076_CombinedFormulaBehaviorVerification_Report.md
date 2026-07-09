# CodeUpdate-0071 through 0076 Combined Formula Behavior Verification Report

Generated: 2026-07-08T20:19:53.517175+00:00
## Scope

This update completes the planned 0071-0076 behavior sweep in one cumulative package: combat, experience/leveling, regeneration/time, loot/spawn, NPC AI, and quest runtime behavior. The update preserves the Librarium as canonical and treats MMUD Explorer and MBBSEmu as evidence only.
## Summary
- **codeupdate**: 0071-0076
- **title**: Combined Formula Behavior Verification Sweep
- **generated_utc**: 2026-07-08T20:19:53.517175+00:00
- **baseline**: CodeUpdate-0070 FormulaBehaviorVerification cumulative
- **librarium_canonical_preserved**: True
- **total_behavior_records**: 28
- **total_gap_records_created**: 28
- **verified_promotions**: 0
- **runtime_formula_replacements**: 0
- **status**: PASS_WITH_LIMITATIONS
- **reason_no_promotions**: The sweep established behavior shapes and testable probes, but no source provided sufficient Classic runtime proof for exact equations, ordering, RNG bounds, rounding, tick timing, or edge cases.

## Subsystem Results
### CodeUpdate-0071 — Combat Behavior Verification
- Candidate references considered: **250**
- Behavior records created: **5**
- Deterministic probe families: **1**
- Verified promotions: **0**
- Runtime replacements: **0**
- Behavior records:
  - `hit_resolution_order`
  - `damage_range_and_rounding`
  - `armor_or_resist_mitigation`
  - `backstab_or_special_attack_modifiers`
  - `weapon_speed_tick_interaction`

### CodeUpdate-0072 — Experience and Leveling Behavior Verification
- Candidate references considered: **250**
- Behavior records created: **4**
- Deterministic probe families: **1**
- Verified promotions: **0**
- Runtime replacements: **0**
- Behavior records:
  - `experience_award_source`
  - `party_xp_distribution`
  - `level_threshold_check`
  - `death_or_penalty_application`

### CodeUpdate-0073 — Regeneration and Time Behavior Verification
- Candidate references considered: **250**
- Behavior records created: **4**
- Deterministic probe families: **1**
- Verified promotions: **0**
- Runtime replacements: **0**
- Behavior records:
  - `periodic_tick_cadence`
  - `hp_mana_move_regeneration`
  - `duration_expiration`
  - `ongoing_effect_tick_order`

### CodeUpdate-0074 — Loot and Spawn Behavior Verification
- Candidate references considered: **250**
- Behavior records created: **5**
- Deterministic probe families: **1**
- Verified promotions: **0**
- Runtime replacements: **0**
- Behavior records:
  - `spawn_probability_model`
  - `respawn_attempt_window`
  - `loot_table_selection`
  - `rare_drop_probability`
  - `gold_or_item_reward_generation`

### CodeUpdate-0075 — NPC AI Behavior Verification
- Candidate references considered: **250**
- Behavior records created: **5**
- Deterministic probe families: **1**
- Verified promotions: **0**
- Runtime replacements: **0**
- Behavior records:
  - `target_selection_priority`
  - `offensive_action_selection`
  - `flee_or_retreat_threshold`
  - `pathing_and_pursuit`
  - `special_ability_weighting`

### CodeUpdate-0076 — Quest Runtime Behavior Verification
- Candidate references considered: **116**
- Behavior records created: **5**
- Deterministic probe families: **1**
- Verified promotions: **0**
- Runtime replacements: **0**
- Behavior records:
  - `quest_stage_ordering`
  - `dialogue_condition_resolution`
  - `quest_flag_or_ability_grants`
  - `branch_failure_completion_states`
  - `scripted_runtime_effects`

## Why no formulas were promoted

No behavior record reached Verified because the available evidence establishes candidate behavior shapes but does not prove exact Classic equations or edge cases. Promotion still requires corroborating Classic/reference/runtime/simulator evidence, including ordering, rounding, RNG bounds, tick timing, and runtime consumers.
## What changed

The update adds behavior models, deterministic probe definitions, candidate-reference indexes, gap records, validation JSON, a query helper, and session handoff documentation. It does not change `/Data`, generated runtime formulas, or gameplay behavior.
