# MajorMUD Parity Scorecard - Code Update 0280

- Strict proven parity assertion closure: **63/157 = 40.1%**
- Evidence-backed assertion coverage (PASS + STRONG): **106/157 = 67.5%**
- Unresolved native dependency groups/assertions: **51/157 = 32.5%**
- Remaining-system compatibility/policy shells implemented: **36/36** (reported separately and awarded no parity credit)

## Scoring rule

`PASS` is exact/captured/recovered behavior with executable regression proof. `STRONG` is implemented or substantially integrated and strongly evidenced but still lacks exact native proof. `UNKNOWN` is unresolved and blocks 100%. No half-credit is used. The denominator can grow when a formerly broad subsystem is decomposed into auditable native requirements.

## By subsystem

- **Administration & Host** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Administration / Security / Builder** — PASS 0, STRONG 0, UNKNOWN 2; strict 0.0%, evidence-backed 0.0%
- **Character & Status** — PASS 18, STRONG 9, UNKNOWN 9; strict 50.0%, evidence-backed 75.0%
- **Combat** — PASS 12, STRONG 5, UNKNOWN 1; strict 66.7%, evidence-backed 94.4%
- **Commands & Help** — PASS 6, STRONG 1, UNKNOWN 2; strict 66.7%, evidence-backed 77.8%
- **Data Reconstruction** — PASS 0, STRONG 3, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Economy & Services** — PASS 0, STRONG 1, UNKNOWN 3; strict 0.0%, evidence-backed 25.0%
- **Host / Persistence / Worldgroup** — PASS 0, STRONG 2, UNKNOWN 7; strict 0.0%, evidence-backed 22.2%
- **Import / Export / Limits / Recovery** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Items & Inventory** — PASS 4, STRONG 1, UNKNOWN 1; strict 66.7%, evidence-backed 83.3%
- **NPC & AI** — PASS 0, STRONG 9, UNKNOWN 2; strict 0.0%, evidence-backed 81.8%
- **Optional / Rankings / Extensions** — PASS 0, STRONG 0, UNKNOWN 3; strict 0.0%, evidence-backed 0.0%
- **Parties / Communication / Organizations** — PASS 1, STRONG 1, UNKNOWN 6; strict 12.5%, evidence-backed 25.0%
- **Protocol & Legacy Clients** — PASS 11, STRONG 0, UNKNOWN 1; strict 91.7%, evidence-backed 91.7%
- **Quests / Scripts / Triggers** — PASS 0, STRONG 2, UNKNOWN 0; strict 0.0%, evidence-backed 100.0%
- **Rooms & World** — PASS 7, STRONG 1, UNKNOWN 7; strict 46.7%, evidence-backed 53.3%
- **Spells & Effects** — PASS 1, STRONG 2, UNKNOWN 0; strict 33.3%, evidence-backed 100.0%
- **Timers / Resets / World Rules** — PASS 3, STRONG 4, UNKNOWN 4; strict 27.3%, evidence-backed 63.6%

## Pass 10 NPC / AI movement

- Corrected `attack_count` to mean authored executable attack-row count rather than per-round swing count.
- Activated the recovered six NPC attack-selection attempt slots per 5-second combat round.
- Corrected monster `Energy` to a per-round gain with zero encounter-start carry and uncapped unused-energy carry between rounds.
- Replaced normalized/energy-filtered NPC attack weighting with one inclusive d100 and ordered authored `Att%` thresholds; unaffordable rows remain selected and are not re-normalized away.
- Corrected Type-2 direct spell field semantics: `AttAcc` = spell id, `AttMax` = cast level, `AttMin` = cast-success percent, with a separate inclusive cast roll.
- Corrected failed/fully-resisted direct Type-2 energy cost to VB-style `Round(AttEnergy/2,0)` midpoint-to-even behavior while physical misses/dodges retain full cost.
- Preserved the retained Type-1 physical early-round-end branch when remaining energy is below the selected physical row cost.
- Recorded the stock 18-round NPC HP-regeneration interval and decoded per-monster `hp_regen` amount as STRONG, while leaving exact live tick phase/order fail-closed.
- Downgraded Reforged-only target/threat/assist formulas from Verified to StronglyCorroborated because deterministic implementation does not prove original MajorMUD AI decision ordering.
- Folded operator-confirmed Classic boundaries into the ledger: one MajorMUD Sysop role gated by Worldgroup keys/WCCMMUD.INI, no native in-game builder, 9-life start/cap, one life lost on death, and one life gained per level up to 9 (increment remains STRONG until native code/table proof).

## Score movement

- Pass 9: **60 PASS / 30 STRONG / 52 UNKNOWN / 142 total** — strict 42.3%, evidence-backed 63.4%.
- Pass 10: **63 PASS / 43 STRONG / 51 UNKNOWN / 157 total** — strict 40.1%, evidence-backed 67.5%.
- The denominator grows because broad death/lives and NPC behavior are decomposed into independently auditable requirements. The lower strict percentage is denominator expansion, not loss of proven behavior; evidence-backed coverage rises and the UNKNOWN count still decreases by one.

## Validation note

The packaging environment does not have the .NET SDK, so `dotnet build` cannot be executed here. All available Python validators in the current complete-runtime gate were run separately; 27 passed, while Groups 1–3 and two legacy/evidence validators stopped only at documented missing-data/hard-coded-path/provenance prerequisites. The WCC Librarium exhaustion audit also passed.
