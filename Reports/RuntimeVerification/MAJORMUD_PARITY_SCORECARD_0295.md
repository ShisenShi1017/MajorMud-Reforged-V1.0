# MajorMUD Parity Scorecard 0295

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

## Pass 24 notes

- Corrected the active PASS semantics for race/class EXP: these values form the progression chart used to determine total XP required for each level; they do not multiply XP awards.
- Removed the legacy race/class-scaled AwardExperience path so monster/quest/script rewards enter the XP ledger at their earned amount.
- Preserved the recovered stock chart formula: `(class ExpTable + 100) + race ExpTable` feeds the level-threshold schedule.
- Classic party admission now uses a STRONG six-total-character cap. Exact native party-full wording remains UNKNOWN.
- Party membership is not an XP-sharing rule. NPC XP remains based on active combat with that exact NPC at death.
- Native-era captures show monster currency dropping into the room; no automatic party loot split is promoted. SHARE remains explicit voluntary currency distribution.
- XP remainder disposition, leader-leave behavior, exact Frontrank slot formula, party death/disconnect and cross-subsystem spell/teleport/resurrection edges remain UNKNOWN.

Pass 24 score: **PASS 73 / STRONG 75 / UNKNOWN 31 = 179**, strict **40.8%**, evidence-backed **82.7%**.
