# Combined Decode Audit After Items

## Merge result

Items DONE was merged into the combined Character, Textblock/Command/Ability, Economy, Remaining Systems, Combat/Spells, and NPC/AI cumulative overlay.

- Base files: 1,433
- Item archive unique files: 197
- New paths added: 195
- Shared paths reconciled: 2
- Shared runtime reconciliation preserved both the newer Item lifecycle implementation and the Character/NPC compatibility surfaces already consumed by the combined baseline.
- Equivalent remaining-work findings are represented once in this audit; distinct evidence and provenance files remain preserved.

## Decode status

All major gameplay subsystem packages are now combined and have reached their pre-native boundary:

- Rooms
- Triggers, Scripts, and Quests
- Character
- Textblocks, Commands, and Abilities
- NPC and AI
- Combat and Spells
- Economy
- Remaining Systems
- Items

There are no additional major subsystem decode windows required before native closure.

## What remains

The remaining project phase is the **Final WCC/WBB/native DLL and leftover-file semantic exhaustion pass**. It must close the native-only dependency families below:

1. Seven unresolved Room latch effects and qualification of the retained unknown exits.
2. Native WCC/WBB record layouts, field widths, enums, flags, masks, restrictions, and unknown bytes.
3. Native command and script dispatch, including `remoteaction`, player-command aliases, prefixes, and parsers.
4. Exact formulas, execution ordering, timing, scheduling, random ranges, seeding, and RNG behavior.
5. Native character/world save layouts, checksums, journals, backups, migrations, and crash recovery.
6. Native-only Character lifecycle, reconnect, persistence, and formula edge cases.
7. Native-only NPC/AI spawn, perception, hostility, threat, pathing, chase/leash, and timer behavior.
8. Native-only Combat/Spell enums, formulas, flags, masks, timing, status, and restart semantics.
9. Native-only Economy calculation ordering and executable-path/offset binding.
10. Native-only Item category/flag mapping, stack limits, charges, durability, loot/reset RNG, persistence, command ordering, and exact messages.
11. Remaining Systems Classic behavior and MajorMUD/Worldgroup ownership boundaries.
12. Final provenance graph, validation suite, compatibility report, and native-dependency closure report.

## Items-specific boundary

Items CodeUpdate0204 reports pre-native semantic exhaustion complete for 1,950 canonical item records. Remaining Item unknowns are explicitly native-dependent and should be resolved in the final native pass, not in another standalone Item decode window.
