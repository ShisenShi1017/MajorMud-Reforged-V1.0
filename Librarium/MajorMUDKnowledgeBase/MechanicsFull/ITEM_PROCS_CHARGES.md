# Item procs and charges

**Mechanic ID:** `ITEM.PROCS_CHARGES`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Triggered item effects, consumables, charges, and spell-cast items.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 6651
- Representative evidence entries: 60
- Source/path coverage: 19

## Representative Evidence

- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 9 — `| `Majorbbs.cs` | ~8,400 | Core BBS API: user management, I/O, string/memory ops, file I/O, config, date/time, Btrieve, module registration |`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galgsbl.cs` line 275 — `///     Set the input byte trigger quantity (used in conjunction with btuict())`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/GlobalRoutines/SysopGlobal.cs` line 181 — `_sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"BROADCAST <MESSAGE>",-30} Broadcasts message to all users online".EncodeToANSIString());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/GlobalRoutines/SysopGlobal.cs` line 437 — `///     Sysop Command to broadcast a message to all users`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 59 — `'Becouse I have no idea how the buld process works and I could not stop it moving the f$%king buttons`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 106 — `Declare Function CallWindowProc Lib "user32" Alias "CallWindowProcA" (ByVal lpPrevWndFunc As Long, ByVal hWnd As Long, ByVal Msg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 257 — `Public Declare Function DefWindowProc Lib "user32" Alias "DefWindowProcA" (ByVal hWnd As Long, ByVal wMsg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 55 — `--This editor is used to organize text blocks pertaining to quests in the game. Normally, to find these blocks one would have to search through the textblock database and find it by hand. This time consuming process has been greatly reduced by the quest organizer by listing common quest text blocks in a tree structure. Making changes to the quest text blocks in the game used to take hours, but now`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4909 — `ts.Write (Userrec.MaxMana & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4910 — `ts.Write (Userrec.CurrentMana & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4911 — `ts.Write (Userrec.SpellCasting & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4923 — `ts.Write (Userrec.BroadcastChan & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4948 — `ts.Write (Userrec.SpellCasted(x) & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1446 — `& "uses and is not set to destroy after the uses expire, the recharge ability value " _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1447 — `& "will be verified against the max uses.  If an item in this case has no recharge ability, " _`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 66 — `4. **Host Process** (`HostProcess/`): `MbbsHost` manages module lifecycle, session scheduling, and execution units. `ExecutionUnit` wraps CPU contexts for concurrent module operations.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 69 — `6. **Sessions** (`Session/`): `SessionBase` subclasses handle different connection types (Telnet, Rlogin, local console). Sessions are assigned channel numbers and manage user I/O buffers.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 59 — `var resourceManager = ResourceManager.GetTestResourceManager();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 63 — `File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 568 — `.Where(ex => ex.SqliteExtendedErrorCode == BtrieveFileProcessor.SQLITE_CONSTRAINT_TRIGGER);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1083 — `private static byte[] CreateRecord(string username)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1085 — `var usernameBytes = Encoding.ASCII.GetBytes(username);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1090 — `Array.Copy(usernameBytes, 0, record, 2, usernameBytes.Length);`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 35 — `-FIX: Calculate weight difference button on EQ tab used in conjunction with carried items on item manager`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
