# Final Native Closure 0012 — Host Argument-Source Footprints

## Proven

- Analyzed all 14056 direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.
- Classified bounded caller-side argument sources for 121 imported host symbols.
- 87 symbols have one stable bounded argument count across all observed direct callsites.
- 24 symbols receive at least one proven zero/null machine-word argument.
- 42 symbols receive at least one address-, symbol-, or memory-derived argument.
- WCC/WBB counterpart argument-source profiles match exactly.

## Runtime effect

`NativeHostArgumentSourceRegistry` exposes only proven caller-side source classes and stable bounded counts. Exact parameter types, ownership, pointee layouts, and hidden/register arguments remain fail-closed.

## Remaining boundary

Exact prototypes and implementations still require matching Worldgroup binaries, symbols, headers, or dynamic traces.
