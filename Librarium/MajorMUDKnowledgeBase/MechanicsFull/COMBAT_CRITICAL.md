# Critical hit logic

**Mechanic ID:** `COMBAT.CRITICAL`  
**Bucket:** Combat  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Critical chance, critical damage, and any special conditions that alter critical outcomes.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 127
- Representative evidence entries: 36
- Source/path coverage: 19

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1888 — `'         { Critical hit }`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27917 — `'| CRITICAL HIT CALCULATION...`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmSwingCalc.frm` line 2048 — `'         { Critical hit }`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 2041 — `' { Calculates the critical hit chance bonus for being quick and deadly with a`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 187 — `-NEW: Critical Hit Chance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 93 — `-NEW: Critical Hit Chance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 175 — `-NEW: Critical Hit Chance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 4533 — `' { Calculates the critical hit chance bonus for being quick and deadly with a`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 528 — `-UP: mass room editor now logs errors (and only the critical errors) to a log file`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 559 — `-UP: mass room editor now logs errors (and only the critical errors) to a log file`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5693 — `MsgBox "Error regetting room " & nTempMap & "/" & nTempRoom, vbCritical`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 159 — `Caption         =   "Crit AVG"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 169 — `Caption         =   "Crit %"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1573 — `If x = 0 Then GoTo Crit:`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1577 — `If nExtraP = 0 Then GoTo Crit:`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1580 — `If x = 0 Then GoTo Crit:`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1583 — `nExtraA = Val(Mid(sClipText, x, InStr(x, sClipText, "Crit") - x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1584 — `If nExtraA = 0 Then GoTo Crit:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 51 — `The CPU is the innermost loop of the emulator. Understanding it is critical.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 287 — `Performance-critical utility methods with aggressive inlining: `ByteExtensions` (bit operations, parity), `UshortExtensions`/`UintExtensions` (sign extension, bit tests), `ReadOnlySpanExtensions`, `ANSIStringExtensions`, `EnumSessionStateExtensions`.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 126 — `**Why a giant switch and not a dictionary/delegate table:** The large `switch` on `Mnemonic` is intentional. When the .NET JIT compiles this to native code, it emits an optimized jump table — a single indexed jump instruction rather than a chain of comparisons or dictionary lookups. This makes dispatch O(1) with minimal overhead (one bounds check + indirect jump), which matters critically here sin`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 293 — ``Op_Retf()` pops IP then CS from the stack. The critical line: `Registers.Halt |= Registers.CS == 0xFFFF`. When the sentinel value pushed during `Reset()` is reached (CS=0xFFFF), execution stops. `Op_Ret()` (near return) pops only IP but also checks the halt condition. Both handle optional `RETF imm16` by advancing SP.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/DOS/Structs/PSPStruct.cs` line 55 — `///     Critical Error Handler Address (See Int 24h)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExecutionUnits/CLAUDE.md` line 3 — `This directory contains `ExecutionUnit.cs`, a single but critical class that encapsulates everything needed to run a chunk of x86 code within a module: a CPU core, registers, memory reference, exported module dictionary, and interrupt handlers. ExecutionUnits are the mechanism that enables nested subroutine calls without corrupting CPU state.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExecutionUnits/CLAUDE.md` line 58 — `// Critical: re-associate this EU's registers with the exported module,`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
