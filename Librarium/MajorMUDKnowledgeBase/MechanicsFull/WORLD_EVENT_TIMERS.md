# World event timers

**Mechanic ID:** `WORLD.EVENT_TIMERS`  
**Bucket:** World and Spawns  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Scheduled events, room/NPC timers, and recurring world actions.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 576
- Representative evidence entries: 36
- Source/path coverage: 10

## Representative Evidence

- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 201 — `_tickTimer = new Timer(_ => _timerEvent.Set(), this, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / configuration.TimerHertz));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 60 — `var expected = (byte)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 88 — `var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 116 — `var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 145 — `var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 92 — `return (byte)TickToUShort();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 95 — `return (byte)(TickToUShort() >> 8);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 236 — `Properties: `BTURNO` (BBS registration number), `BTURS` (buffer size), `TICKER` (seconds counter, updated by a 1-second timer).`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 125 — `///     Timer that sets _timerEvent on a set interval, controlled by Timer.Hertz`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 127 — `private readonly Timer _tickTimer;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 128 — `private readonly EventWaitHandle _timerEvent;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 200 — `_timerEvent = new AutoResetEvent(true);`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 4552 — `SEC_PER_REGEN_TICK, SEC_PER_MEDI_TICK, bDebugExpPerHour, "respawn")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 12 — `Begin VB.Timer tmrRefresh`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 17 — `Begin VB.Timer FlashTimer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 22 — `Begin VB.Timer tmrMouse`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 365 — `Private Sub FlashTimer_Timer()`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 389 — `'Stop the Timer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 390 — `FlashTimer.Enabled = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 581 — `'Start the Timer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 582 — `FlashTimer.Enabled = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 593 — `Private Sub tmrMouse_Timer()`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 595 — `' this timer is used to take away the focus rect, for bars`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 734 — `If InDesign = True Then: Stop: Else: Call HandleError("Class " & TypeName(Me) & "_tmrMouse_Timer" & vbCrLf & "Line# " & Erl & vbCrLf & "Err#" & Err.Number & vbCrLf & "Desc. " & Err.Description, App.Title, "")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 739 — `Private Sub tmrRefresh_Timer()`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
