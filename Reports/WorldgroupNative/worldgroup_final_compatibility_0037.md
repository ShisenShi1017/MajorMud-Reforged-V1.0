# Worldgroup Final Compatibility Report — Update0037

## Verdict

The supplied Worldgroup runtime is compatible with the supplied MajorMUD modules at the recovered host ABI and ownership boundary. All **156** MajorMUD-used Worldgroup symbols match exports in the supplied `WGSERVER.EXE`; no unmatched or unowned used symbol remains.

## Verified compatibility

- 121 callable imports, 34 imported globals/indirect references, and one statically unused import are classified.
- WCC/WBB caller evidence is preserved and bound to the supplied host runtime.
- Lifecycle callbacks, global commands, session polling, tasks, message/terminal services, DFA operations, and support-file ownership are closed.
- The seven Room latch owners across 12 qualified exit rows are closed with a fail-safe native/passive-state policy.
- No speculative token service, HSE/STO fallback, player command, latch trigger, or recovery-log parser is activated.

## Corrected conclusions preserved

- `0x004381F2` is `_add2arr`, not the lifecycle dispatcher.
- `0x004789CA` is `_injacr`; `_globs` and `_nglobs` are imported host globals.
- `0x0046B010` is room-scoped broadcast, not command lookup.
- `0xC0` is a cooperative polling sentinel, not a time delay.

## Compatibility boundary

Exact `BTRVLOG.TRN` and `BTRVLOG.RCV` record layouts and undo/redo payload semantics remain dependent on representative artifacts or controlled crash captures. This is an external evidence boundary, not an unowned MajorMUD host dependency. The runtime therefore delegates those operations to the proven W32MKDE storage engine and does not invent a replacement format.
