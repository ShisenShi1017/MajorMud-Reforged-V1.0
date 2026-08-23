# Librarium — Worldgroup Remoteaction Dispatch Update0022

## Proven boundary

`wccmmud.dll!0x0046C573` is the four-argument MajorMUD room-action executor reached by the special-command `remoteaction` keyword. The decoded syntax is `remoteaction <actionRecordId> <optionalMessageRecordId> <selector> <actionSlot>`. It reads the action type from `record + 0x360 + actionSlot*2` and directly switches on types 6 through 24. Earlier evidence labels incorrectly called the second argument the action index; callee frame use proves the fourth argument owns every indexed action field.

## Proven action classes

- Types **7 and 11** form a paired toggle class. The current action state at `+0x374` toggles between `0` and `2`, the record is marked dirty at `+0x5F8`, and a linked room/action record referenced through `+0x338` is resolved and toggled when its mapped action type is also 7 or 11.
- Types **9 and 24** form a room-wide dispatch class guarded by parameter value `2` at `+0x39C`. Every active session in the same room receives helper dispatch `0x00417692(..., 7)`, followed by processing of up to fifteen room object IDs at `+0x400` through `0x004252F3`.
- Type **6** is a mode-indexed action-state bit controller. Mode 0 clears bits `0x10..0x2000`; modes 1 through 10 clear one corresponding state bit. When no controlled bit remains, the routine forces state `8` and emits configured output.
- Types **8, 10, and 12–23** have no type-specific branch in this routine and pass to common finalization.

## Common ordering

Optional action text is emitted first. Type-specific mutation follows. The resulting record is marked dirty and stored, optional state propagation executes, session/action cleanup runs, output is flushed, and the optional host status refresh is last.

Numeric action type names are intentionally not invented. Their side effects are authoritative; labels remain open pending independent record or UI correlation.
