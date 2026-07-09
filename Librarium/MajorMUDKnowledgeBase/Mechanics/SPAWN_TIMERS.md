# Spawn Timers and World Scheduling

**Mechanic ID:** `SPAWN_TIMERS`

**Domain:** World / Spawns

**Status:** probable_needs_reconstruction

**Confidence:** medium_reconstruction_needed

**Evidence hits:** 453

**Unique evidence files:** 60

**Runtime status:** requires_formula_record_expansion_before_runtime_sync


## Description

Spawn timing, event timers, room/event scheduling, and world lifecycle references.


## Source agreement

- NightmareRedux: 58 hits
- MBBSEmu: 100 hits
- MMUDExplorer: 295 hits

## Primary buckets

- combat: 174
- tooling_ui: 120
- character_progression: 46
- networking_runtime: 43
- commands_client: 34
- logging_debug: 12
- world_spawns: 10
- magic: 7
- docs_notes: 5
- tests_validation: 2

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` (123 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (26 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` (25 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (25 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` (22 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmCoinConvert.frm` (12 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` (11 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` (11 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` (9 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmSwingCalc.frm` (9 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 12: Begin VB.Timer tmrRefresh
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 17: Begin VB.Timer FlashTimer
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 22: Begin VB.Timer tmrMouse
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 365: Private Sub FlashTimer_Timer()
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 389: 'Stop the Timer
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 390: FlashTimer.Enabled = False
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 581: 'Start the Timer
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 582: FlashTimer.Enabled = True
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 593: Private Sub tmrMouse_Timer()
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 595: ' this timer is used to take away the focus rect, for bars

## Next action

Expand the formulas and data-field mapping for this mechanic using the evidence records above.