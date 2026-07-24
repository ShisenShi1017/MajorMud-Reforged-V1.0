# Final Native Closure 0007 — WGSERVER Import Table Layout

## Proven closure

The supplied WCC and WBB MMUD/MMPLS modules each use one contiguous x86 import-address-table run for all named `WGSERVER.EXE` imports.

- MMUD: 149 four-byte slots from `0x0049D488` through `0x0049D6D8`.
- MMPLS: 55 four-byte slots from `0x00407254` through `0x0040732C`.
- WCC and WBB counterparts preserve identical slot order and virtual addresses while retaining separate exact hashes.

The runtime registry now rejects unknown or hash-mismatched modules rather than applying a layout inferred from another distribution.

## Remaining boundary

This closes the MajorMUD-side binary linkage layout only. Function bodies, host data types, scheduler behavior, RNG implementation, persistence internals, and callback structures remain inside the unavailable matching Worldgroup host runtime.
