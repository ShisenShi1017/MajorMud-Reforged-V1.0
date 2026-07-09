# Phase 2 — Knowledge Reconstruction Completion Report

Generated: 2026-07-09T18:07:36Z

## Result

**PASS_KNOWLEDGE_RECONSTRUCTION_FROM_EXHAUSTED_CANONICAL_EVIDENCE**

This pass reconstructed mechanic-level Librarium records from the exhausted Phase 1 evidence corpus. Runtime/gameplay code was not modified.

## Counts

- Canonical evidence sources: 4
- Phase 1 files classified: 941 / 941
- Evidence hits organized: 31281
- Mechanic records created: 14
- Bucket pages created: 19
- High-confidence verified mechanic records: 4
- Probable/reconstruction-needed records: 6
- Supporting foundation records: 1
- Open conflict records: 3
- Runtime modified: No

## High-confidence promoted records

- `COMBAT_HIT_AC` — Combat Hit vs AC
- `MAGIC_RESIST` — Magic Resistance
- `MAGIC_SPELL_ENERGY` — NPC Spell Energy
- `NPC_ATTACK_LOOP` — NPC Attack Energy Loop

## Open conflicts

- `CONFLICT_COMBAT_DR_001` — Physical Damage Reduction: open_leading_nightmare_redux
  - Policy: Prefer Nightmare Redux 2025 over historical Monster Sim when unsupported by original observations; seek MBBSEmu/runtime corroboration before classic-verified.
- `CONFLICT_COMBAT_DODGE_ORDER_001` — Dodge/Evasion order: open_needs_order_confirmation
  - Policy: Nightmare Redux is leading interpretation; confirm sequence against emulator/log behavior before runtime lock.
- `CONFLICT_BACKSTAB_SPECIAL_DEFENSE_001` — Backstab/special attack defense order: open_needs_specific_scenario_tests
  - Policy: Treat as unresolved until cross-source scenario tests exist.

## What changed

- The evidence corpus is now organized by mechanic, not just by source file.
- Each mechanic has a Markdown Librarium article and a JSON machine-readable record.
- Each record includes source counts, top evidence files, representative evidence excerpts, status, confidence, runtime status, and conflict links.
- The source-priority policy is now encoded in the Librarium.

## What this does not claim

This pass does not claim every mechanic is classic-verified. It claims the exhausted evidence corpus has been reconstructed into a knowledge base that can drive formula tests, runtime mapping, and simulator synchronization.

## Next milestone

Phase 3 should convert high-confidence Librarium records into deterministic tests and runtime/simulator mapping IDs. Conflicted records remain blocked from classic-verified implementation until resolved.
