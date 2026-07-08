# Runtime-0006 Movement Strategy

Runtime-0006 adds the first deterministic movement runtime.

## Implemented

- Room index by canonical `roomNumber`.
- Direction normalization for cardinal, vertical, and diagonal movement.
- Exit lookup by room and direction.
- Safe movement result tracing.
- Open-exit traversal for exits with `exit_type` of `null` or `0`.
- Special exit preservation as `UNKNOWN_SPECIAL_EXIT`.
- Movement validation against canonical `/Data/rooms.json`.

## Not Promoted Yet

The following are intentionally not guessed:

- Locked doors.
- Hidden exits.
- Search-gated exits.
- Key requirements.
- Exit scripts.
- Teleport/special transition logic.
- PvP or combat movement restrictions.
- Sneak, flee, follow, stamina, or encumbrance movement effects.

## Runtime Rule

Movement may traverse only clearly open exits. Special exits must return `UNKNOWN_SPECIAL_EXIT` until verified by evidence or implemented as explicit Reforged behavior.
