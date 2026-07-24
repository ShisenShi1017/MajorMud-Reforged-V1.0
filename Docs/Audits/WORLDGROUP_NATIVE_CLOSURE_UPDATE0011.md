# Worldgroup Native Closure Update0011 — Gameplay Command Gateway

## Proven boundary

`wccmmud.dll!0x0045003C` is the active gameplay command gateway, not the terminal tokenizer. It resolves the current player/session record, applies state and movement gates, scans room action records, handles speech prefixes, and delegates ordinary commands to `0x0046B010`.

## Recovered ordering

1. Session and pending-exit gates.
2. Movement and wait-state restrictions.
3. Up to ten room-action slots, including type `0x0A` and `0x0C` alias matching.
4. Special-command handlers.
5. Say/yell prefix dispatch.
6. General command execution through `0x0046B010`.
7. `Your command had no effect.` only when no branch handles the input.

## Concrete data contracts

- Current input buffer pointer: `0x0049D580`.
- Room-action aliases occupy three 82-byte fields at `+0x04`, `+0x56`, and `+0xA8`.
- The room action scan is capped at ten slots.
- General execution returns a handled bit that is OR-combined across the room and linked records.
- Speech formatting distinguishes local `say` from directional `yell`.

## Next trace

Trace `0x0046B010` to recover tokenization, command-table lookup, handler selection, and direct textblock/remoteaction edges.
