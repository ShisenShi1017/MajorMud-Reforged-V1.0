# MajorMUD Parity Scorecard - Code Update 0279

- Strict proven parity assertion closure: **60/142 = 42.3%**
- Evidence-backed assertion coverage (PASS + STRONG): **90/142 = 63.4%**
- Unresolved native dependency groups/assertions: **52/142 = 36.6%**
- Remaining-system compatibility/policy shells implemented: **36/36** (reported separately and awarded no parity credit)

## Scoring rule

`PASS` is exact/captured/recovered behavior with executable regression proof. `STRONG` is implemented and strongly evidenced but still lacks exact native proof. `UNKNOWN` is unresolved and blocks 100%. No half-credit is used. The denominator can grow when a formerly broad subsystem is decomposed into auditable native requirements.

## By subsystem

- **Administration / Security / Builder** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Character & Status** — PASS 15, STRONG 6, UNKNOWN 9; strict 50.0%, evidence-backed 70.0%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 0, STRONG 1, UNKNOWN 3; strict 0.0%, evidence-backed 25.0%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 2, UNKNOWN 7; strict 0.0%, evidence-backed 22.2%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 0, STRONG 1, UNKNOWN 2; strict 0.0%, evidence-backed 33.3%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Parties / Communication / Organizations** — PASS 1, STRONG 1, UNKNOWN 6; strict 12.5%, evidence-backed 25.0%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 1; strict 91.7%, evidence-backed 91.7%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 7, STRONG 1, UNKNOWN 7; strict 46.7%, evidence-backed 53.3%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 3, STRONG 4, UNKNOWN 4; strict 27.3%, evidence-backed 63.6%

## Pass 9 Character / Advancement movement

- Added explicit PASS coverage for native-traced Max HP, Max Mana/Kai, promotion HP/mana growth, race/class XP percentage modifiers, and the verified modified Stealth formula.
- Added STRONG live coverage for HP regeneration, mana regeneration/meditation, corrected character Magic Resistance, recovered stock raw Dodge, and de-duplicated acquired-ability/equipment modifier composition.
- Kept Picklocks stock formula, absolute XP/CP schedule, exact death/lives behavior, and full cross-category modifier order unresolved rather than guessing.
- The scored denominator grows from **128 to 142** because Pass 9 decomposes previously broad Character/Advancement behavior into fourteen explicit requirements. PASS count rises **55 → 60** and evidence-backed count rises **80 → 90**. The strict percentage therefore moves **43.0% → 42.3%** despite five new PASS closures; this is denominator expansion, not a regression.

## Implementation / data coverage

- Decode families: **39/39 connected**
- Decode scalar fields: **2094/2096 connected/structural**
- Economy services: **204 classified / 0 unclassified**
- Native message catalog: **3869 messages loaded**
- Runtime status effects: **111/111**
- Spells classified: **1350/1350**
- Character formula audit: **5/5 static vectors passed**

## Validation note

The packaging environment does not have the .NET SDK, so `dotnet build` cannot be executed here. Static formula vectors, JSON/parity-ledger consistency, and Python validators are run separately. Gameplay Groups 1–3 retain their pre-existing missing generated `Data/Classic` / hard-coded path failures and are not Pass 9 regressions.
