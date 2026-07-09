# Critical Hits

**Mechanic ID:** `COMBAT_CRITICAL`

**Domain:** Combat

**Status:** probable_needs_reconstruction

**Confidence:** medium_reconstruction_needed

**Evidence hits:** 78

**Unique evidence files:** 24

**Runtime status:** requires_formula_record_expansion_before_runtime_sync


## Description

Critical hit detection, modifiers, and damage amplification.


## Source agreement

- NightmareRedux: 30 hits
- MBBSEmu: 12 hits
- MMUDExplorer: 36 hits

## Primary buckets

- tooling_ui: 33
- combat: 21
- character_progression: 8
- logging_debug: 6
- networking_runtime: 4
- tests_validation: 2
- world_spawns: 2
- data_schema: 1
- quests_scripts: 1

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (16 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/modMain.bas` (14 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` (12 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modSyntaxsFunc.bas` (5 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmSwingCalc.frm` (4 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (3 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (3 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` (2 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` (2 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExecutionUnits/CLAUDE.md` (2 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 528: -UP: mass room editor now logs errors (and only the critical errors) to a log file
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 559: -UP: mass room editor now logs errors (and only the critical errors) to a log file
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5693: MsgBox "Error regetting room " & nTempMap & "/" & nTempRoom, vbCritical
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 159: Caption         =   "Crit AVG"
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 169: Caption         =   "Crit %"
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1573: If x = 0 Then GoTo Crit:
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1577: If nExtraP = 0 Then GoTo Crit:
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1580: If x = 0 Then GoTo Crit:
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1583: nExtraA = Val(Mid(sClipText, x, InStr(x, sClipText, "Crit") - x))
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1584: If nExtraA = 0 Then GoTo Crit:

## Next action

Expand the formulas and data-field mapping for this mechanic using the evidence records above.