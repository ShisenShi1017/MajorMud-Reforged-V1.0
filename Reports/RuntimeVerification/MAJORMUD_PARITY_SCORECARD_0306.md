MAJORMUD PARITY SCORECARD

Strict proven parity: 97/200 = 48.5%
Evidence-backed coverage: 173/200 = 86.5%
Unresolved native dependency groups: 27/200 = 13.5%
Remaining-system shells: 27/27 implemented (reported separately; no parity credit)

Scoring rule: PASS = strict credit; STRONG = implemented/evidence-backed but not exact; UNKNOWN = blocks 100%. No half-credit is awarded.
The denominator is the current known requirement set and may grow when new native behavior is discovered.

BY SUBSYSTEM
- Administration & Host: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Character & Status: PASS 24, STRONG 15, UNKNOWN 7 (strict 52.2%, evidence 84.8%)
- Combat: PASS 12, STRONG 5, UNKNOWN 1 (strict 66.7%, evidence 94.4%)
- Commands & Help: PASS 10, STRONG 1, UNKNOWN 2 (strict 76.9%, evidence 84.6%)
- Data Reconstruction: PASS 0, STRONG 3, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Economy & Services: PASS 1, STRONG 15, UNKNOWN 0 (strict 6.3%, evidence 100.0%)
- Host / Persistence / Worldgroup: PASS 4, STRONG 1, UNKNOWN 4 (strict 44.4%, evidence 55.6%)
- Import / Export / Limits / Recovery: PASS 2, STRONG 0, UNKNOWN 2 (strict 50.0%, evidence 50.0%)
- Items & Inventory: PASS 4, STRONG 1, UNKNOWN 1 (strict 66.7%, evidence 83.3%)
- NPC & AI: PASS 1, STRONG 10, UNKNOWN 2 (strict 7.7%, evidence 84.6%)
- Optional / Rankings / Extensions: PASS 0, STRONG 2, UNKNOWN 1 (strict 0.0%, evidence 66.7%)
- Parties / Communication / Organizations: PASS 10, STRONG 3, UNKNOWN 2 (strict 66.7%, evidence 86.7%)
- Protocol & Legacy Clients: PASS 11, STRONG 0, UNKNOWN 0 (strict 100.0%, evidence 100.0%)
- Quests / Scripts / Triggers: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Rooms & World: PASS 11, STRONG 9, UNKNOWN 1 (strict 52.4%, evidence 95.2%)
- Spells & Effects: PASS 1, STRONG 2, UNKNOWN 0 (strict 33.3%, evidence 100.0%)
- Timers / Resets / World Rules: PASS 6, STRONG 5, UNKNOWN 4 (strict 40.0%, evidence 73.3%)

PASS 35 CHANGE
- Added one narrow PASS for the exact-version direct WCCMMUD/WBBMMUD randomness import-call boundary.
- Broad native.global-rng.native-contract remains UNKNOWN: indirect paths, gameplay semantic attribution, execution-thread CRT mapping, draw ordering, and instruction-equivalent range-loop activation are still unresolved.
