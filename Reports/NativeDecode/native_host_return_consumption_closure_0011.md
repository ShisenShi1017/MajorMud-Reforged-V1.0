# Final Native Closure 0011 — Host Return-Value Consumption

## Proven

- Analyzed all 14056 direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.
- 57 imported symbols have at least one direct callsite where the caller consumes EAX before an observed overwrite or control-flow boundary.
- 28 symbols have EAX consumed at every observed direct callsite.
- WCC/WBB counterpart return-consumption histograms match for every imported symbol.
- The evidence distinguishes zero/flag tests, comparisons, argument forwarding, copies/address use, transformations, overwrites, and no bounded local consumption.

## Runtime effect

`NativeHostReturnConsumptionRegistry` exposes only whether a machine-word return is observably consumed. Exact C return types remain fail-closed.

## Remaining boundary

Exact scalar, pointer, handle, boolean, signedness, floating-point, and structure-return declarations still require Worldgroup binaries, symbols, headers, or dynamic traces.
