# NPC targeting and aggro

**Mechanic ID:** `NPC.TARGETING`  
**Bucket:** NPC AI  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Target selection, hostility, aggro retention, and group behavior.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2005
- Representative evidence entries: 60
- Source/path coverage: 19

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3380 — `ts.Write ("Increase" & vbTab & "Level Cap" & vbTab & "Difficulty" & vbTab & "Length" & vbTab & "Type of Resist" & vbTab & "Spell Type" & vbTab & "Target" & vbTab & "Resist Ability" & vbTab & "Type of Attack" & vbTab & "Cast Msg A" & vbTab & "Cast Msg B" & vbTab)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 2128 — `Dim DF_Flags As eDefenseFlags, eMobDefenseFlags As eDefenseFlags, eAttackFlags As eAttackRestrictions, bValidTarget As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3211 — `If eAttackFlags <= AR001_None Then bValidTarget = True`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4841 — `Dim DF_Flags As eDefenseFlags, bValidTarget As Boolean, nReturnSwings As Double, nAttackTypeMUD As eAttackTypeMUD`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 5081 — `If eAttackFlags <= AR001_None Then bValidTarget = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbp` line 62 — `Form=frmSwingCalc.frm`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbw` line 50 — `frmSwingCalc = 338, 338, 2136, 1083, C, 208, 208, 1372, 1054, C`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 16 — `-UP: update attack sim to allow for true dodge value (as computed in MME v1.9.2) or megamud's value`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 53 — `-UP: Added attack names field (will show attack names on monster details and in simulator)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 54 — `-UP: Added average monster damage and true attack percentages (unique, calculated fields for MME)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 215 — `-UP: updated the swing calculator`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 271 — `-NEW: swing calculator tool`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 4 — `Persistable = 0  'NotPersistable`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 11 — `Attribute VB_Creatable = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 15 — `Attribute VB_Ext_KEY = "Top_Level" ,"Yes"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 30 — `Private m_nAtkResist(0 To 4) As Integer`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 7 — `MBBSEmu is a cross-platform emulator for running legacy MajorBBS and Worldgroup modules (16-bit NE format DLLs) on modern systems. It emulates both the Galacticomm host environment and an x86-16 processor. Written in C# targeting .NET 10.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 76 — `- **Logging** (`Logging/`): `LogFactory` manages `MessageLogger`, `AuditLogger`, `CPULogger` with pluggable targets.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 125 — `of background processing (such as monster attacks)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Integration/BtrievePrint_Tests.cs` line 580 — `session.SendToModule(Encoding.ASCII.GetBytes("SWhatever\r\n"));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 6 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 12 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 18 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 24 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 204 — `- `OpKind.NearBranch16` — direct branch target offset`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
