# Spawn timers

**Mechanic ID:** `WORLD.SPAWN_TIMERS`  
**Bucket:** World and Spawns  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Spawn interval, hard timers, reset behavior, and timer visibility.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 883
- Representative evidence entries: 36
- Source/path coverage: 8

## Representative Evidence

- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 11 — `public class ProgrammableIntervalTimer_Tests : CpuTestBase`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 88 — `var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 116 — `var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 145 — `var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/CpuTestBase.cs` line 18 — `private protected readonly ProgrammableIntervalTimer pit;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/CpuTestBase.cs` line 28 — `pit = new ProgrammableIntervalTimer(logger: null, fakeClock, mbbsEmuCpuCore);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 11 — `public class ProgrammableIntervalTimer : IIOPort, IStoppable, IDisposable`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 46 — `private Timer _timer;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 48 — `public ProgrammableIntervalTimer(IMessageLogger logger, IClock clock, ICpuCore cpu)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 57 — `_timer?.Dispose();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 58 — `_timer = null;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/BIOS/ProgrammableIntervalTimer.cs` line 63 — `_timer?.Dispose();`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 273 — `-UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 179 — `-UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 261 — `-UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 124 — `roamSecs        As Double   'Seconds waiting for respawns (per-cycle, rarely used)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 465 — `'   -> No movement or recovery; cycle time = respawn time.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 581 — `'   - Instant respawn (stand-in-place): no movement.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 1487 — `' Lairs respawn on the wall-clock minute, so the real wait is the stated`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2179 — `'       Spawn-gating waits when the loop outruns respawns`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2211 — `'       - Scale for respawn throughput: compute effectiveLairs & roomsScaled;`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2239 — `'       - If timePerClear < spawnInterval, wait fillerSec to the gate; split that`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2340 — `Dim spawnInterval       As Double`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 3262 — `spawnInterval = 0`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 12 — `Begin VB.Timer tmrRefresh`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
