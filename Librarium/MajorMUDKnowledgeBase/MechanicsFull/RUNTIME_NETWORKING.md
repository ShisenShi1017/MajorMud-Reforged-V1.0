# Network/session model

**Mechanic ID:** `RUNTIME.NETWORKING`  
**Bucket:** Runtime Support  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Telnet/web/client session expectations and server communications.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 3280
- Representative evidence entries: 26
- Source/path coverage: 8

## Representative Evidence

- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Server/Socket/SocketServer.cs` line 32 — `public SocketServer(LogFactory logger, IMbbsHost host, AppSettingsManager configuration, ITextVariableService textVariableService, PointerDictionary<SessionBase> channelDictionary)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SocketSession.cs` line 161 — `ProcessIncomingClientData(bytesReceived);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SocketSession.cs` line 168 — `///     Allows a derived class to parse and alter incoming client data, such as stripping`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SocketSession.cs` line 171 — `/// <returns>A tuple. The first result is the byte array to be entered into the client`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SocketSession.cs` line 175 — `protected abstract (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SocketSession.cs` line 177 — `private void ProcessIncomingClientData(int bytesReceived)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SocketSession.cs` line 183 — `var (dataFromClient, length) = ProcessIncomingClientData(_socketReceiveBuffer, bytesReceived);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 216 — `- **SocketSession** (abstract) — Base for socket sessions. Async receive via `BeginReceive`/`EndReceive`, dedicated sender thread processing `DataToClient` queue, optional heartbeat`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/HostRoutines/FsdRoutines.cs` line 117 — `session.SendToClient(new string(' ', field.FieldLength));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/HostRoutines/FsdRoutines.cs` line 119 — `session.SendToClient(field.Value ?? string.Empty);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/HostRoutines/FsdRoutines.cs` line 139 — `session.SendToClient(field.FieldAnsi);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/HostRoutines/FsdRoutines.cs` line 141 — `session.SendToClient(new string(' ', field.FieldLength));`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 101 — `AddStrip(array('Also here:', 'You hear movement ', ' into the room from ', ' just entered the Realm', ' just left the Realm', ' just hung up', ' peeks in from ', 'the server will be shutting', 'nightly "auto-cleanup"', 'Please finish up and log off', 'Software caused connection abort', 'Session ended', 'or type "new"', 'Enter your password', 'password you have given', 'glad to see you back', 'majo`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 608 — `' WndProc of MDIClient window`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 24 — `Begin VB.Timer CursorTimer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 50 — `Attribute VB_Creatable = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 57 — `Private Sub CursorTimer_Timer()`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 103 — `'frmPreview.CursorTimer.Enabled = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 116 — `'frmPreview.CursorTimer.Enabled = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 133 — `'  frmPreview.CursorTimer.Enabled = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 196 — `'        term_process_char (Asc(Mid(CH, i, 1)))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 201 — `'frmPreview.CursorTimer.Enabled = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 208 — `'frmPreview.CursorTimer.Enabled = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 225 — `'                term_process_char (Asc(Mid(CH, i, 1)))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmPreview.frm` line 231 — `'frmPreview.CursorTimer.Enabled = True`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
