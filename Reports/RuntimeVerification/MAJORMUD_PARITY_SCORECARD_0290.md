# MajorMUD Parity Scorecard 0290

- **PASS:** 72
- **STRONG:** 70
- **UNKNOWN:** 44
- **Scored requirements:** 186
- **Strict proven parity:** 38.7%
- **Evidence-backed parity:** 76.3%

## By subsystem

- **Administration & Host** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Administration / Security / Builder** — PASS 0, STRONG 0, UNKNOWN 2; strict 0.0%, evidence-backed 0.0%
- **Character & Status** — PASS 20, STRONG 15, UNKNOWN 7; strict 47.6%, evidence-backed 83.3%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 1, STRONG 13, UNKNOWN 2; strict 6.2%, evidence-backed 87.5%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 1, UNKNOWN 6; strict 0.0%, evidence-backed 14.3%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 1, STRONG 10, UNKNOWN 2; strict 7.7%, evidence-backed 84.6%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Parties / Communication / Organizations** — PASS 1, STRONG 2, UNKNOWN 5; strict 12.5%, evidence-backed 37.5%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 1; strict 91.7%, evidence-backed 91.7%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 11, STRONG 8, UNKNOWN 5; strict 45.8%, evidence-backed 79.2%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 4, STRONG 5, UNKNOWN 4; strict 30.8%, evidence-backed 69.2%

## Pass 20 Timers / Resets / World Rules notes

- Native `WCCMMUD.mcv` directly retains `Seconds between saving Player data? 60`. The Classic scheduler now uses a 60-second character autosave interval instead of the Reforged five-minute placeholder.
- Native `WCCMMUD.mcv` also retains `Seconds between saving Buffer data? 300`. The value is preserved in the timing authority, but no generic Reforged world-state writer is labeled as native `Buffer data` until exact ownership/order is recovered.
- Retained Last Killed/regen evidence is activated at STRONG confidence: elapsed NPC respawn timers no longer materialize from the global world heartbeat. Due NPCs are admitted when a player enters the NPC home room.
- Zero-delay room semantics, exact native minute-boundary phase, restart/missed-timer behavior, simultaneous due-timer ordering, and world-reset interaction remain UNKNOWN.
- The already-PASS REST/MEDITATE interruption contract is repaired in the live path: ordinary commands do not cancel focused regeneration; movement and actual combat entry do.
- Five-second graceful exit/save timing is centralized in `RuntimeClassicTimingAuthority` without changing its existing PASS classification.
- Exact world-reset categories/order, hard-reset encodings, active-player/combat behavior, corpse/limited-item survival, reset rollback, events, and broader world-rule mutation semantics remain UNKNOWN.
- Two new independently auditable requirements are added: `timers.player-data-save-60s` = PASS and `timers.room-entry-respawn-admission` = STRONG.

Pass 20 score: **PASS 72 / STRONG 70 / UNKNOWN 44 = 186**, strict **38.7%**, evidence-backed **76.3%**.
