# Spell duration

**Mechanic ID:** `MAGIC.DURATION`  
**Bucket:** Magic  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
How spell durations are stored, scaled, and expired.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 637
- Representative evidence entries: 40
- Source/path coverage: 12

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 3070 — `' Ensure both the seconds timer and the rounds tracker reflect fallback`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1005 — `CalculateSpellCast.nOOM = CalcRoundsToOOM(CalculateSpellCast.nManaCost + tCharStats.nSpellOverhead, tCharStats.nMaxMana, tCharStats.nManaRegen, nCastChance, nSpellDuration)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1032 — `sAvgRound = sAvgRound & " for " & (nSpellDuration * SPELL_ROUND_SECS) & " secs/" & Fix((nSpellDuration * SPELL_ROUND_SECS) / ROUND_SECS) & " rounds (" & ((CalculateSpellCast.nAvgRoundDmg + CalculateSpellCast.nAvgRoundHeals) * nSpellDuration) & " total)"`
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
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 79 — `TMRINT {Initiating the timers, x to exit module...`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 80 — `} T Timer Intro`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 82 — `TMROUT {Timer has fired! %d:%d rti:%d`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 83 — `} T Timer`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 11 — `public class ProgrammableIntervalTimer_Tests : CpuTestBase`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 17 — `public void CantWriteToInvalidPorts(byte port)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 54 — `public void ReadLowByteOnly(byte channel)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 58 — `mbbsEmuCpuRegisters.AL = (byte)((1 << 4) | (channel << 6)); // lo byte only`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 60 — `var expected = (byte)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/BIOS/ProgrammableIntervalTimer_Tests.cs` line 64 — `instructions.@in(al, (byte)(0x40 + channel));`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
