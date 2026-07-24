# Worldgroup Native Closure Update0007 — Module and Player-Command Dispatch

## Proven findings

- `_register_module` at `0x0041FC6A` validates a 3–24 byte module name, stores the descriptor pointer, allocates a 36-byte per-module statistics record, and returns the zero-based module index.
- WCCMMUD registers descriptor `0x0047915C` at callsite `0x00401D83`; WCCMMPLS registers descriptor `0x00405124` at `0x00401AF1`. Both retain the returned module index.
- `_globalcmd` appends raw no-argument callback pointers to a fixed 50-entry array.
- The normal input handler executes global command callbacks in FIFO order before selecting a module-local callback.
- Global callbacks use explicit negative control values: `-4` continues, while `-2` and `-3` terminate with distinct host actions.
- Module-local dispatch selects descriptor offset `0x1C`, `0x2C`, or `0x20` according to session state 4, 5, or all other states.
- Both MajorMUD modules supply concrete callback pointers in all three selected slots.

## Boundary

This pass closes the Worldgroup-owned player-command routing boundary through the exact MajorMUD callback entry points. It does not assign unproven semantic names to the internal MajorMUD callback bodies.
