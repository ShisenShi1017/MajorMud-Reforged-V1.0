# Final Native Closure 0009 — Host Call ABI Footprints

## Proven

- Recovered bounded pre-call stack-setup windows for every direct WGSERVER import-thunk call in supplied WCC/WBB MMUD and MMPLS modules.
- WCC MMUD contains 6547 analyzed direct host callsites; WCC MMPLS contains 481.
- WCC/WBB counterpart footprint histograms match for every shared imported symbol.
- Immediate caller `ADD ESP` cleanup observations are retained per callsite and symbol.
- Raw instruction windows are preserved so future Worldgroup symbols or binaries can promote these footprints into exact native prototypes.

## Runtime effect

`NativeHostCallAbiFootprintRegistry` exposes the proven MajorMUD-side ABI observations but refuses to claim formal parameter counts or calling conventions.

## Remaining boundary

Exact prototypes, register arguments, hidden parameters, delayed cleanup, callback signatures, and host implementations require the matching Worldgroup executable, symbols, or dynamic traces.
