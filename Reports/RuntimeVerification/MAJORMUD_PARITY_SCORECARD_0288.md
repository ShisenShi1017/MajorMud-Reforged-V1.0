# MajorMUD Parity Scorecard 0288

- **PASS:** 71
- **STRONG:** 68
- **UNKNOWN:** 45
- **Scored requirements:** 184
- **Strict proven parity:** 38.6%
- **Evidence-backed parity:** 75.5%

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
- **Parties / Communication / Organizations** — PASS 1, STRONG 1, UNKNOWN 6; strict 12.5%, evidence-backed 25.0%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 1; strict 91.7%, evidence-backed 91.7%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 11, STRONG 8, UNKNOWN 5; strict 45.8%, evidence-backed 79.2%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 3, STRONG 4, UNKNOWN 4; strict 27.3%, evidence-backed 63.6%

## Pass 18 GIVE / Shops / Transportation closure notes

- Classic GIVE now accepts the documented `GIVE <item> <player>` syntax; the older Reforged-only requirement for the word `to` is removed.
- `SET RECEIVE ON|OFF` is wired to GIVE. RECEIVE OFF rejects incoming items.
- GIVE checks recipient native encumbrance capacity before commit and routes item restrictions through `RuntimeItemEngine.Give`; limited status by itself is not a blanket transfer ban.
- Formal Reforged `TRADE` escrow/convenience transfer and generated `REPAIR` are blocked from Classic and removed from the MajorMUD unknown scope.
- BUY preserves stock-first then currency-debit failure ordering; exact native failure wording remains unresolved.
- Transportation is retained as MajorMUD-owned. Exact fares/schedules/boarding/messages remain UNKNOWN.
- The 12/2248 rug exit is no longer the lone unknown-qualified exit: its opening is temporary and shares the normal MajorMUD door-timer family. Exact timer duration remains unresolved.
