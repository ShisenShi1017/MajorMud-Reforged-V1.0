# Validation0002 — MBBSEmu Host and MajorMUD Routine Mapping

## Objective
Map the public MBBSEmu host source and the MajorMUD disassembly into evidence categories before promoting behavior into the reconstruction.

## What was validated
- MBBSEmu host cleanup scheduler.
- MBBSEmu exported-module callback bridge.
- MBBSEmu tick/timer infrastructure.
- MajorMUD disassembly background routines and scheduler call clues.
- MajorMUD search/cleanup/respawn labels and strings as clue sources only.

## Findings
### Host cleanup mapping
MBBSEmu contains an explicit configurable nightly cleanup time (`Cleanup.Time`) and a host timer that sets cleanup once per day. The host waits through a cleanup grace period, warns users, removes sessions, calls each module's `mcurou` cleanup routine, calls each module's `finrou` finish-up routine, runs MDF cleanup programs, and restarts/reloads modules.

This strongly supports the earlier model that Worldgroup-style cleanup is host-driven and daily, but it does not by itself prove the exact original MajorMUD item cleanup flags.

### Timer/callback mapping
MBBSEmu uses a host tick timer controlled by `Timer.Hertz` with a default of 36. SDK/API calls are routed through exported module invocation by ordinal. The MajorMUD disassembly shows repeated use of `RTKICK` and `BEGIN_POLLING`, indicating module-level scheduled routines and user polling callbacks.

### MajorMUD background routines mapped
The disassembly exposes background routines registered during initialization, including:

- `_BACKGROUND_ENERGY`
- `_BACKGROUND_FAST`
- `_BACKGROUND_INACTIVITY`
- `_BACKGROUND_MEDIUM`
- `_BACKGROUND_MONSTER_CREATE`
- `_BACKGROUND_OTHER`
- `_BACKGROUND_SAVE_BUFFERS`
- `_BACKGROUND_SLOW`
- `_BACKGROUND_UPDATE_EXITS`

Notable scheduler evidence:
- `RTKICK` call references in disassembly: 44
- `BEGIN_POLLING` call references in disassembly: 12
- Far procedures in MajorMUD disassembly: 800
- Search clue hits in MajorMUD disassembly: 351

## Discoveries
- The host nightly cleanup sequence is now mapped to concrete MBBSEmu code.
- MajorMUD appears to register multiple background routines rather than relying on one monolithic loop.
- Monster creation has a named background routine: `_BACKGROUND_MONSTER_CREATE`.
- Energy/combat/inactivity have separate background routines, suggesting multiple timer cadences or scheduled subsystems.

## Statistics
- MBBSEmu C# files scanned: 215
- MajorMUD disassembly lines scanned: 328627
- MajorMUD far procedures: 800
- MajorMUD RTKICK references: 44
- MajorMUD BEGIN_POLLING references: 12
- Background routine names found: 9

## Changes
- Added external-evidence mapping report.
- Added machine-readable Validation0002 evidence JSON.
- Updated EngineKnowledge with MBBSEmu validation notes.
- No `/Data` changes.

## Promotions
No gameplay behavior was promoted into `/Data`. Host cleanup scheduling is marked as **verified against MBBSEmu host source**, not yet promoted as original MajorMUD module semantics.

## Remaining unknowns
- Exact MajorMUD item cleanup flag interpretation.
- Exact `_BACKGROUND_MONSTER_CREATE` cadence and death-timer interaction.
- Exact search stat formula.
- Whether MajorMUD cleanup deletes/hides items inside `mcurou`, a cleanup executable, or both.

## Next pass
Validation0003 should inspect the MajorMUD disassembly around `_BACKGROUND_MONSTER_CREATE`, `_BACKGROUND_FAST`, `_BACKGROUND_MEDIUM`, `_BACKGROUND_SLOW`, `RTKICK`, and any cleanup/initialize routines to connect scheduler calls to monster respawn and object lifecycle.
