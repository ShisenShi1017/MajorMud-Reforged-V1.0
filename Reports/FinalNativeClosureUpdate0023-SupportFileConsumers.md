# Final Native Closure Update0023 — Support-file consumer contracts

## Proven closure

- Recovered **7** gravestone override files using the exact filename grammar `WCC<decimal-record-id>.STO`.
- Recovered **47** supplied WCC house-description overrides using `WCC<decimal-house-room-id>15.HSE`; the trailing `15` is invariant across the supplied family.
- Bound each override to its decimal identifier, exact payload hash, byte length, title/role, and available WBB counterpart.
- Proved static native references from MMUD/MMUTL to `WCCADDON.SYS`, `WCCDMA.SYS`, `wccknows.sys`, `wccmmutl.bin`, `WCCBOX.BIN`, and `wccaedit.bin`.
- Preserved distribution coverage differences without inventing missing WCC files.

## Still left

- Recover the exact native open/read call paths and fallback ordering for STO and HSE overrides.
- Recover the token-validation algorithms behind `WCCDMA.SYS`, `WCCADDON.SYS`, and `wccknows.sys`.
- Trace BIN assets through console rendering and Worldgroup palette conversion.
