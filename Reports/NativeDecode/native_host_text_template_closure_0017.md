# Final Native Closure 0017 — Host Text Template Contract

## Proven

- Classified 396 immediate host-facing ASCII records covering 628 observed argument occurrences.
- Separated 82 printf-style template records from 314 literal records.
- Recovered 232 conversion occurrences and their exact caller-side directive syntax.
- Recorded CR, LF, TAB, escaped-percent, edge-whitespace, byte-length, symbol, argument-position, and address evidence.
- Confirmed WCC/WBB syntactic-contract parity for both MMUD and MMPLS module pairs.

## Runtime effect

`NativeHostTextTemplateRegistry` exposes proven templates and marks templates requiring a native-compatible formatter. It intentionally does not render them or assign unsupported variadic parameter types.

## Remaining boundary

The exact Worldgroup formatter, runtime-built strings, variadic argument types, message routing, terminal translation, and output ordering still require `WGSERVER.EXE`, symbols, headers, or dynamic traces.
