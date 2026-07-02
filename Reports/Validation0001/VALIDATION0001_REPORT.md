# MajorMUD Reforged — MBBSEmu Validation0001 Report

## Objective
Inventory the uploaded external validation sources and identify the highest-value evidence paths for validating DataSimVer hypotheses.

## Baseline
- Current DataSimVer baseline: DataSimVer0024 cumulative package.
- Validation pass: Validation0001.
- `/Data` modification status: unchanged.

## Sources inventoried
| Source | Files | Bytes | Role |
|---|---:|---:|---|
| MBBSEmu-master.zip | 572 | 841286 | Host emulator source; cleanup, scheduler, module lifecycle, Btrieve behavior. |
| MBBSDASM-master.zip | 46 | 112919 | Disassembler source; supports interpretation of MajorBBS/Worldgroup modules. |
| MBBSEmu.Disassemblies-main.zip | 8 | 4343139 | Public disassemblies; includes MajorMUD WCCMMUD.DLL.asm and IDA database. |
| EditorPatch0003-MDB-Extractor-Correction.zip | 7 | 46363 | Project extraction tooling and extracted MDB correction evidence. |
| MajorMUD-Reforged-DataSimVer0024-Cumulative.zip | 392 | 5465609 | Current project baseline for comparison. |

## Key findings
1. The external source set is sufficient to start validation: emulator source, disassembler source, MajorMUD disassembly, and extraction-tool evidence are all present.
2. MBBSEmu provides strong host-level evidence for nightly cleanup scheduling: configurable `Cleanup.Time`, default 03:00, daily timer, warning window, session removal, module cleanup routine calls, finish-up routine calls, cleanup EXE execution, and host restart.
3. The MajorMUD disassembly provides direct module-level investigation targets for search, timer, cleanup, reset, and command handling, but routines still require mapping before behavior promotion.
4. No direct evidence in Validation0001 justifies changing `/Data`.

## Promotion status
- `/Data` changed: No.
- Cleanup semantics promoted: No; host-level nightly cleanup scheduler evidence is now externally supported, but MajorMUD item cleanup semantics still require module-routine validation.
- Search formula promoted: No; search command/messages exist in disassembly, but formula not mapped yet.
- Monster respawn semantics promoted: No; timer evidence exists, but monster-specific routines are not mapped yet.
- Quest persistence promoted: No.

## Recommended next validation pass
Validation0002 should map the MBBSEmu host lifecycle and imported/exported routine calls used by MajorMUD, especially `mcurou`, `finrou`, status-code dispatch, timer callbacks, and Btrieve operations.
