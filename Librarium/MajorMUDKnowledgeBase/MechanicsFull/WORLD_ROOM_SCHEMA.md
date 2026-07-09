# Room field schema

**Mechanic ID:** `WORLD.ROOM_SCHEMA`  
**Bucket:** World and Spawns  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Room data layout, exits, flags, text, and environmental attributes.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 22022
- Representative evidence entries: 60
- Source/path coverage: 6

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5588 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5591 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5604 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Item: " & Roomrec.Para1(x) & ")"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5607 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Toll: " & Roomrec.Para1(x) & ")"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5610 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5633 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & sTemp`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5662 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Trap, " & Roomrec.Para1(x) & " damage)"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5665 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Text: " & GetMessages(Roomrec.Para1(x), -1) & ")"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5727 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Class: " & Roomrec.Para1(x) & " OK, " & Roomrec.Para2(x) & " NO)"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5730 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Race: " & Roomrec.Para1(x) & " OK, " & Roomrec.Para2(x) & " NO)"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5733 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Level: " & Roomrec.Para1(x) & " to " & Roomrec.Para2(x) & ")"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5736 — `tabRooms.Fields(sDir) = Roomrec.MapNumber & "/" & Roomrec.RoomExit(x) & " (Timed: " & Roomrec.Para2(x) & "*5 minutes)"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/c-users-matthew-snead-github-megamud2-m-sharded-engelbart.md` line 71 — `- Add `Case 14` to both `MapDrawOnRoom` subs: a box **outline** (`B` flag) drawn in the gap,`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 20344 — `RoomExit = ExtractMapRoom(tabRooms.Fields(sLook))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22853 — `If InStr(1, LCase(tabRooms.Fields("Name")), LCase(sMapSearch)) > 0 Then Exit Do`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22962 — `RoomExit = ExtractMapRoom(tabRooms.Fields(sLook))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22989 — `If Not RoomExit.Map = tabRooms.Fields("Map Number") Then nExitType = 8 'map change`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31882 — `tRoomExits = ExtractMapRoom(lvMonsterCompareLoc.SelectedItem.ListSubItems(1).Tag)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31914 — `tRoomExits = ExtractMapRoom(lvMonsterDetail.ListItems(x).ListSubItems(1).Tag)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31923 — `tRoomExits = ExtractMapRoom(lvMonsterDetail.ListItems(x).ListSubItems(1).Tag)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31955 — `tRoomExits = ExtractMapRoom(lvMonsterDetail.SelectedItem.ListSubItems(1).Tag)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 33102 — `RoomExit = ExtractMapRoom(tabRooms.Fields(sLook))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 33185 — `RoomExit2 = ExtractMapRoom(tabRooms.Fields(sLook))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 33261 — `RoomExits = ExtractMapRoom(tabRooms.Fields(sDirection))`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/ADC_Tests.cs` line 12 — `public void ADC_AX_IMM16_CarryFlagSet(ushort adcValue, ushort axStartingValue, ushort axExpectedValue)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
