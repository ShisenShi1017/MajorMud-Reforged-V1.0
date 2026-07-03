# Validation0043 — `_PERFORM_MATCHED_ACTION` Opcode Dispatch Pass

Date: 2026-07-03

## Scope

Validation0043 decodes the native `_PERFORM_MATCHED_ACTION` dispatcher using the frozen Reference Pack disassembly and correlates the recovered opcode strings with MMExplorer/live-user evidence supplied during the session.

## Inputs

- Living baseline: `MajorMUD-Reforged-Living-Validation0042-Cumulative.zip`
- Reference evidence: `MajorMUD-Reforged-ReferencePack-Validation0029(2).zip`
- Native disassembly: `Evidence/ExternalSources/SourceSnapshots/MBBSEmu.Disassemblies/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`
- User MMExplorer/runtime screenshots added under `Evidence/Validation0043/`

## Verified native dispatcher structure

`_PERFORM_MATCHED_ACTION` is a large opcode dispatcher called by `_PERFORM_SPECIAL_COMMAND`. The dispatcher compares the current script/action token against literal opcode strings using repeated `SAMETO` calls. On match, each branch parses arguments using common parser helpers such as `SKPWRD`, `SKPWHT`, and `ATOL`, then dispatches into a target subsystem routine.

This confirms that textblock actions are interpreted as data-driven command tokens, not hardcoded NPC-specific behaviors.

## Recovered opcode strings

- `price` (condition/economy) — asm excerpt line 95; calls near branch: near ptr sub_7D70B, _ADD_ITEM_TO_INVENTORY, _ALLOC_GET_SPELL_DATA
- `cast` (action/spell) — asm excerpt line 158; calls near branch: _ALLOC_GET_SPELL_DATA, _CAST_NO_TARGET, _FREE_SPELL_DATA
- `giveability` (action/ability) — asm excerpt line 236; calls near branch: _ADD_SPELL_TO_SPELLBOOK
- `addability` (action/ability) — asm excerpt line 405; calls near branch: sub_7A943, near ptr sub_7DD47
- `teleport` (action/movement) — asm excerpt line 488; calls near branch: near ptr sub_7DD47, _PURGE_SPELL_FROM_SPELLBOOK, _GET_ROOM_DATA
- `removeability` (action/ability) — asm excerpt line 510; calls near branch: _PURGE_SPELL_FROM_SPELLBOOK, _GET_ROOM_DATA
- `nomonsters` (condition/room) — asm excerpt line 610; calls near branch: _GET_ROOM_DATA, near ptr sub_7D630, _GET_ROOM_DATA
- `needmonster` (condition/monster) — asm excerpt line 806; calls near branch: near ptr sub_7E6A6, _GENERATE_MONSTER, near ptr sub_7D630
- `summon` (action/npc) — asm excerpt line 828; calls near branch: _GENERATE_MONSTER, near ptr sub_7D630
- `message` (action/output) — asm excerpt line 897; calls near branch: near ptr sub_7D630, _REMOVE_ITEM_FROM_INVENTORY, _ADD_ITEM_TO_INVENTORY
- `takeitem` (action/inventory) — asm excerpt line 941; calls near branch: _REMOVE_ITEM_FROM_INVENTORY, _ADD_ITEM_TO_INVENTORY, near ptr sub_7D630
- `addexp` (action/character) — asm excerpt line 1097; calls near branch: _ADD_QUEST_EXP, _ADD_ITEM_TO_INVENTORY, CLRPRF
- `giveitem` (action/inventory) — asm excerpt line 1138; calls near branch: _ADD_ITEM_TO_INVENTORY, CLRPRF, _GET_ITEM_DATA
- `hideitem` (action/room-item) — asm excerpt line 1225; calls near branch: _GET_ITEM_DATA, _ADD_ITEM_TO_ROOM, _USER_HAS_ABILITY
- `checkability` (condition/ability) — asm excerpt line 1296; calls near branch: _USER_HAS_ABILITY, _GET_USER_ABILITY_VALUE
- `class` (condition/character) — asm excerpt line 1411; calls near branch: none identified in near window
- `race` (condition/character) — asm excerpt line 1462; calls near branch: near ptr sub_7D630
- `goodaligned` (condition/alignment) — asm excerpt line 1513; calls near branch: near ptr sub_7D630
- `evilaligned` (condition/alignment) — asm excerpt line 1606; calls near branch: near ptr sub_7D630
- `minlevel` (condition/level) — asm excerpt line 1699; calls near branch: near ptr sub_7D630
- `maxlevel` (condition/level) — asm excerpt line 1792; calls near branch: near ptr sub_7D630
- `checkitem` (condition/inventory) — asm excerpt line 1885; calls near branch: near ptr sub_7D630
- `failitem` (condition/inventory) — asm excerpt line 2012; calls near branch: near ptr sub_7D630
- `text` (action/output) — asm excerpt line 2139; calls near branch: _DISPLAY_LONG_TEXT, _DISPLAY_LONG_TEXT_TO_ROOM, _GET_ROOM_DATA
- `roomtext` (action/output-room) — asm excerpt line 2183; calls near branch: _DISPLAY_LONG_TEXT_TO_ROOM, _GET_ROOM_DATA
- `roomitem` (condition/room-item) — asm excerpt line 2231; calls near branch: _GET_ROOM_DATA, near ptr sub_7D630
- `failroomitem` (condition/room-item) — asm excerpt line 2372; calls near branch: _GET_ROOM_DATA, near ptr sub_7D630
- `clearitem` (action/room-item) — asm excerpt line 2513; calls near branch: sub_7A544, near ptr sub_7D630
- `addevil` (action/alignment) — asm excerpt line 2606; calls near branch: _USER_HAS_ABILITY, _GET_USER_ABILITY_VALUE
- `testability` (condition/ability) — asm excerpt line 2646; calls near branch: _USER_HAS_ABILITY, _GET_USER_ABILITY_VALUE
- `failability` (condition/ability) — asm excerpt line 2775; calls near branch: _USER_HAS_ABILITY, near ptr sub_7D630
- `remoteaction` (action/remote) — asm excerpt line 2874; calls near branch: _GET_ROOM_DATA
- `random` (flow/random) — asm excerpt line 3036; calls near branch: near ptr sub_80A66, near ptr sub_7D949
- `adddelay` (flow/timing) — asm excerpt line 3094; calls near branch: near ptr sub_7D949, TOUPPER
- `testskill` (condition/skill) — asm excerpt line 3132; calls near branch: near ptr sub_7D949, TOUPPER
- `checkskill` (condition/skill) — asm excerpt line 3145; calls near branch: near ptr sub_7D949, TOUPPER
- `givecoins` (action/economy) — asm excerpt line 3171; calls near branch: TOUPPER
- `test_tournament` (condition/event) — asm excerpt line 3353; calls near branch: near ptr sub_7D83A, near ptr sub_7E22F, near ptr sub_7E528
- `checkspell` (condition/spell) — asm excerpt line 3394; calls near branch: near ptr sub_7D83A, near ptr sub_7E22F, near ptr sub_7E528
- `flag` (condition/flag) — asm excerpt line 3416; calls near branch: near ptr sub_7E22F, near ptr sub_7E528
- `learnspell` (action/spell) — asm excerpt line 3438; calls near branch: near ptr sub_7E528

## High-confidence subsystem mapping

- Ability/quest state: `giveability`, `addability`, `removeability`, `checkability`, `testability`, `failability`.
- Inventory/item flow: `checkitem`, `takeitem`, `giveitem`, `failitem`.
- Room item/environment checks: `roomitem`, `failroomitem`, `clearitem`, `hideitem`.
- Economy: `price`, `givecoins`.
- Movement: `teleport`.
- Spell subsystem: `cast`, `checkspell`, `learnspell`.
- Character gates: `class`, `race`, `minlevel`, `maxlevel`, `testskill`, `checkskill`, alignment gates.
- Output/flow: `message`, `text`, `roomtext`, `random`, `adddelay`, `remoteaction`.
- NPC/monster/system gates: `nomonsters`, `needmonster`, `summon`, `test_tournament`, `flag`.

## Correlation to user MMExplorer evidence

The screenshots supplied during the session are consistent with the native opcode table:

- Quest state: GoodQuest/NeutralQuest/EvilQuest and MageBaneQuest use ability opcodes.
- Craft/turn-in: black chitin/spider silk armor and apple pie exchange use item/economy opcodes.
- Dao Lord/Sunstone logic uses `checkability`, `giveability`, `failitem`, `failroomitem`, and `giveitem`.
- Barmaid examples use `class`, `testskill`, `teleport`, `takeitem`, and `givecoins`.
- Beautiful woman example uses `cast fruit(703)`, and Spell 703 is present as a normal spell record.

## Promotion decision

Promoted:

- `_PERFORM_MATCHED_ACTION` is the matched textblock action dispatcher.
- Opcode matching is literal string/token based via repeated `SAMETO` comparisons.
- The recovered opcode strings listed in `VALIDATION0043_OPCODE_TABLE.csv` are true script opcodes present in native disassembly.
- Textblock scripting is a generic interpreter layer that dispatches into existing subsystems.

Not promoted yet:

- Exact failure/continue/short-circuit rules for every opcode branch.
- Exact argument layouts for every opcode.
- Whether all opcode branches return the same status semantics.
- Complete mapping of helper subroutines with unlabeled names such as `sub_7D630`, `sub_7A943`, `sub_7D83A`, `sub_7E22F`, and `sub_7E528`.

## Impact

This pass substantially improves server-readiness. The game should be implemented around a data-driven script interpreter whose opcode catalog begins with the recovered native table here. Server construction can begin with promoted opcodes and mark unresolved branch semantics as TODOs backed by validation tasks.

## Next target — Validation0044

Validation0044 should decode argument and return semantics for the highest-value opcodes:

1. `giveitem`, `takeitem`, `checkitem`, `failitem`, `failroomitem`
2. `giveability`, `addability`, `checkability`, `testability`, `failability`
3. `price`, `givecoins`
4. `teleport`, `cast`

The specific goal is to determine whether each opcode short-circuits, continues, branches, or only emits output.
