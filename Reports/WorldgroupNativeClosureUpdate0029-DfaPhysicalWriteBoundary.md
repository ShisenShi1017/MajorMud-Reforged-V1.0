# Worldgroup Native Closure Update0029 — DFA Physical Write Boundary

## Proven boundary

WGSERVER's exported DFA mutation wrappers marshal their arguments into the active DFA context and call `WBTRV32.DLL!BTRCALL` through IAT slot `0x0047BB9C`. The common wrapper at `0x0044302A` stores the returned status in `0x00467CA0` and the auxiliary status in `0x00467CA2`.

## Mutation operations

- `_dfaInsertV` and `_dfaInsertDup` use BTRCALL operation `2`.
- `_dfaUpdateV` and `_dfaUpdateDup` use operation `3`.
- `_dfaDelete` uses operation `4`.
- `_dfaUnlock` uses operation `0x1B`.
- `_dfaBegTrans`, `_dfaEndTrans`, and `_dfaAbtTrans` use `0x13 + subtype`, `0x14`, and `0x15` respectively.

Variable-length insert and update paths clamp record data to `0xFFF0` bytes. Insert/update data is staged in the shared record buffer at `0x00467CA4` before the storage call.

## Ordering

The proven wrapper ordering is: resolve retained context, normalize or clamp length, stage the record, invoke BTRCALL synchronously, store status, then enter operation-specific error reporting for nonzero results. Resetting the DFA block is separate from the physical mutation.

## Correction

Update0028 stated that MajorMUD imports no separate commit, rollback, journal, or checksum operation. That remains true at the MajorMUD import boundary. However, WGSERVER itself exports explicit begin/end/abort transaction wrappers; they are not directly imported by the recovered MajorMUD modules.

## Remaining boundary

No direct filesystem flush, journal format, backup rotation, checksum, or crash-replay behavior is visible in these WGSERVER wrappers. Those semantics must be traced inside `WBTRV32.DLL`, its backing files, or other host recovery components.
