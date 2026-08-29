# MajorMUD Parity Scorecard

- PASS: **98**
- STRONG: **102**
- UNKNOWN: **0**
- Scored assertions: **200**
- Strict parity: **49.0%**
- Evidence-backed parity: **100.0%**

| Category | PASS | STRONG | UNKNOWN | Total | Evidence-backed |
|---|---:|---:|---:|---:|---:|
| Administration & Host | 0 | 2 | 0 | 2 | 100.0% |
| Character & Status | 24 | 22 | 0 | 46 | 100.0% |
| Combat | 12 | 6 | 0 | 18 | 100.0% |
| Commands & Help | 10 | 3 | 0 | 13 | 100.0% |
| Data Reconstruction | 0 | 3 | 0 | 3 | 100.0% |
| Economy & Services | 1 | 15 | 0 | 16 | 100.0% |
| Host / Persistence / Worldgroup | 4 | 5 | 0 | 9 | 100.0% |
| Import / Export / Limits / Recovery | 2 | 2 | 0 | 4 | 100.0% |
| Items & Inventory | 4 | 2 | 0 | 6 | 100.0% |
| NPC & AI | 2 | 11 | 0 | 13 | 100.0% |
| Optional / Rankings / Extensions | 0 | 3 | 0 | 3 | 100.0% |
| Parties / Communication / Organizations | 10 | 5 | 0 | 15 | 100.0% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% |
| Quests / Scripts / Triggers | 0 | 2 | 0 | 2 | 100.0% |
| Rooms & World | 11 | 10 | 0 | 21 | 100.0% |
| Spells & Effects | 1 | 2 | 0 | 3 | 100.0% |
| Timers / Resets / World Rules | 6 | 9 | 0 | 15 | 100.0% |

## Remaining UNKNOWN assertions

None. Nahr's five message-8480 call sites pass verified empty attack-verb and weapon-name strings, exactly matching the native equipped-item-zero branch.
