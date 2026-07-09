# Special room actions

**Mechanic ID:** `WORLD.SPECIAL_ROOMS`  
**Bucket:** World and Spawns  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Room scripts, special actions, gates, traps, teleports, and puzzles.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2868
- Representative evidence entries: 39
- Source/path coverage: 15

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 890 — `1436|0|0|flag check, remote action`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 38 — `---This very useful tool allows you to change ranges of specific fields in the room database and is  more configurable for rooms than the universal modifier. Very useful for fixing area regen problems, or changing the room description of an entire area. Could also be used to remove large ranges from rooms, such as setting every room's light setting to 0.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3701 — `tabActions.Fields("Single to Room") = Actionrec.SingleToRoom`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3704 — `tabActions.Fields("User to Room") = Actionrec.UserToRoom`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3706 — `tabActions.Fields("Monster to Room") = Actionrec.MonsterToRoom`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3708 — `tabActions.Fields("Inventory to Room") = Actionrec.InventoryToRoom`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3710 — `tabActions.Fields("Floor Item to Room") = Actionrec.FloorItemToRoom`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2284 — `Actionrec.SingleToRoom = Trim(tabActions.Fields("Single to Room"))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2287 — `Actionrec.UserToRoom = Trim(tabActions.Fields("User to Room"))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2289 — `Actionrec.MonsterToRoom = Trim(tabActions.Fields("Monster to Room"))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2291 — `Actionrec.InventoryToRoom = Trim(tabActions.Fields("Inventory to Room"))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2293 — `Actionrec.FloorItemToRoom = Trim(tabActions.Fields("Floor Item to Room"))`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/EnumFlags.cs` line 39 — `///     Trap Flag`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/ICpuRegisters.cs` line 28 — `///     While the majority of these are basic 16-bit values, several special`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/create_user.py` line 14 — `parser.add_argument('--keys', help='Account keys to add to the new account', action='append', default=['NORMAL','PAYING'])`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 20339 — `If Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 20368 — `If TextBlockHasTeleport(tabRooms.Fields("CMD"), nMapStartRoom, nMapStartMap, bStrict) Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22958 — `If Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 26611 — `MsgBox "Note: room command (teleport) not recorded to MegaMUD path because current path position does not match.", vbInformation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 33184 — `ElseIf Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 33483 — `If Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 41017 — `If Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMap.frm` line 43872 — `If Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMap.frm` line 44788 — `If Left(tabRooms.Fields(sLook), 6) = "Action" Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMap.frm` line 45746 — `ElseIf Left(tabRooms.Fields(sLook), 6) = "Action" Then`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
