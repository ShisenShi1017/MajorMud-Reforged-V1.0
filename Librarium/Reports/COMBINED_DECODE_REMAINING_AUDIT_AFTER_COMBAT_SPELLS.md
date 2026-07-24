# Combined Semantic Exhaustion Audit — Combat & Spells Added

## Merge result

- Existing combined files: 666
- Combat and Spells files added: 526
- Source files before new audit artifacts: 1192
- Filename collisions: 0
- Conflicting collisions: 0

## What the new archive closes

- Combat and Spells non-native evidence is exhausted through Update0185.
- Canonical classification covers **1,930 records**.
- **1,927** records remain canonical-identity-only and fail closed.
- **1** record is native-blocked: `spell.1304`.
- **1** isolated deterministic spell fixture and **1** weapon magnitude definition binding are promoted only within their proven scope.
- Combat and Spells are therefore removed from the full-subsystem-not-included list, but they are not native-complete.

## What still needs to be decoded

1. **Items — full subsystem**
   Definitions and runtime instances; ownership and transfers; stacking; charges; durability; use behavior; portable containers; limited/unique rules; persistence; death/corpse; reset/restock; cross-subsystem relationships.

2. **NPC & AI — full subsystem**
   NPC records and instances; spawn/despawn/respawn; AI update loop; perception; hostility; target selection; movement/pathing; dialogue; merchants/trainers; inventory; death; persistence.

3. **Combat & Spells — native closure only**
   Raw enum meanings; native timing; flags and masks; formulas; random ranges; save/restart behavior; and exact `spell.1304` target, resistance, timing, resource, and effect semantics. The 1,927 identity-only records stay fail-closed until record-specific evidence exists.

4. **Remaining Systems — native closure**
   The repository-visible inventory is closed across 36 registered systems, but exact Classic behavior still needs native records, formulas, commands, messages, persistence, timing, ownership boundaries, and Worldgroup integration evidence.

5. **Character — native boundary**
   Welcome/event ordering; reconnect grace and socket takeover; link-dead combat retention; party leadership succession; trade escrow disposition; offline corpse-decay reconciliation.

6. **Textblocks, Commands & Abilities — native boundary**
   Exact message payload catalog; native textblock bodies and branching; `remoteaction` dispatcher; native player-command table, aliases, prefixes, and parsers.

7. **Economy — native boundary**
   Exact service-cost calculation stage order and source-offset binding for remaining markup/service observations.

8. **Rooms — native boundary**
   Seven unresolved native latch effects and 266 unknown-qualified exits.

9. **Final WCC/WBB/DLL/File exhaustion**
   Resolve surviving native layouts, flags, masks, save formats, dispatcher paths, timing/random behavior, host boundaries, and all quarantined subsystem dependencies.

## Recommended continuation order

1. Items
2. NPC & AI
3. Final native closure across all completed managed/non-native subsystems
4. Final WCC/WBB/DLL/File exhaustion
