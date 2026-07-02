# Validation0004 — Combat, Quest, and Persistence Evidence Mapping

## Objective
Map available external evidence for combat, quest, player/world persistence, and promotion readiness using the current Validation0003 baseline plus MBBSEmu, MBBSDASM, MBBSEmu.Disassemblies, and the MDB extractor-only tool.

## What was scanned
- MajorMUD disassembly: `MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`
- MBBSEmu C# source files: 457
- External source ZIPs inventoried: 4
- Data files hashed for no-change verification: 92

## Discoveries
- MajorMUD disassembly contains strong textual/code-symbol evidence around combat, attacks, spells, monsters, rooms, save/load, quest, gold, and experience.
- MBBSEmu host source contains callback/scheduler/storage evidence relevant to module lifecycle, Btrieve-style access, users, channels, events, timers, RTKICK, and polling.
- Evidence is sufficient to continue function-level mapping, but not sufficient yet to promote exact formulas or persistence semantics into `/Data`.

## Key counts
- MajorMUD far procedures found: 800
- MajorMUD near procedures found: 2
- Disassembly `monster` hits: 3261
- Disassembly `quest` hits: 144
- Disassembly `attack` hits: 970
- Disassembly `spell` hits: 1604
- Disassembly `save` hits: 588
- MBBSEmu `Btrieve` hits: 1677
- MBBSEmu `RTKICK` hits: 23
- MBBSEmu `BEGIN_POLLING` hits: 9

## Promotion review
No `/Data` promotion in this pass. The evidence currently validates that relevant routines and host systems exist, but the exact behavioral rules still require direct routine mapping:
- Combat formulas: not promoted
- Quest persistence rules: not promoted
- Player/world save layout: not promoted
- Boss/unique monster semantics: not promoted
- Cleanup/search/respawn exact flags: not promoted

## Remaining unknowns
- Which disassembly procedures implement player save/load versus world save/load.
- Exact combat damage/hit resolution formula.
- Exact spell/status resolution rules.
- Exact quest flag storage and completion checks.
- Whether quest state is player-scoped, world-scoped, or mixed.

## Next validation pass
Validation0005 should perform function-level clustering around the highest-value disassembly neighborhoods: combat/attack, spell resolution, quest flag checks, save/load persistence, and Btrieve-backed data access.
