# Code Update 0296 — Doors / Locks / Traps / Room Effects Closure

Pass 25 narrows the Classic room-mechanics boundary without inventing native rolls.

## Door / Gate correction

MajorMUD Door/Gate Para2 belongs to the explicit `PICKLOCK <direction>` / `BASH <direction>` action boundary. It is not a passive stat requirement that should automatically block walking through an already-open door. Para3 is the number of five-minute blocks the door remains open, and Para4 is the key requirement.

The live movement path now checks the resulting door state (`locked`, then `closed`) only. `BASH <direction>` is tested as a door action before falling back to combat BASH; `BASH <target>` still uses the combat runtime. `PICKLOCK <direction>` is owned by the living-world boundary and requires the recovered Picklocks skill.

The exact native PICKLOCK/BASH success roll, exact success/failure text, key consumption, durability/break recovery, and reset persistence remain evidence-gated.

## Trap command boundary

Native help proves that characters can search for traps and that an expert may issue `DISARM TRAP <direction>` after a trap is found. The command is now registered and routed to the Classic room boundary. The decoded 295 exit trap identities/effect operands remain authoritative.

Per-exit native Para2 activation states are still absent from the current MME-derived projection, and exact detection/disarm rolls, failed-disarm triggering, state transitions, rearm/reset behavior, and native text remain UNKNOWN. No roll was invented.

## Environmental hazard scope correction

The stock RoomType family has no generic environmental-hazard record family. Classic room damage/status mechanics remain owned by actual decoded MajorMUD mechanisms such as damage/spell traps, CAST transitions, text/script actions, remote actions, and other room/exit record types.

`RuntimeEnvironmentalHazardService` remains available as a Reforged extension abstraction but no longer creates a scored Classic UNKNOWN requirement.

## Score

- PASS: 73
- STRONG: 76
- UNKNOWN: 29
- Total: 178
- Strict parity: 41.0%
- Evidence-backed: 83.7%
