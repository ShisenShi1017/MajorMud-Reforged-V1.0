# Validation0003 — Cleanup/Search/Respawn Cross-Validation Report

## Objective
Complete the previously interrupted Validation0003 pass by cross-validating MajorMUD cleanup, search, respawn/reset, and timer evidence against uploaded external sources.

## Sources Used
- Current project baseline: MajorMUD-Reforged-Validation0003-Cumulative corrected package.
- MBBSEmu source ZIP.
- MBBSEmu.Disassemblies ZIP, especially MajorMUD/WCCMMUD.DLL.asm.
- MBBSDASM source ZIP for disassembly provenance/toolchain context.
- EditorPatch0003 ZIP extractor only: `Tools/MDBExtractor/offline_mdb_text_extractor.py`.

## Confirmed Evidence Found

### Host scheduler evidence
MBBSEmu maps MajorBBS/Worldgroup host scheduling primitives including `BEGIN_POLLING` and `RTKICK`. This validates that MajorMUD modules can use host-provided polling and delayed real-time callbacks.

### MajorMUD background scheduler evidence
The MajorMUD disassembly shows `INIT__WCCMMUD` registering multiple background routines, including:
- `_BACKGROUND_FAST`
- `_BACKGROUND_MONSTER_CREATE`
- `_BACKGROUND_OTHER`
- `_BACKGROUND_SLOW`
- `_BACKGROUND_MEDIUM`
- `_BACKGROUND_ENERGY`
- `_BACKGROUND_INACTIVITY`

These registrations use `RTKICK` or `_MY_RTKICK`, supporting the existence of independent background subsystems rather than a single monolithic loop.

### Cleanup evidence
The disassembly contains a cleanup routine that emits `Starting Cleanup` and logs `Entering cleanup`. It also contains item/attribute text entries for:
- `Delete at cleanup`
- `Recharge at cleanup`
- `Delete from inventory at cleanup`
- `Remain Visible at cleanup`

This is stronger than the previous simulator-only hypothesis. Cleanup is now validated as a real MajorMUD concept in the disassembly, but exact flag offsets and complete control flow still require additional mapping before `/Data` promotion.

### Search evidence
The disassembly includes `_CMD_SEARCH`, messages for searching the area, prohibition while attacking, and a call path to `_SEARCH_FOR_HIDDEN_EXITS`. This confirms Search is implemented as an explicit command with hidden-exit behavior. Search-to-hidden-item behavior remains plausible but still needs routine-level mapping.

### Respawn/reset evidence
The disassembly contains room/area reset messages: `Room reset - monsters will start generating appropriately` and `Area Reset - monsters will start generating appropriately`. This validates a real reset/generation relationship for monsters. Exact global interval, death-start timer behavior, and boss/unique handling remain not fully promoted.

## Promotion Review

Promote to EngineKnowledge as externally validated evidence:
- MajorMUD uses RTKICK/MY_RTKICK background routines.
- Cleanup is a real MajorMUD routine/concept.
- Cleanup-related item attributes exist in the disassembly.
- Search command exists and calls a hidden-exit search routine.
- Room/area reset can restart monster generation.

Do not promote to `/Data` yet:
- Exact item cleanup flag offsets.
- Exact hidden-item search formula.
- Exact 15-minute standard monster interval.
- Exact per-monster death timer queue behavior.
- Exact boss/quest monster persistence rules.

## Status
Validation0003 is now complete.
