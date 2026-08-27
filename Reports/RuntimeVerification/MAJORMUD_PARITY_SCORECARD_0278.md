# MajorMUD Parity Scorecard - Code Update 0278

- Strict proven parity assertion closure: **55/128 = 43.0%**
- Evidence-backed assertion coverage (PASS + STRONG): **80/128 = 62.5%**
- Unresolved native dependency groups/assertions: **48/128 = 37.5%**
- Remaining-system compatibility/policy shells implemented: **36/36** (reported separately and awarded no parity credit)

## Scoring rule

`PASS` is exact/captured/recovered behavior with executable regression proof. `STRONG` is implemented and strongly evidenced but still lacks exact native proof. `UNKNOWN` is an unresolved native dependency group/assertion and blocks 100%. No half-credit is used. The denominator is the currently known requirement set and can grow if new native behavior is discovered.

## By subsystem

- **Administration / Security / Builder** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Character & Status** — PASS 10, STRONG 1, UNKNOWN 5; strict 62.5%, evidence-backed 68.8%
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

## Implementation / data coverage

These metrics are deliberately **not** converted into strict parity credit:
- Decode families: **39/39 connected**
- Decode scalar fields: **2094/2096 connected/structural**
- Spells classified: **1350/1350**
- Runtime status effects: **111/111**
- Native message catalog: **3869 messages loaded**
- Economy services: **204 classified / 0 unclassified**

## Pass 8 movement

Pass 8 moves the frozen score from **44/113 (38.9%)** strict in update 0277 to **55/128 (43.0%)** strict. Evidence-backed coverage moves from **65/113 (57.5%)** to **80/128 (62.5%)**. The denominator grows because recovered combat timing and physical-formula behaviors are now individually scored.

## Interpretation

Strict credit in this pass is limited to recovered formula/timing units with executable regression coverage. The broad combat-native boundary remains unresolved. General Backstab damage, broad physical-damage ordering, class accuracy ordering, non-default Speed-adjustment integration, some native combat narration, and death/reward/drop ordering are not declared closed.
