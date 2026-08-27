# Code Update 0298 — Pass 27 Trap State / Reset Boundary Closure

## Purpose
Close provable Classic trap schema and command-admission behavior without inventing the remaining trap detection/disarm formulas or reset transitions.

## Proven/corroborated corrections
- Stock RoomRec has ten exit slots. `RoomType` and `Para2` are signed 16-bit; `Para1`, `Para3`, and `Para4` are signed 32-bit.
- RoomType 9 is a damage Trap; RoomType 24 is a Spell Trap.
- Trap Para2 states are exactly: 0 Active, 1 Inactive, 2 Hidden, 3 Active movement, 4 Inactive movement.
- Native help documents SEARCH discovery and `DISARM TRAP <direction>`. Anyone may search; only a traps expert may attempt to disarm a found trap.
- The Classic command boundary now enforces the Traps/DisarmTraps ability requirement before the unresolved native disarm dispatcher.
- Generic `RuntimeTrapService` state such as charges/rearm timers is a Reforged extension model and is not Classic parity evidence.

## Evidence conflict retained
Nightmare Redux `docs/roomspara.txt` and `frmRoom.frm` disagree on whether Para3 or Para4 owns the passage versus disarm-failure message for trap records. Neither mapping is promoted. Raw fields must be preserved until native caller evidence resolves the conflict.

## Remaining UNKNOWN
- each stock exit's original Para2 value;
- exact SEARCH trap-detection formula and Traps-skill bonus;
- exact DISARM success formula;
- whether failed DISARM triggers the trap and exact ordering;
- exact Para3/Para4 message ownership/rendering;
- state transitions among Active/Inactive/Hidden/Movement modes;
- rearm/reset/world-cleanup interaction and simultaneous ordering.

## Correct recovery source
The MMExplorer MDB is insufficient because the exported Rooms table retained rendered trap qualifiers but discarded native Para2. Recover exact per-exit state from stock `WCCMP002` room records (or an equivalent native RoomRec export).
