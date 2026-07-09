# Wear/use restrictions

**Mechanic ID:** `ITEM.WEAR_RESTRICTIONS`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Race/class/alignment/level/slot restrictions for using items.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2344
- Representative evidence entries: 40
- Source/path coverage: 18

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 272 — `-NEW: New option to not follow restricted exits (class, race, and level) when drawing maps`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/c-users-matthew-snead-github-megamud2-m-sharded-engelbart-HANDOFF.md` line 70 — `6. Confirm restricted exits (class/race/level) are now always followed on both forms.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/c-users-matthew-snead-github-megamud2-m-sharded-engelbart.md` line 144 — `6. Confirm restricted exits (class/race/level, types 13–15) are now always followed on both forms.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 178 — `-NEW: New option to not follow restricted exits (class, race, and level) when drawing maps`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 260 — `-NEW: New option to not follow restricted exits (class, race, and level) when drawing maps`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmExpCalc.frm` line 209 — `Public Sub CalcBy(nClass As Long, nRace As Long, nLevel As Long)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMapLegend.frm` line 242 — `Caption         =   "Class / Race / Level / Alignment / Ability"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmRecordChange.frm` line 2511 — `Case 1, 13, 14, 15: 'spell/class/race/level`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmRecordChange.frm` line 2516 — `& " -- Exit " & GetFriendlyRoomExit(x, False) & " (spell/class/race/level): " & tabRooms.Fields("Para3 " & x) & " to " & (ChangeList(3, y) + (tabRooms.Fields("Para3 " & x) - ChangeList(1, y)))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 7359 — `Call frmExpCalc.CalcBy(cmbClasses.ListIndex, cmbRaces.ListIndex, Val(txtLevel.Text))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 7397 — `frmUserPaste.Caption = "Paste Character Class/Race/Level/Exp"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 8140 — `& vbCrLf & vbCrLf & "Paste: Paste a capture of a character's ""stat"" output.  Class, Race, Level, Exp, " _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9383 — `'    nExp = CalcExpNeeded(CLng(Val(txtLevel.Text)), CInt(nClassExp + nRaceExp))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 24339 — `nExp = CalcExpNeededByRaceClass(val(txtGlobalLevel(0).Text), _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 26990 — `Dim nCombatLevel As Integer, sGlobalCharAccyFromAbils As String, bClassStealth As Boolean, bRaceStealth As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35135 — `If nRace > 0 And nClass > 0 And nLevel > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35136 — `sClipBoardText = sClipBoardText & "Experience:" & (CalcExpNeededByRaceClass(nLevel, nClass, nRace) + 1) & vbCrLf`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 38268 — `Dim nLevelReq As Long, nBaseCP As Long, nRace As Long, nClass As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 38327 — `lblStatCalc.Caption = lblStatCalc.Caption & vbCrLf & "EXP Req: " & PutCommas(CalcExpNeededByRaceClass(nLevelReq, nClass, nRace), True)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 40517 — `'class, level, and alignment ok ... now check armour/weapon restrictions`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 761 — `4327|0|0|tele for solo trainer, class filtered`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 84 — `-BUGFIX: fixed item race restriction fieldmap locations`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 280 — `-FIX: class/race editors were reloading the record lists on close`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 515 — `-FIX: database exporter/importer wasn’t exporting/importing a spell’s level restriction field`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Logging/MessageLogger.cs` line 52 — `target.Write(logLevel, exception.StackTrace);`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
