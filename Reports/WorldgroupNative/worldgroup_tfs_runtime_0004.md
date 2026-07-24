# Worldgroup Native Closure Update0004 — TFS Runtime State Machine

## Proven exports

- `_tfsrdl` (ordinal 1279, RVA `0x0003E8D6`) implements a five-state multi-file text scanner.
- `_tfsdpr` (ordinal 1273, RVA `0x0003EA2A`) copies the suffix pointer selected by `_tfspfx` back to `_tfsbuf`.

## State machine

- **0 — closed/exhausted:** no active stream and no remaining match.
- **1 — first match staged:** `_tfsopn` found a match; the first read call advances to state 2.
- **2 — open pending:** the enumerated filename is joined to the derived directory and opened with mode `rt`; state becomes 3.
- **3 — reading:** `_fgets(_tfsbuf, 0x81, _tfsfp)` reads a line, one trailing LF is removed, and the host normalizer runs. EOF closes the stream and moves to state 4.
- **4 — advance enumeration:** find-next succeeds and returns to state 2, or exhaustion resets state to 0.

## Structure boundary

The exported `_tfsfb` block occupies exactly `0x114` bytes before `_tfsfp`. Direct accesses prove a 260-byte filename region at offset `0x10`; the first 16 bytes remain helper-owned metadata whose individual field names are not yet proven.

## Ownership conclusion

TFS in this path is a text-file scanning subsystem. It does not itself establish MajorMUD save/database transaction semantics. Those remain in separate Worldgroup database APIs and must not be inferred from `_tfsopn`, `_tfsrdl`, or `_tfsabt`.
