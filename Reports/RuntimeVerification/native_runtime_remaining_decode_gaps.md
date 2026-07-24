# Native Runtime Remaining Decode Gaps

Date: 2026-07-17

## Current Local Baseline

- Classic compiler output contains `26,666/26,666` room descriptions.
- Classic runtime audit loads `26,666/26,666` room descriptions.
- Runtime audit loads `1,013` live NPC instances and `626` live monster instances with hard map-qualified placement validation.
- Interaction/narrative smoke passes with `2,183` quests, `83` executable quest entries, `182` decoded textblock stage scripts, and exhaustive NPC quest-entry dispatch validation.
- Full current-health validator passes `13/13`.

## Decode Work Still Needed

### Room and World Placement

- Decode/materialize static room ground objects. Compiler output has `Objects: 288`, but runtime audit still reports `Ground objects: 0`.
- Resolve five unresolved generated room references from exits/room linkage so traversal has no missing destination targets.
- Keep the two skipped map-qualified spawn records classified as invalid/non-spawn placements:
  - `spawn.288.43` -> `room.map1.288`; user verified that no such room exists.
  - `spawn.2306.213` -> `room.map1.2306`; user verified this room is a shop with no NPC.
- Determine whether `Newhaven Arena` is an incorrect audit probe label, a room alias problem, or missing generated room data.

### Native Actions and Messages

- Prove exact native `remoteaction` mutations. Current runtime records durable event metadata and fails closed on unknown mutation behavior.
- Finish exact success/failure message binding where generated events still have IDs but not all original runtime output order/wording is proven.
- Classic completed quests are non-repeatable on the same character. Replay requires starting a new character; do not add same-character quest reset/restart behavior.

### Combat, Spells, and Status

- Continue replacing identity-only spell/effect records with handler-backed behavior only when binary/runtime evidence proves target type, timing, resistance, duration, damage/heal formula, and message behavior.
- Confirm exact native random roll, save/resist, fizzle/interrupt, duration expiration, and room-effect scheduling behavior for remaining spells/status effects.
- Keep unproven spell/effect records fail-closed or explicitly classified unsupported instead of guessing.

### Economy and Services

- Prove exact native restock timing, merchant repair behavior, player-stocked shop behavior, sell-only junkyard behavior, and service failure messages.
- Verify bank/trainer/guild service costs and all failure paths against native evidence or strongly-correlated MMExplorer data before widening runtime execution.

### Persistence and Host Runtime

- Native binary save/journal/recovery bytes remain unproven. The Reforged save format is functional but not claimed byte-compatible with Worldgroup/Btrieve behavior.
- Exact Worldgroup host callback ordering, module command dispatch, recovery transactions, and live Btrieve/MKDE durability behavior require live trace or stronger binary proof.

## Rule For Future Work

Promote behavior into executable Classic runtime only when it is verified/native or strongly correlated enough for the user-approved Classic baseline. Keep exact unknown native behavior evidence-gated and visible in reports.

Classic quest completion is final for that character. Same-character quest replay belongs only in a future Custom/AI rule set if explicitly approved, not in Classic.
