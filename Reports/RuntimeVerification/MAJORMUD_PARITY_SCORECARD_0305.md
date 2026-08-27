# MajorMUD Parity Scorecard — Update 0305 / Pass 34

- PASS: **96**
- STRONG: **76**
- UNKNOWN: **27**
- Total assertions: **199**
- Strict parity: **48.2%**
- Evidence-backed: **86.4%**

Pass 34 adds three narrow PASS assertions for the bound Worldgroup/Borland PRNG core, startup packed-time seed lifecycle, and host range endpoint contract. The broad global-RNG UNKNOWN remains for MajorMUD gameplay callsite-to-thread-state mapping, exact mutation order, and fully instruction-equivalent `_genrdn`/`_lngrnd` runtime activation.

## Categories

- Administration & Host: PASS 0, STRONG 2, UNKNOWN 0 / 2 (strict 0.0%, evidence 100.0%)
- Character & Status: PASS 24, STRONG 15, UNKNOWN 7 / 46 (strict 52.2%, evidence 84.8%)
- Combat: PASS 12, STRONG 5, UNKNOWN 1 / 18 (strict 66.7%, evidence 94.4%)
- Commands & Help: PASS 10, STRONG 1, UNKNOWN 2 / 13 (strict 76.9%, evidence 84.6%)
- Data Reconstruction: PASS 0, STRONG 3, UNKNOWN 0 / 3 (strict 0.0%, evidence 100.0%)
- Economy & Services: PASS 1, STRONG 15, UNKNOWN 0 / 16 (strict 6.2%, evidence 100.0%)
- Host / Persistence / Worldgroup: PASS 3, STRONG 1, UNKNOWN 4 / 8 (strict 37.5%, evidence 50.0%)
- Import / Export / Limits / Recovery: PASS 2, STRONG 0, UNKNOWN 2 / 4 (strict 50.0%, evidence 50.0%)
- Items & Inventory: PASS 4, STRONG 1, UNKNOWN 1 / 6 (strict 66.7%, evidence 83.3%)
- NPC & AI: PASS 1, STRONG 10, UNKNOWN 2 / 13 (strict 7.7%, evidence 84.6%)
- Optional / Rankings / Extensions: PASS 0, STRONG 2, UNKNOWN 1 / 3 (strict 0.0%, evidence 66.7%)
- Parties / Communication / Organizations: PASS 10, STRONG 3, UNKNOWN 2 / 15 (strict 66.7%, evidence 86.7%)
- Protocol & Legacy Clients: PASS 11, STRONG 0, UNKNOWN 0 / 11 (strict 100.0%, evidence 100.0%)
- Quests / Scripts / Triggers: PASS 0, STRONG 2, UNKNOWN 0 / 2 (strict 0.0%, evidence 100.0%)
- Rooms & World: PASS 11, STRONG 9, UNKNOWN 1 / 21 (strict 52.4%, evidence 95.2%)
- Spells & Effects: PASS 1, STRONG 2, UNKNOWN 0 / 3 (strict 33.3%, evidence 100.0%)
- Timers / Resets / World Rules: PASS 6, STRONG 5, UNKNOWN 4 / 15 (strict 40.0%, evidence 73.3%)
