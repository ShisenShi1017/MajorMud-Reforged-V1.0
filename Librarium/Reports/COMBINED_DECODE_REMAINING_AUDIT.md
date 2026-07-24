# Combined Semantic Exhaustion Audit

## Merge result

- Source archives: 3
- Unique file paths: 401
- Filename collisions: 0
- Conflicting files: 0
- Duplicate consolidated findings: 0

## Included subsystem status

- **Character** — managed runtime semantically exhausted; 6 native-only lifecycle dependencies quarantined (CodeUpdate0166).
- **Textblocks / Commands / Abilities** — closed for current decoded corpus; 4 native-only dependencies quarantined (CodeUpdate0160).
- **Economy** — managed economy passes through service-cost finalization included; remaining native ordering/binding retained (CodeUpdate0164).

## Remaining decode work

1. **Textblocks/Commands/Abilities — native.message_payload_catalog**
   UNKNOWN_NATIVE_DEPENDENCY; 130 message references. Decode exact message payloads, substitution grammar, color/formatting, pagination and timing.
2. **Textblocks/Commands/Abilities — native.textblock_payload_catalog**
   UNKNOWN_NATIVE_DEPENDENCY; 389 invocations / 156 unique targets. Decode native textblock bodies, branch encoding, nested return/termination behavior and embedded expressions.
3. **Textblocks/Commands/Abilities — native.remoteaction_dispatcher**
   UNKNOWN_NATIVE_DEPENDENCY; remoteaction 1423 66 0 3. Recover dispatcher entry point, argument decoding, target resolution, mutation order and rollback.
4. **Textblocks/Commands/Abilities — native.player_command_table**
   UNKNOWN_NATIVE_DEPENDENCY; commands outside 77 managed registrations. Enumerate native commands, aliases, minimum prefixes, handlers and argument parsers.
5. **Character — character.welcome_event_ordering**
   UNKNOWN_NATIVE_DEPENDENCY; login lifecycle. Recover exact welcome and event-message ordering.
6. **Character — character.reconnect_grace_socket_takeover**
   UNKNOWN_NATIVE_DEPENDENCY; reconnect lifecycle. Recover reconnect grace interval and native socket ownership takeover.
7. **Character — character.link_dead_combat_retention**
   UNKNOWN_NATIVE_DEPENDENCY; disconnect/combat lifecycle. Recover native link-dead combat retention duration and cleanup order.
8. **Character / Parties — character.party_leadership_succession**
   UNKNOWN_NATIVE_DEPENDENCY; party disconnect lifecycle. Recover native party leadership succession ordering.
9. **Character / Trading — character.trade_escrow_disposition**
   UNKNOWN_NATIVE_DEPENDENCY; disconnect during trade. Recover escrow disposition beyond safe cancellation.
10. **Character / World Runtime — character.offline_corpse_decay_reconciliation**
   UNKNOWN_NATIVE_DEPENDENCY; offline corpse timers. Recover corpse timer reconciliation while owner is offline.
11. **Economy — economy.native_service_cost_stage_order**
   UNKNOWN_NATIVE_DEPENDENCY; service-cost finalization. Prove native ordering of minimum price, additive adjustments, multiplier composition and finalization.
12. **Economy — economy.native_markup_sources**
   UNKNOWN_NATIVE_DEPENDENCY; native markup and observations. Bind remaining native markup/service-cost observations to exact executable paths and source offsets.
13. **Rooms — rooms.native_latch_effects**
   UNKNOWN_NATIVE_DEPENDENCY; 7 room latch effects. Resolve the seven tracked native latch effects during final WCC/WBB/DLL pass.
14. **Rooms — rooms.unknown_qualified_exits**
   PRESERVED_UNKNOWN_DATA; 266 exits. Resolve qualifiers only with authoritative native evidence; preserve current fail-safe routing.
15. **Items — items.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete item definitions, instances, ownership, stacking, charges, durability, use, containers, limited-item rules, persistence and cross-system integration.
16. **NPC & AI — npc_ai.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete NPC records, spawn/despawn, AI loop, perception, hostility, movement, dialogue, merchants/trainers, death, respawn and persistence.
17. **Combat & Spells — combat_spells.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete combat lifecycle, timing, hit/damage formulas, casting, targeting, resistance, saves, status, death and resurrection.
18. **World Objects / Containers / Doors / Locks / Traps — world_objects.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete shared object ownership, door/lock state, keys, traps, portable/world containers, reset and persistence.
19. **World Runtime / Resets / Events / Scheduling — world_runtime.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete authoritative clock, timer ordering, reset lifecycle, events, hard timers, restart restoration and global state.
20. **Parties / Guilds / Factions / Reputation — parties_factions.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete membership, leadership, shared rewards, faction reactions, reputation, law and organization restrictions.
21. **Sessions / Accounts / Login / Persistence Coordination — sessions_persistence.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete session ownership, account boundary, linkdead handling, duplicate sessions, autosave coordination, startup/shutdown and crash recovery.
22. **Communications & Social — communications.complete_runtime**
   BOUNDARY_AUDIT_REQUIRED; MajorMUD vs Worldgroup boundary. Determine MajorMUD-owned channels, routing, ignores/mutes, offline messages, moderation and host-platform ownership.
23. **Administration / Sysop / Builder / Permissions — administration.complete_runtime**
   SUBSYSTEM_NOT_INCLUDED; full subsystem. Complete permissions, privileged mutation policy, audit logging, builder lifecycle and security boundaries.
24. **Remaining Systems — remaining_systems.inventory**
   GAP_AUDIT_REQUIRED; repository-wide. Inventory any unassigned runtime service, record family, command family, timer, flag, global variable or host integration.
25. **Final WCC/WBB/DLL/File Pass — final_native_formats**
   FINAL_PHASE; all quarantined dependencies. Resolve binary record layouts, save formats, native flags/masks, dispatcher paths, timing/random behavior and all surviving subsystem dependencies.

## Recommended continuation order

1. Items
2. NPC & AI
3. Combat & Spells
4. World Objects / Containers / Doors / Locks / Traps
5. World Runtime / Resets / Events / Scheduling
6. Parties / Guilds / Factions / Reputation
7. Sessions / Accounts / Login / Persistence Coordination
8. Communications boundary audit
9. Administration / Sysop / Builder / Permissions
10. Remaining Systems inventory
11. Final WCC/WBB/DLL/File pass

The detailed machine-readable audit is `Librarium/Reports/combined_decode_remaining_audit.json`.
