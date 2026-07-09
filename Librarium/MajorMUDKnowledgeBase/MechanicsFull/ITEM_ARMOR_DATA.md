# Armor data interpretation

**Mechanic ID:** `ITEM.ARMOR_DATA`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Armor class, DR, slots, and protective effects.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 9243
- Representative evidence entries: 60
- Source/path coverage: 16

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4482 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Experience" & vbTab & "Index" & vbTab & "Weapon" & vbTab & "AC" & vbTab & "DR" & vbTab & "Follow" & vbTab & "MR" & vbTab & "HPs" & vbTab & "BS Defense" & vbTab & "Energy" & vbTab & "HP Regen" & vbTab & "Game Limit" & vbTab & "Active" & vbTab & "Type" & vbTab & "Alignment" & vbTab & "Gender" & vbTab & "Group" & vbTab & "Regen Time" & vbTab)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 50 — `-FIX: Armor comparison calculation for AC/DR +/- (it was rounding decimals)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 47 — `-FIX: Armor comparison calculation for AC/DR +/- (it was rounding decimals)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 23151 — `& vbCrLf & "Party physical damage per round = ((phys_damage * party_size) - (defense_DR * swings)) - [defenses from ac+accuracy+dodge]" _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3148 — `tBackStab = CalculateAttack(tBackStabProfile, a4_Surprise, nTemp, False, nSpeedAdj, nCalcDamageAC, nCalcDamageDR, nCalcDamageDodge, , , , , nBSdefense)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 5002 — `tBackStab = CalculateAttack(tCharacter, a4_Surprise, nTemp, False, nSpeedAdj, nVSAC, nVSDR, nVSDodge, , , , , nVSBSDefense)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 24 — `LOG_ONLY: Monster #174 - dark bishop: Item #1708 - runed sacrificial robes: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 9 — `-UP: Added monster game limit, bs defense, and lair exp fields for MME export`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 26 — `-NEW: Tools > Monsters > Item drop percentage modifier - alter monster drop percentages based on monster and item info.  this will allow you to do things like set the minimum drop percentage to 50% for items dropped by monsters that regen greater than 7 days, for example`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 47 — `-UP: Changed drop down spell editor's "Type of Resist" to what I believe they actually mean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 83 — `-BUGFIX: fixed container coin drop fieldmap locations`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 312 — `-NEW: chest cash drops fields on item editor (thx Locke Cole)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 313 — `-NEW: "BS Defense" field for monsters added (thx Tsunami)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 497 — `-UP: in-game AC and DR is calculated and shown on the item editor`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 83 — `Private m_nBetweenRoundResistType(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 84 — `Private m_nBetweenRoundResistDmgMR(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 177 — `Public Property Get nBetweenRoundResistDmgMR(ByVal Index As Integer) As Integer`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 63 — `1. **Module Loading** (`Module/`): Parses NE-format DLLs, MDF (module definition), MSG (messages), MCV (config values). Each module gets a full 4GB virtual address space.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 65 — `3. **Memory** (`Memory/`): `ProtectedModeMemoryCore` provides per-module 4GB address space. `FarPtr` represents segment:offset pointers. Memory is lazily allocated.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/cncchr_Tests.cs` line 22 — `public void cncchr_Test(string inputString, ushort nxtcmdStartingOffset, char expectedResult, ushort expectedNxtcmdOffset)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/cncchr_Tests.cs` line 43 — `expectedResultPointer.Offset += expectedNxtcmdOffset;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/cncsig_Tests.cs` line 23 — `public void cncsig_Test(string inputString, ushort nxtcmdStartingOffset, string expectedResult, ushort expectedNxtcmdOffset, string expectedNxtcmdRemaining)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/cncsig_Tests.cs` line 45 — `expectedResultPointer.Offset += expectedNxtcmdOffset;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/morcnc_Tests.cs` line 21 — `public void morcnc_Test(string inputString, ushort nxtcmdStartingOffset, char expectedResult, ushort expectedNxtcmdOffset)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/morcnc_Tests.cs` line 42 — `expectedResultPointer.Offset += expectedNxtcmdOffset;`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
