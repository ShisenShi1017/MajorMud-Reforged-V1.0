# Backstab / Special Attack Defense

**Mechanic ID:** `COMBAT_BACKSTAB_SPECIAL`

**Domain:** Combat

**Status:** open_conflict

**Confidence:** blocked_by_open_conflict

**Evidence hits:** 245

**Unique evidence files:** 12

**Runtime status:** do_not_implement_as_classic_verified_yet


## Description

Special attack and backstab defensive ordering and interaction with dodge/AC/DR.


## Source agreement

- MMUDExplorer: 245 hits

## Primary buckets

- tooling_ui: 122
- combat: 98
- character_progression: 25

## Open conflicts

- `CONFLICT_BACKSTAB_SPECIAL_DEFENSE_001`: Backstab/special attack defense order — open_needs_specific_scenario_tests
  - Policy: Treat as unresolved until cross-source scenario tests exist.

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (81 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` (64 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (39 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` (19 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (18 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` (9 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (6 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/README.md` (3 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` (2 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` (2 hits)

## Representative evidence

- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/README.md` line 20: if the character wasn't also backstabbing. Even in those cases, it was the backstab weapon magic being considered.
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/README.md` line 52: -FIX: Backstab accuracy calculation for [GreaterMUD]
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/README.md` line 525: -NEW: backstab calculator
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 432: -NEW: backstab calculator
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 18: if the character wasn't also backstabbing. Even in those cases, it was the backstab weapon magic being considered.
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 49: -FIX: Backstab accuracy calculation for [GreaterMUD]
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` line 5: Caption         =   "Backstab Calculator"
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 228: Caption         =   "Backstab"
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 957: Dim bShadow As Boolean, bSeeHidden As Boolean, bBackstab As Boolean, bVSplayer As Boolean
- MMUDExplorer `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 963: If optHitCalcType(1).Value = True Then bBackstab = True

## Next action

Resolve the conflict before runtime implementation is marked classic-verified. Use Nightmare Redux as leading hypothesis only where source-priority policy applies.