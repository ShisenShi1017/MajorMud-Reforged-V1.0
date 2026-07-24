# Final Native Closure 0014 — Host Argument Positions

## Proven

- Analyzed all 4630 direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.
- Recovered bounded stack-argument position profiles for 121 imported host symbols.
- 88 symbols have one stable bounded argument count across all observed modules.
- 77 symbols have at least one argument position with a stable caller-source category.
- 70 symbols have stable categories at every observed bounded argument position.
- Stable positions include 10 zero/null positions, 45 immediate-constant positions, and 21 address/memory-derived positions.
- WCC/WBB counterpart profiles match exactly.

## Runtime effect

`NativeHostArgumentPositionRegistry` exposes proven caller-side positional ABI evidence while exact parameter types, pointee layouts, enum names, and ownership remain fail-closed.

## Remaining boundary

Exact prototypes and host semantics still require matching Worldgroup binaries, symbols, headers, or dynamic traces.
