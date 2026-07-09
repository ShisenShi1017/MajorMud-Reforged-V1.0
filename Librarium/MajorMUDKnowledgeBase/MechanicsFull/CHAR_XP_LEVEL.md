# Experience and level progression

**Mechanic ID:** `CHAR.XP_LEVEL`  
**Bucket:** Character Progression  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
XP thresholds, level checks, and leveling rules.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 13156
- Representative evidence entries: 37
- Source/path coverage: 17

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 104 — `-NEW: tool to combine monster experience and experience multiplier`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 107 — `-UP: exp calc will copy to clipboard the selected levels experience`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 377 — `-NEW: a pseudo tool to multiply just boss experience`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 617 — `-NEW: divide monster experience`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 117 — `-NEW: tool to combine monster experience and experience multiplier`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 120 — `-UP: exp calc will copy to clipboard the selected levels experience`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 390 — `-NEW: a pseudo tool to multiply just boss experience`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 25 — `---This function will divide all monster experience by a specific amount. (2-20) with a minimum experience of 1 possible. Useful for those who may have multiplied their monster experience more than they wanted to.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 27 — `---This function will multiply all monster experience by a specific amount. (2-20)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 146 — `.Columns.Append "Experience", adDouble		==> EXP`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4520 — `ts.Write ((CDbl(SLong2ULong(Monsterrec.Experience)) * CDbl(SLong2ULong(Monsterrec.ExpMulti))) & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4522 — `ts.Write (SLong2ULong(Monsterrec.Experience) & vbTab)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/mmud_edit.py` line 31 — `if args.experience is not None:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/mmud_edit.py` line 32 — `#print('Experience is {}'.format(args.experience))`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/mmud_edit.py` line 34 — `b[0x470] = ((args.experience >> 8) & 0xFF)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/mmud_edit.py` line 35 — `b[0x471] = ((args.experience >> 16) & 0xFF)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/mmud_edit.py` line 36 — `b[0x472] = ((args.experience >> 24) & 0xFF)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/readme.md` line 12 — `mmud_edit.py --username=USERNAME [--experience=EXPERIENCE]`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 196 — `-UP: Experience now calculated internally and no need for lltmmudxp.dll`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 409 — `-NEW: filtering capabilities: cash, hp, damage, regen, experience`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 134 — `You gain 1500 experience.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 217 — `You gain 2250 experience.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 438 — `You gain 2576 experience.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 102 — `-UP: Experience now calculated internally and no need for lltmmudxp.dll`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 316 — `-NEW: filtering capabilities: cash, hp, damage, regen, experience`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
