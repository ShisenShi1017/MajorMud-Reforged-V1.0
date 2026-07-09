# Area/targeting rules

**Mechanic ID:** `MAGIC.AOE_TARGETING`  
**Bucket:** Magic  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
How spell targets, groups, rooms, and area effects are selected.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 1357
- Representative evidence entries: 38
- Source/path coverage: 16

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 38 — `---This very useful tool allows you to change ranges of specific fields in the room database and is  more configurable for rooms than the universal modifier. Very useful for fixing area regen problems, or changing the room description of an entire area. Could also be used to remove large ranges from rooms, such as setting every room's light setting to 0.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3202 — `tabRooms.Fields("Max Area") = Roomrec.MaxArea`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2006 — `Roomrec.MaxArea = tabRooms.Fields("Max Area")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMap.frm` line 21135 — `tabRooms.Fields("Max Area") = Roomrec.MaxArea`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 825 — `& " (" & ClipNull(tabRooms.Fields("Name")) & "): " & GetMonGroupName(ChangeList(x, 1)) & "/" & tabRooms.Fields("Min Index") & "-" & tabRooms.Fields("Max Index") _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 995 — `lstRecordList.AddItem GetMonGroupName(nLastGroup) & "/" & nLastMin & "-" & nLastMax & ": " & sRooms`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 997 — `lstRecordList.AddItem GetMonGroupName(nLastGroup) & "/" & nLastMin & ": " & sRooms`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 1024 — `lstRecordList.AddItem GetMonGroupName(nLastGroup) & "/" & nLastMin & "-" & nLastMax & ": " & sRooms`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 1026 — `lstRecordList.AddItem GetMonGroupName(nLastGroup) & "/" & nLastMin & ": " & sRooms`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 7 — `MBBSEmu is a cross-platform emulator for running legacy MajorBBS and Worldgroup modules (16-bit NE format DLLs) on modern systems. It emulates both the Galacticomm host environment and an x86-16 processor. Written in C# targeting .NET 10.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 22 — `-FIX: utilizing the "reset user intractable" fields on database record room exports will no longer remove hidden sign items.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 202 — `-NEW: map now marks room spells/shops/exit & death rooms`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 222 — `-UP: resized monster/room/shop editors to fit better on 800x600 resolutions`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 67 — `5. **Exported Modules** (`HostProcess/ExportedModules/`): C# implementations of MajorBBS/Worldgroup APIs that modules call via imported ordinals. Each mapped to a virtual segment:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 76 — `- **Logging** (`Logging/`): `LogFactory` manages `MessageLogger`, `AuditLogger`, `CPULogger` with pluggable targets.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFileProcessor.cs` line 443 — `private BtrieveRecord GetAndCacheBtrieveRecord(uint id, SqliteDataReader reader, int ordinal)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 6 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 12 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 18 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/Enums/EnumBtrieveOperationCodes.cs` line 24 — `[System.AttributeUsage(System.AttributeTargets.Field)]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 7 — `This is the main MBBSEmu project — a cross-platform emulator for 16-bit MajorBBS/Worldgroup modules. It loads legacy NE-format DLLs, emulates an x86-16 CPU, and implements the Galacticomm SDK APIs in C# so that decades-old BBS modules can run on modern systems.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 204 — `- `OpKind.NearBranch16` — direct branch target offset`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 281 — `2. Read the target far pointer from memory at `[MemorySegment:offset]``
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 3302 — `//Far call with target offset at memory location`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 65 — ``Recordset` objects opened once at load: `tabItems`, `tabMonsters`, `tabSpells`, `tabRooms`,`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
