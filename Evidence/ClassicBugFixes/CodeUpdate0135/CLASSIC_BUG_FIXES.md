# Classic Bug Corrections — CodeUpdate0135

Source classification: operator-confirmed Classic corrections and preserved historical WCC/NMR bug reports.

## Corrected in authoritative data

- Bard shop effective percentage is floored at 10%.
- Cloaked figure `npc.1086` awards 10,000 XP, never 100,000.
- Mask items 288, 561, 999, and 1884 are explicitly assigned to the face slot, not ears.
- Lyrist's Companion (`item.1317`) invokes `spell.1055` (`BCNS`).
- BCNS cast message is 2520 and description message is 2531; both are required and nonblank.

## Corrected runtime contracts

- SEE HIDDEN tests hidden state independently of sneak state.
- Invalid/two-handed backstab attempts downgrade to an engaged normal attack.
- Paired lever state mutation is serialized atomically.
- A gang deed may remain with a player only while the gang exists, the player is current leader, and that gang owns the referenced house.

## Exhaustive-data repairs

- Repeated movement-trigger observations from multiple traversal profiles are merged into one canonical trigger while all distinct evidence rows are retained.
- `room.unknown` is not a real runtime room. It was removed from the room family and retained as evidence-only orphaned service data.
