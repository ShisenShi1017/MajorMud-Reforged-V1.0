# Validation0009 — Monster Respawn Scheduler Targeted Rerun

## Objective

Re-execute Validation0009 properly from the trusted Validation0008 baseline. This pass focuses on monster respawn scheduling, background creation, polling interaction, room occupancy, and cleanup separation.

The earlier quick Validation0009/Validation0010 summaries are treated as discarded and are not used as evidence.

## Sources Used

- Current baseline: MajorMUD-Reforged-Validation0008-Cumulative.zip
- MajorMUD disassembly: MBBSEmu.Disassemblies / MajorMUD / WCCMMUD.DLL.asm
- MBBSEmu source: HostProcess/MbbsHost.cs, Module/RealTimeRoutine.cs, ExportedModules/Ordinals.cs
- Existing reconstructed /Data from Validation0008 baseline

## Procedure-Level Findings

### Background monster creation

`_BACKGROUND_MONSTER_CREATE` is a dedicated MajorMUD background routine. It calls `sub_26DF7`, then re-registers itself using `_MY_RTKICK` with `word_998E8` as the delay value. This supports a recurring background monster creation loop implemented as repeated one-shot RTKICK scheduling.

Promotion: **engine documentation approved**.

### RTKICK semantics

MBBSEmu documents and implements RTKICK routines as delayed callbacks that execute once and are then discarded. The MajorMUD disassembly's `_MY_RTKICK` wrapper inserts callbacks into an ordered scheduled-list structure using the current tick counter plus the requested delay. This validates that repeated background behavior must be re-registered after each execution.

Promotion: **engine documentation already validated; reaffirmed**.

### Fast/medium/slow monster update pipelines

MajorMUD contains `_SLOW_UPDATE_MONSTERS`, `_MEDIUM_UPDATE_MONSTERS`, and `_FAST_UPDATE_MONSTERS`, each routing to corresponding next-monster and per-monster update routines. `_BACKGROUND_SLOW`, `_BACKGROUND_MEDIUM`, and `_BACKGROUND_FAST` invoke these systems. The user polling routine also calls `_SLOW_UPDATE_NEXT_MONSTER`, `_FAST_UPDATE_NEXT_MONSTER`, and `_MEDIUM_UPDATE_NEXT_MONSTER` when the relevant flag condition is active.

Promotion: **engine documentation approved**.

### Room occupancy gating

`_GENERATE_MONSTER` checks room monster slots at offsets `400h/402h` and loops up to `0Fh` slots. If no usable slot is available, generation exits. This validates a room-occupancy/slot-availability gate for monster generation.

Promotion: **engine documentation approved**.

### Explicit timer evidence

`_GENERATE_MONSTER` checks fields at offsets including `0A1h`, `0A3h`, `0ADh`, `0AEh`, and `0B0h`. It calls `COFDAT`, `NOW`, `TODAY`, and `_CALC_MINUTES_DIFFERENCE`, then multiplies byte `0ADh` by `60` and applies a random adjustment using `LNGRND`. This is strong evidence of an elapsed-time gate for a category of monster generation.

Promotion: **partial only**. The architecture is documented, but exact field names and `/Data` mappings are not promoted.

## Statistics

- MajorMUD procedures detected: 802
- `_GENERATE_MONSTER` call sites: 17
- `_CALC_MINUTES_DIFFERENCE` call sites: 1
- RTKICK / `_MY_RTKICK` references: 54
- Background monster create references: 8
- BEGIN_POLLING references: 24

## Promotions

Approved for engine documentation:

1. Background monster creation is RTKICK-scheduled and re-registers itself.
2. Monster update processing is split into fast, medium, and slow pipelines.
3. Room monster slot occupancy gates monster generation.

Approved for `/Data`:

- None.

## Remaining Blockers

- Exact normal global respawn interval, including whether it is exactly 15 minutes.
- Exact mapping of explicit monster timer fields into reconstructed `/Data`.
- Exact interaction between daily cleanup and individual monster timer state.
- Boss/unique/quest monster respawn rules.
- Whether all timer fields are in monster records, room records, or runtime state structures.

## Conclusion

Validation0009 now has enough routine-level evidence to promote scheduler architecture into engine documentation, but still not enough to promote exact respawn fields or timer constants into `/Data`.
