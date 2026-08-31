# Native Runtime Remaining Decode Gaps

- Sphinx spell `686` textblocks `2510-2515` are recovered exactly, generated as one compact four-branch runtime script, and executed atomically by combat: messages `1960-1963`, the selected two Map 12 teleports, and decoded casts `689`/`688` are live.
- NPC target type `1` is corrected to native `Self` and its exact six-row attack inventory cannot mutate the player through the generic hostile-effect path. Sphinx spell `686` has its dedicated generated-chain executor. Hooded man spell `1220` now persists Picklocks ability 37 at `+200` for 15 combat rounds/90 seconds in living-NPC state, refreshes, expires, and clears on death. No NPC door-picking behavior is promoted because decoded NPC 1000 evidence has no route or lock-action binding.
- Decoded NPC spell target type `11` is connected as proven `Full Area` behavior for NPC `361` / spell `446` (`plague`). Its exact `4-10` direct damage reaches every persistent character in the room through one cast. Surviving targets now also receive its decoded 30-combat-round Dodge `-10`, AC `-5`, and Max Damage `-4` modifiers through the shared persisted status lifecycle; this former type-0 duration/effect gap is closed.
- Native NPC combat message `8480` is closed for all five physical attacks on NPC `1006` (`Nahr`). The original 1.11p native chain proves both caller buffers are empty when equipped-item ID is zero; runtime preserves those empty arguments exactly. See `native_npc_combat_argument_boundary.md`.
- Decoded NPC spell target type `12` (`area-hostile-target`) is closed: the selected NPC target owns one cast, and all other persistent characters in the room receive independent authoritative resolution without per-session multiplication.

Date: 2026-07-29

## Current Local Baseline

- Classic compiler output contains `26,668/26,668` room descriptions.
- Classic runtime audit loads `26,668/26,668` room descriptions.
- Runtime audit loads `1,015` live NPC instances and `628` live monster instances with hard map-qualified placement validation.
- Librarium retains `2,183` quest evidence records partitioned into `83` decoded executable NPC quest flows plus `2,100` analysis-only candidates. Clean Classic generation emits exactly `83` executable quests, `83` live NPC quest-entry triggers, and `182` linked stage scripts containing `2,616` decoded operations; exhaustive NPC quest-entry dispatch validates all executable flows while the analysis candidates remain Librarium-only.
- Full current-health validator passes `13/13`.

## Decode Work Still Needed

### Room and World Placement

- Exact static room-item promotion emits and materializes `363` shared-world records across `287` rooms: `102` visible and `261` hidden-searchable. Room rendering, pickup, visible re-drop, Search discovery, and living-world persistence consume the generated family.
- Static-placement reset is explicit and idempotent at the host maintenance boundary: missing ordinary placements restore once even when another copy is character-owned, existing ground placements never duplicate, and decoded positive item `game_limit` values block restoration only when the global world-plus-owned count is already at the limit. No universal reset clock or unlinked dedicated item timer is scheduled because no exact interval/linkage is proven.
- Eight unsafe source rows are rejected: seven BBS-only `Sysop Support Chamber` rows and one item `0` row.
- Generated exit destinations are compiler-validated; unresolved destinations no longer reach runtime.
- Keep the two skipped map-qualified spawn records classified as invalid/non-spawn placements:
  - `spawn.288.43` -> `room.map1.288`; user verified that no such room exists.
  - `spawn.2306.213` -> `room.map1.2306`; user verified this room is a shop with no NPC.
- `Newhaven Arena` is verified as canonical `room.map1.2150`; older name-based and numeric probes are stale and must not be treated as missing generated room data.

### Native Actions and Messages

- Full remoteaction closure covers `139/139` decoded invocations and `85/85` distinct signatures with zero ambiguous, unresolved, or evidence-gated contracts: `62` paired-door toggles, `22` native state-bit clears, and `1` native common-finalization action.
- The verified 3,467-record WCCTEXT2 payload catalog is now promoted into Librarium and reproducibly regenerated as one `Data/Classic/native_textblock_payloads.json` runtime file. Clean compilation no longer deletes the F10 payload authority.
- WCCMSG2 promotion and runtime binding are complete for 3,869 logical messages. Map 17 pedestal IDs 3318-3322 render in exact decoded order.
- Map 17 boulder failure textblock 4235 executes message 3309 followed by self-targeted fire-damage spell 1324. The spell consumes decoded +5/+7 per-level magnitude growth and persists character HP damage.
- Classic completed quests are non-repeatable on the same character. Replay requires starting a new character; do not add same-character quest reset/restart behavior.

### Combat, Spells, and Status

- Full generated spell/status classification now covers 1,350/1,350 spells, all 105 decoded ability IDs, and 111/111 runtime-authoritative status effects with zero unclassified records.
- Live direct magnitude selection prioritizes decoded damage/heal ability vectors; status, resistance, expiration, resource, cure/dispel, and shared-world consumers remain data driven.
- Exact semantic names and output ordering for opaque numeric native dispatch branches remain structural evidence boundaries rather than runtime disconnects; no behavior is guessed.

### Economy and Services

- Full generated economy/service closure covers 178 shops, 26 executable room-bound banks/vaults, 44 trainers, 1,204 inventory relationships, 1,950 item economy records, and 23 rules with zero unclassified services. The literal `bank.2` / `Leave this blank` sentinel remains preserved in Librarium only.
- Live buying consumes decoded markup. Persistent restocking consumes decoded time, quantity, percentage, normal, and maximum stock fields; zero regeneration fields remain non-restocking.
- Five-denomination banks, trainers, player-stocked Recycler behavior, repair evidence gates, and deterministic failure paths are connected. Missing exact costs or modifiers continue to fail closed rather than using invented constants.

### Persistence and Host Runtime

- Native binary save/journal/recovery bytes remain unproven. The Reforged save format is functional but not claimed byte-compatible with Worldgroup/Btrieve behavior.
- Exact Worldgroup host callback ordering, module command dispatch, recovery transactions, and live Btrieve/MKDE durability behavior require live trace or stronger binary proof.

## Rule For Future Work

Promote behavior into executable Classic runtime only when it is verified/native or strongly correlated enough for the user-approved Classic baseline. Keep exact unknown native behavior evidence-gated and visible in reports.

Classic quest completion is final for that character. Same-character quest replay belongs only in a future Custom/AI rule set if explicitly approved, not in Classic.
