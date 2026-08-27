# MajorMUD Parity Scorecard 0291

- **PASS:** 73
- **STRONG:** 70
- **UNKNOWN:** 36
- **Scored requirements:** 179
- **Strict proven parity:** 40.8%
- **Evidence-backed parity:** 79.9%

## By subsystem

- **Administration & Host** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Character & Status** — PASS 20, STRONG 15, UNKNOWN 7; strict 47.6%, evidence-backed 83.3%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 1, STRONG 13, UNKNOWN 2; strict 6.2%, evidence-backed 87.5%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 1, UNKNOWN 4; strict 0.0%, evidence-backed 20.0%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 1, STRONG 10, UNKNOWN 2; strict 7.7%, evidence-backed 84.6%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Parties / Communication / Organizations** — PASS 2, STRONG 2, UNKNOWN 3; strict 28.6%, evidence-backed 57.1%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 0; strict 100.0%, evidence-backed 100.0%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 11, STRONG 8, UNKNOWN 4; strict 47.8%, evidence-backed 82.6%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 4, STRONG 5, UNKNOWN 4; strict 30.8%, evidence-backed 69.2%

## Pass 21 notes

- Reconciles the active parity ledger with operator-confirmed scope: Worldgroup mail/account-session/login behavior, host limits, weather/day-night, native logging/diagnostics, native SYSOP interface parity and crash/startup behavior no longer count as MajorMUD UNKNOWNs.
- Native organization scope is promoted to PASS: MajorMUD organizations are gangs; gang commands and gang-house deed/upkeep behavior are retained separately for exact-mechanics closure.
- Bare native SET settings toggle current state. GOSSIP and AUCTION retain their directly documented explicit ON/OFF forms; unresolved TELEPATH explicit polarity stays evidence-gated.
- `BUY HEALING` is recognized as the healer service. Generic price/order remains UNKNOWN, while decoded room textblocks preserve exact room-specific price/cast/delay evidence.
- Ticket/item exit type 17 is active as a required-item gate without invented consumption. There is no native transportation-service model.
- TOP adventurer/gang/class forms are recognized without inventing ranking order/output.
- Casino and arena are confirmed native features; their remaining exact game rules stay UNKNOWN.

Pass 21 score: **PASS 73 / STRONG 70 / UNKNOWN 36 = 179**, strict **40.8%**, evidence-backed **79.9%**.
