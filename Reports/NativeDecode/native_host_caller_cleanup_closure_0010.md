# Final Native Closure 0010 — Uniform Caller Cleanup Cohorts

## Proven

- 42 imported Worldgroup host symbols have immediate caller stack cleanup at every observed direct MMUD/MMPLS callsite, with one stable cleanup size per symbol.
- These symbols cover 2320 direct WCC/WBB callsites.
- 28 symbols also have a bounded pre-call push count exactly equal to `cleanup_bytes / 4` at every observed callsite, covering 796 direct calls.
- WCC/WBB parity is inherited from the validated callsite evidence in FinalNativeClosure0009.

## Runtime effect

`NativeHostCallerCleanupRegistry` exposes only the proven cleanup byte count and exact-footprint status. It does not fabricate parameter types, return types, register arguments, or host implementations.

## Important examples

- `_sameas`: 8-byte caller cleanup with an exact two-push bounded footprint at all 394 paired-distribution calls.
- `_dfaStepLock`: 12-byte caller cleanup with an exact three-push bounded footprint at all 94 calls.
- `_lngrnd`: stable 8-byte caller cleanup at all 84 paired-distribution calls, but the bounded setup window is not exact at every site, so a complete prototype is not asserted.

## Remaining boundary

Exact parameter types, return types, register or hidden arguments, indirect-call signatures, and implementations still require Worldgroup binaries, symbols, or dynamic traces.
