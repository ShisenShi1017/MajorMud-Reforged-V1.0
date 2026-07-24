# Final Native Closure 0013 — Host Return-Value Domains

## Proven

- Analyzed all 14056 direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.
- Classified bounded first-use return domains for 121 imported host symbols.
- 10 symbols are tested for zero/nonzero at one or more direct callsites.
- 2 symbols are compared against immediate constants.
- 0 symbols are consumed by bit masks or bitwise transforms.
- 12 symbols now have a conservative machine-word domain classification.
- WCC/WBB counterpart profiles match exactly.

## Runtime effect

`NativeHostReturnDomainRegistry` exposes proven caller-side domain evidence while exact native return declarations, signedness, handles, pointers, enums, and flag names remain fail-closed.

## Remaining boundary

Exact return types and implementations still require matching Worldgroup binaries, symbols, headers, or dynamic traces.
