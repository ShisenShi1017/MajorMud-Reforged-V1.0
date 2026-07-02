# Validation0001 External Evidence Excerpts

## MBBSEmu README
- MBBSEmu describes itself as an emulator for MajorBBS and Worldgroup modules.
- It states it loads module DLLs like the original host and executes them inside an emulated 16-bit x86 environment.
- It states the host registers modules and invokes defined routines for events/status codes.

## MajorMUD disassembly README
- Module name: MajorMUD.
- File: WCCMMUD.DLL.
- Version: 1.11p for Worldgroup 2.0 (DOS).

## MBBSEmu cleanup scheduler evidence
- AppSettingsManager exposes `Cleanup.Time` and defaults unparsable/missing cleanup time to 03:00.
- MbbsHost creates a cleanup timer with a daily period: `TimeSpan.FromDays(1)`.
- MbbsHost worker loop calls `ProcessNightlyCleanup()` before channel processing.
- DoNightlyCleanup removes all sessions, calls module routine `mcurou`, calls module routine `finrou`, disposes modules, runs configured module cleanup EXEs, and restarts the host.

## MajorMUD disassembly search clues
- WCCMMUD.DLL.asm contains user-facing search strings including:
  - `Your search revealed nothing.`
  - `You may not search while attacking!`
  - `%s is searching the area.`

## Interpretation limits
- MBBSEmu validates host behavior and compatibility behavior, not necessarily every original-host edge case.
- WCCMMUD.DLL.asm is disassembly evidence, but individual routines still need mapping before promotion.
- No `/Data` changes are permitted from this pass.
