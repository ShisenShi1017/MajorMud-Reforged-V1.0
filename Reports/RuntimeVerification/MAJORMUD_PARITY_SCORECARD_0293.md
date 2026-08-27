# MajorMUD Parity Scorecard 0293

- **PASS:** 73
- **STRONG:** 75
- **UNKNOWN:** 31
- **Scored requirements:** 179
- **Strict proven parity:** 40.8%
- **Evidence-backed parity:** 82.7%

## By subsystem

- **Administration & Host** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Character & Status** — PASS 20, STRONG 15, UNKNOWN 7; strict 47.6%, evidence-backed 83.3%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 1, STRONG 15, UNKNOWN 0; strict 6.2%, evidence-backed 100.0%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 1, UNKNOWN 4; strict 0.0%, evidence-backed 20.0%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 1, STRONG 10, UNKNOWN 2; strict 7.7%, evidence-backed 84.6%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 2, UNKNOWN 1; strict 0.0%, evidence-backed 66.7%
- **Parties / Communication / Organizations** — PASS 2, STRONG 3, UNKNOWN 2; strict 28.6%, evidence-backed 71.4%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 0; strict 100.0%, evidence-backed 100.0%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 11, STRONG 8, UNKNOWN 4; strict 47.8%, evidence-backed 82.6%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 4, STRONG 5, UNKNOWN 4; strict 30.8%, evidence-backed 69.2%

## Pass 23 notes

- Native room-owned healer action scripts are now executable for the recovered `monsters -> price -> cast(219..224) -> adddelay` line family. Exact decoded price, native insufficient-funds message, and existing healer spell authorities are reused; `adddelay` timing remains gated.
- Ticket/item exit gates remain possession checks without invented consumption. Classic is still modeled with no transportation service.
- Room-owned textblock actions whose matched line contains only native `message`/`text` presentation plus one `teleport` now execute immediate decoded relocation and persist the destination.
- Complex/delayed room relocations remain evidence-gated. Party XP/loot sharing remains UNKNOWN because retained party help does not prove its formula/order.
- Global RNG remains UNKNOWN; healer magnitude rolls reuse the existing spell resolver but are not promoted to exact RNG parity.

Pass 23 score: **PASS 73 / STRONG 75 / UNKNOWN 31 = 179**, strict **40.8%**, evidence-backed **82.7%**.
