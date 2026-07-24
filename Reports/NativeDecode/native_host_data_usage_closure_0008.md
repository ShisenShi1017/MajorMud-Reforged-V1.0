# Final Native Closure 0008 — Imported Host Data Usage

## Proven

- Classified all 35 distinct non-direct Worldgroup imports used by the supplied MMUD/MMPLS modules.
- Recovered whether each import is dereferenced locally, passed onward, consumed without a recoverable dereference, or not statically referenced.
- Recovered MajorMUD-visible access widths and field offsets reached through imported host pointers.
- Verified WCC and WBB counterparts have identical usage profiles while retaining separate binary identities.
- Confirmed `_input` is accessed as byte-addressable host data with observed client offsets 0 and 60.
- Confirmed `_margv` is traversed through 32-bit entries at offsets 0, 4, 8, 12, and 16.

## Runtime effect

`NativeHostDataUsageRegistry` exposes only the proven client-side access footprint. Exact host structure names, allocation lifetime, ownership rules, and callback contents remain fail-closed.

## Remaining native dependency

The matching Worldgroup host executable, symbols, or runtime memory traces are still required to recover exact C declarations and complete structure layouts.
