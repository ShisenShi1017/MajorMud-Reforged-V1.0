# Code Update 0287 — Classic Rooms Native Schema / Trap State Closure

## Scope
Pass 17 revisits the remaining Rooms/Doors/Locks/Traps boundary using the original Nightmare Redux room field map and room-parameter documentation. It corrects one prior parity overclaim instead of preserving a convenient approximation.

## Findings
- The stock room-exit record is mixed-width: `RoomType` and `Para2` are signed 16-bit integers; `RoomExit`, `Para1`, `Para3`, and `Para4` are signed 32-bit longs.
- Native exit types 0–24 and their parameter ownership are now captured in `RuntimeClassicRoomExitNativeFieldContract`.
- Hidden exits use explicit Para1 encodings including Hidden Passable, Hidden Need Search, action-count masks, Hidden Once, and Permanent Visible.
- Door/Key/Gate records carry native key, picklock/bash, and five-minute open-duration fields. This does **not** prove the live PICKLOCK/BASH command roll semantics.
- Trap type 9 and Spell Trap type 24 carry a separate Para2 state: Active, Inactive, Hidden, Active movement, or Inactive movement.

## Trap correction
The retained MMUD Explorer export used by Reforged preserves all 295 trap owners and their damage/spell operands, but the exporter emits only the trap display operand and drops native Para2 activation state. Previous Pass 11 runtime therefore treated every decoded trap as active. That was too strong.

Pass 17 keeps the exact trap catalog and effect consumers but fails closed before applying a trap whose native activation/transition semantics are unproven. The two live trap execution assertions are demoted from PASS to STRONG; no evidence was discarded.

## Still unresolved
- Per-exit native trap Para2 values and transitions/reset behavior.
- Detection/disarm/rearm rules and non-exit trap ownership.
- Exact PICKLOCK/BASH command success rolls/messages and key-consumption semantics.
- The single remaining `exit.map12.2248.d` Hidden/Unknown rug edge mutation/reset.
- World-container, environmental-hazard, and weather/day-night ownership/semantics.
