# MajorMUD Parity Scorecard 0281

- **PASS:** 69
- **STRONG:** 45
- **UNKNOWN:** 50
- **Scored requirements:** 164
- **Strict proven parity:** 42.1%
- **Evidence-backed coverage:** 69.5%

## Rooms / Doors / Locks / Traps closure

- The seven formerly unresolved room latches are closed against exact decoded command/textblock/remoteaction evidence.
- All 206/206 action-gated exits are bound with zero native/passive placeholders.
- The old 266 unknown-qualified-exit count is obsolete. The current 0183 semantic corpus has 61,975/61,976 classified exits.
- The sole remaining unknown qualifier is `exit.map12.2248.d`; `move rug` / `pull rug` are known, but native mutation/reset semantics remain evidence-gated.
- The exact Classic exit-trap corpus contains 295 traps: 273 fixed-damage and 22 spell traps.
- Core decoded door state ownership/key enforcement is PASS; exact Picklocks/bash formulas and unsupported native door variants remain UNKNOWN.

## By subsystem

- **Administration & Host**: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- **Administration / Security / Builder**: PASS 0, STRONG 0, UNKNOWN 2 (strict 0.0%, evidence 0.0%)
- **Character & Status**: PASS 18, STRONG 9, UNKNOWN 9 (strict 50.0%, evidence 75.0%)
- **Combat**: PASS 12, STRONG 5, UNKNOWN 1 (strict 66.7%, evidence 94.4%)
- **Commands & Help**: PASS 6, STRONG 1, UNKNOWN 2 (strict 66.7%, evidence 77.8%)
- **Data Reconstruction**: PASS 0, STRONG 3, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- **Economy & Services**: PASS 0, STRONG 1, UNKNOWN 3 (strict 0.0%, evidence 25.0%)
- **Host / Persistence / Worldgroup**: PASS 0, STRONG 2, UNKNOWN 7 (strict 0.0%, evidence 22.2%)
- **Import / Export / Limits / Recovery**: PASS 0, STRONG 0, UNKNOWN 3 (strict 0.0%, evidence 0.0%)
- **Items & Inventory**: PASS 4, STRONG 1, UNKNOWN 1 (strict 66.7%, evidence 83.3%)
- **NPC & AI**: PASS 0, STRONG 9, UNKNOWN 2 (strict 0.0%, evidence 81.8%)
- **Optional / Rankings / Extensions**: PASS 0, STRONG 0, UNKNOWN 3 (strict 0.0%, evidence 0.0%)
- **Parties / Communication / Organizations**: PASS 1, STRONG 1, UNKNOWN 6 (strict 12.5%, evidence 25.0%)
- **Protocol & Legacy Clients**: PASS 11, STRONG 0, UNKNOWN 1 (strict 91.7%, evidence 91.7%)
- **Quests / Scripts / Triggers**: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- **Rooms & World**: PASS 13, STRONG 3, UNKNOWN 6 (strict 59.1%, evidence 72.7%)
- **Spells & Effects**: PASS 1, STRONG 2, UNKNOWN 0 (strict 33.3%, evidence 100.0%)
- **Timers / Resets / World Rules**: PASS 3, STRONG 4, UNKNOWN 4 (strict 27.3%, evidence 63.6%)

## Scoring rule

PASS receives strict parity credit. STRONG is evidence-backed but not yet exact native proof. UNKNOWN blocks strict parity. The denominator may grow when broad requirements are decomposed into independently testable native behavior.

## Validation

- Pass 11 static audit: **98/98 checks passed**.
- Full C# compilation was not run in the packaging environment because the .NET SDK is unavailable.
- Existing Gameplay Groups 1–3 generated-data/path prerequisites are unchanged and are not Pass 11 regressions.
