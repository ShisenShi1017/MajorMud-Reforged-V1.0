# Final Native Closure 0006 — Host Data Import Classification

## Proven closure

Static x86 references to every non-direct `WGSERVER.EXE` import in the supplied WCC/WBB `MMUD.DLL` and `MMPLS.DLL` pairs were classified.

- No classified IAT data slot is used by a static `call` or `jmp` instruction.
- The previously unresolved non-direct import set is therefore a host data/global boundary in the supplied modules, not a proven callback-dispatch boundary.
- WCC and WBB retain the same access classification.
- `_register_agent` is imported by MMUD but has no static code reference in the analyzed image.

## Runtime policy

The server registry exposes only the proven static boundary. It does not assign invented C types, layouts, widths, ownership lifetimes, or callback semantics. Requests for an exact native type fail closed.

## Remaining native dependency

Exact types and structure layouts require matching `WGSERVER.EXE` symbols/source or dynamic memory traces. Function pointers stored inside host structures reached through these globals remain possible and are not disproven by this pass.
