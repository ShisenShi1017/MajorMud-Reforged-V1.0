# Final Native Closure 0003 — Host Dispatch Boundary

## Proven

- Parsed the PE import tables and x86 import-thunk callsites for both WCC and WBB MMUD/MMPLS modules.
- WCC MMUD imports 149 WGSERVER symbols and contains 6547 direct calls to their thunks.
- WCC MMPLS imports 55 WGSERVER symbols and contains 481 direct calls to their thunks.
- WCC/WBB callsite counts are retained separately and hash-bound.
- `_lngrnd` is confirmed as a host-owned RNG import boundary; this pass does not invent its algorithm.

## Runtime effect

`NativeHostCallsiteRegistry` exposes only proven direct-call ownership evidence. It does not emulate missing host behavior.

## Remaining boundary

Indirect calls and the implementations behind WGSERVER imports remain native-host dependencies. Exact RNG, DFA/save locking, scheduling, message transport, and recovery semantics require the matching WGSERVER executable/runtime or dynamic traces.
