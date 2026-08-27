# MajorMUD Parity Scorecard 0287

- **PASS:** 70
- **STRONG:** 63
- **UNKNOWN:** 47
- **Scored requirements:** 180
- **Strict proven parity:** 38.9%
- **Evidence-backed parity:** 73.9%

## By subsystem

- **Administration & Host** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Administration / Security / Builder** — PASS 0, STRONG 0, UNKNOWN 2; strict 0.0%, evidence-backed 0.0%
- **Character & Status** — PASS 20, STRONG 15, UNKNOWN 7; strict 47.6%, evidence-backed 83.3%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 0, STRONG 9, UNKNOWN 3; strict 0.0%, evidence-backed 75.0%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 1, UNKNOWN 6; strict 0.0%, evidence-backed 14.3%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 1, STRONG 10, UNKNOWN 2; strict 7.7%, evidence-backed 84.6%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Parties / Communication / Organizations** — PASS 1, STRONG 1, UNKNOWN 6; strict 12.5%, evidence-backed 25.0%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 1; strict 91.7%, evidence-backed 91.7%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 11, STRONG 7, UNKNOWN 6; strict 45.8%, evidence-backed 75.0%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 3, STRONG 4, UNKNOWN 4; strict 27.3%, evidence-backed 63.6%

## Pass 17 room/trap closure notes

- Native room-exit storage is mixed-width: RoomType/Para2 are signed 16-bit; RoomExit and Para1/Para3/Para4 are signed 32-bit.
- Stock Hidden Para1 encodings, Key/Door/Gate parameter ownership, and Trap/Spell-Trap Para2 state families are now recorded as evidence-backed schema contracts.
- The MME export retained all 295 trap identities/effect operands but discarded native Para2 trap state. Pass 17 therefore demotes exact live damage/spell trap execution from PASS to STRONG and fails closed before applying an unproven trap activation.
- The final Hidden/Unknown rug exit remains UNKNOWN because its native stored Para1 mutation/reset semantics are still absent.
- PICKLOCK/BASH operation semantics remain UNKNOWN; no command roll or key-consumption behavior was invented.
