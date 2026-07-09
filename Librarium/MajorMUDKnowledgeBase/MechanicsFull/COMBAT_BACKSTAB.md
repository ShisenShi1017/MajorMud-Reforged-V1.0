# Backstab/special attack resolution

**Mechanic ID:** `COMBAT.BACKSTAB`  
**Bucket:** Combat  
**Status:** open_conflict  
**Runtime:** not_changed_by_phase2_population

## Description
How backstab and special physical attacks interact with hit, dodge, defenses, and damage.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 251
- Representative evidence entries: 13
- Source/path coverage: 6

## Notes
- Conflict carried forward: special/backstab defense order requires exact pipeline resolution.

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 20 — `if the character wasn't also backstabbing. Even in those cases, it was the backstab weapon magic being considered.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 52 — `-FIX: Backstab accuracy calculation for [GreaterMUD]`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 525 — `-NEW: backstab calculator`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 432 — `-NEW: backstab calculator`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 18 — `if the character wasn't also backstabbing. Even in those cases, it was the backstab weapon magic being considered.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 49 — `-FIX: Backstab accuracy calculation for [GreaterMUD]`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` line 5 — `Caption         =   "Backstab Calculator"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 228 — `Caption         =   "Backstab"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 957 — `Dim bShadow As Boolean, bSeeHidden As Boolean, bBackstab As Boolean, bVSplayer As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 963 — `If optHitCalcType(1).Value = True Then bBackstab = True`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 1023 — `nVileWard, eEvil, bShadow, bSeeHidden, bBackstab, bVSplayer, nClass)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 1094 — `'        If optHitCalcType(1).Value = True Then 'backstab - bs defense`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 625 — `SomeUserFlags(7) As Byte            '8      1788   629  flag sneaking (0x6f4==1780): *(uint16_t*)((char*)arg1 + 0x6f4) |= 4;`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
