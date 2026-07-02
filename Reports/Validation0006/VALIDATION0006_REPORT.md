# Validation0006 Report — Routine-Level Promotion Review

## Objective
Drill into the implementation evidence behind cleanup, respawn, search, and quest handling using MBBSEmu host source and the MajorMUD disassembly, then separate promotion-ready behavior from unresolved hypotheses.

## Sources Used
- Validation0005 cumulative baseline.
- MBBSEmu host source.
- MajorMUD disassembly: `WCCMMUD.DLL.asm`.
- MBBSDASM and MDB extractor remain available as tools/evidence support.

## Key Findings

### Host nightly cleanup is engine-documentation verified
MBBSEmu implements a scheduled nightly cleanup using cleanup time, warning timers, and a cleanup grace period. The cleanup path removes active sessions, then invokes module `mcurou` and `finrou` routines. This validates that nightly cleanup is a host lifecycle service, not merely a simulator assumption.

### RTKICK is engine-documentation verified
MBBSEmu models RTKICK as a delayed one-shot real-time routine. The routine begins counting when created, fires after its delay in seconds, then is marked executed and removed. MajorMUD contains many RTKICK references, which supports the model that the module uses host-delayed scheduling.

### BEGIN_POLLING is engine-documentation verified
MBBSEmu models BEGIN_POLLING as persistent per-session polling that continues until stopped. MajorMUD contains BEGIN_POLLING references.

### Search subsystem is documentation-level verified
The MajorMUD disassembly contains `_CMD_SEARCH`, `_CLEAR_SEARCH_FLAGS`, and many hidden/hide references. This validates the existence of a search/hidden-discovery subsystem, but not the exact probability formula.

### Monster respawn remains not promoted
The host provides scheduling primitives and the MajorMUD disassembly contains reset/monster references, but the exact MajorMUD monster respawn routine and data fields still need direct mapping. The user-provided model — normal creatures around 15 minutes, explicit timers start on death and continue through reset — remains a documented user-knowledge hypothesis until tied to code/data fields.

### Quest persistence remains not promoted
Quest, save/load, and Btrieve evidence is present, but field-level quest state transitions are not mapped yet.

## Statistics
- MajorMUD far procedure hits: 800
- MajorMUD total proc-token hits: 802
- MajorMUD `search` hits: 355
- MajorMUD `hidden` hits: 130
- MajorMUD `cleanup` hits: 111
- MajorMUD `quest` hits: 144
- MajorMUD `monster` hits: 3261
- MajorMUD `RTKICK` hits: 54
- MajorMUD `BEGIN_POLLING` hits: 24
- MBBSEmu cleanup hits in mapped host files: 84
- MBBSEmu RTKICK hits in mapped host files: 19

## Promotions Approved
Engine/documentation promotions approved: 4

1. Host nightly cleanup lifecycle.
2. RTKICK delayed one-shot scheduling primitive.
3. BEGIN_POLLING persistent polling primitive.
4. Search/hidden-discovery subsystem existence.

## Data Promotions Approved
None.

## Remaining Unknowns
- Exact item cleanup flags and hide/delete mapping.
- Exact search formula.
- Exact monster respawn routine and timing field mapping.
- Quest variable storage and transition logic.
- Boss/unique lifecycle semantics.

## Next Validation Pass
Validation0007 should perform routine-level disassembly mapping for monster reset/respawn and item cleanup, especially functions near `_RESET_ROOM`, `_MCUROU`, `_CMD_SEARCH`, and RTKICK call sites.
