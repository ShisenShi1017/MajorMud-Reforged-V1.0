# Item and Equipment Effects

**Mechanic ID:** `ITEM_EFFECTS`

**Domain:** Items / Equipment

**Status:** probable_needs_reconstruction

**Confidence:** medium_reconstruction_needed

**Evidence hits:** 1738

**Unique evidence files:** 311

**Runtime status:** requires_formula_record_expansion_before_runtime_sync


## Description

Equipment modifiers, effects, restrictions, charges, flags, and stat interactions.


## Source agreement

- NightmareRedux: 439 hits
- MBBSEmu: 1015 hits
- MMUDExplorer: 284 hits

## Primary buckets

- tooling_ui: 399
- character_progression: 373
- commands_client: 275
- networking_runtime: 216
- data_schema: 150
- items_equipment: 102
- quests_scripts: 80
- tests_validation: 77
- combat: 55
- logging_debug: 7

## Top evidence files

- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` (90 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (89 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMain.frm` (82 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` (80 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (73 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmRecordChange.frm` (62 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` (58 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` (53 hits)
- MBBSEmu: `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/tfs_Tests.cs` (43 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (41 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 1: Monster Item Drop Percentage Modifier 8/5/2021 @ 11:11:49 AM
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 498: 9537|0|9483|death spell of dark mage -- flag check, give 175 mill exp bonus
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 503: 9547|0|0|death spell connected to text block -- flag check, add 50 mill exp bonus
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 59: 'Becouse I have no idea how the buld process works and I could not stop it moving the f$%king buttons
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 332: ' called only from substitutedWndProc for MDI window
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 845: ' process (even if it is over another object)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 106: Declare Function CallWindowProc Lib "user32" Alias "CallWindowProcA" (ByVal lpPrevWndFunc As Long, ByVal hWnd As Long, ByVal Msg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 182: Private pfWndProc As Long
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 257: Public Declare Function DefWindowProc Lib "user32" Alias "DefWindowProcA" (ByVal hWnd As Long, ByVal wMsg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 316: '/// m_lPrevProc is the address of the procedure

## Next action

Expand the formulas and data-field mapping for this mechanic using the evidence records above.