# MajorMUD Parity Scorecard 0286

- **PASS:** 72
- **STRONG:** 59
- **UNKNOWN:** 47
- **Scored requirements:** 178
- **Strict proven parity:** 40.4%
- **Evidence-backed parity:** 73.6%

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
- **Rooms & World** — PASS 13, STRONG 3, UNKNOWN 6; strict 59.1%, evidence-backed 72.7%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 3, STRONG 4, UNKNOWN 4; strict 27.3%, evidence-backed 63.6%

## Pass 16 NPC closure notes

- MajorMUD monster storage is treated as a mixed-width legacy record, not as uniformly 16-bit.
- Ability 146 `MonsGuards` is now the only Classic NPC-to-NPC guard admission relationship. Reforged assist-group metadata is isolated to Custom/AI.
- Native message 8480 is a physical dodge/miss template, **not** an assist event. Thrag (npc.59) supplies all four caller-owned argument pairs; five Nahr (npc.1006) rows remain unresolved.
- The residual native AI UNKNOWN still covers flee, base aggro/perception acquisition, target switching, patrol/pathing, tracking, and exact decision priority/cadence.
