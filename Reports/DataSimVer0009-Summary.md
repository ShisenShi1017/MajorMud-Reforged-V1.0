# DataSimVer0009 Summary — Quest/Flag/Conditional State Discovery

## Objective
Discover quest, flag, membership, command phrase, key/item, level/rank, and state-condition clues implied by `/Data` without changing `/Data`.

## What Was Simulated
This pass used the Worldgroup-era, data-driven interpretation model established in DataSimVer0005–0008. It scanned rooms, items, NPCs/monsters, and spells for condition vocabulary and explicit restriction/state fields, then generated expected-model hypotheses for stateful access.

## Discoveries
- Room condition clues: **610**
- Item condition clues: **1950**
- NPC/monster condition clues: **1084**
- Spell condition clues: **523**
- Low-confidence room/item gate hypotheses: **544**
- `quests.json` currently has **0** records, so quest behavior must be inferred from other tables until engine or runtime evidence confirms actual flag handling.

## Statistics
- Rooms analyzed: **1246**
- Items analyzed: **1950**
- Monsters analyzed: **1100**
- Spells analyzed: **1350**
- Room endpoints promoted: **0**
- Special-exit semantics promoted: **0**

## Changes
- `/Data`: **Unchanged**
- Added DataSimVer0009 reports under `/Simulator`, `/Evidence`, `/Validation`, `/Reports`, `/Documentation`, and `/Codex`.
- No old/nested ZIPs included.

## Remaining Unknowns
- Actual quest flag storage and bit-field layout.
- Exact condition evaluation order.
- Whether command phrases are resolved before or after special exit checks.
- Which item clues are true keys/flags versus flavor text.
- Which NPCs set flags, give items, or unlock progression.

## Impact
This pass does not prove original runtime behavior, but it creates a comparison map for your reverse-engineering work. Once MBBSEmu/original module findings identify flag fields or parser order, these rows can be reclassified from expected-model clues into verified engine behavior.

## Next Pass
DataSimVer0010 should run spell/travel/effect behavior discovery, then generate a consolidated 0001–0010 handoff for a new window.

## Session Health
Status: ✅ Continue current window through DataSimVer0010. After 0010, start a new window with the latest cumulative ZIP and handoff.
