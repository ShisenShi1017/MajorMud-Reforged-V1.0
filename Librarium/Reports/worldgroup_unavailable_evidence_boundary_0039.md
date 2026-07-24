# Worldgroup Unavailable Evidence Boundary — Update0039

## Decision

A compatible live Worldgroup environment cannot be obtained. Therefore the exact byte layouts of `BTRVLOG.TRN`, `BTRVLOG.RCV`, temporary transaction files, and per-transaction hexadecimal logs cannot be recovered through controlled capture.

This is a conclusive external-evidence boundary, not an unfinished runtime implementation.

## Preserved proven behavior

- MajorMUD submits persistent mutations through the recovered WGSERVER DFA interface.
- WGSERVER marshals those mutations into `WBTRV32.DLL!BTRCALL`.
- `W32MKDE.EXE` owns cache management, physical writes, flushes, transaction logging, and recovery.
- Physical mutation opcodes, flush wrappers, startup artifact probe order, and generic move/delete lifecycle primitives remain authoritative.
- Successful ordinary writes may remain cached until a proven flush, close, cache transition, metadata finalization, or recovery/log finalization boundary.

## Permanently bounded details

Without a live compatible engine, representative artifacts, vendor symbols, or source, the following must remain unknown:

- Exact transaction and recovery record layouts.
- Record opcodes and field offsets.
- Checksums.
- Undo-preimage versus redo-image payload semantics.
- Exact replay, rename, and cleanup ordering.

## Runtime policy

No speculative parser, checksum, replay engine, or recovery mutation logic is activated. The Update0038 capture harness remains available for future evidence but is not a completion requirement.

Worldgroup semantic exhaustion and compatibility closure remain complete.
