# MajorMUD Classic Parity Scorecard — Update 0320

- PASS: **97**
- STRONG: **90**
- UNKNOWN: **13**
- Total: **200**
- Strict parity: **48.5%**
- Evidence-backed parity: **93.5%**

| Category | PASS | STRONG | UNKNOWN | Total | Strict | Evidence-backed |
|---|---:|---:|---:|---:|---:|---:|
| Administration & Host | 0 | 2 | 0 | 2 | 0.0% | 100.0% |
| Character & Status | 24 | 20 | 2 | 46 | 52.2% | 95.7% |
| Combat | 12 | 5 | 1 | 18 | 66.7% | 94.4% |
| Commands & Help | 10 | 3 | 0 | 13 | 76.9% | 100.0% |
| Data Reconstruction | 0 | 3 | 0 | 3 | 0.0% | 100.0% |
| Economy & Services | 1 | 15 | 0 | 16 | 6.2% | 100.0% |
| Host / Persistence / Worldgroup | 4 | 1 | 4 | 9 | 44.4% | 55.6% |
| Import / Export / Limits / Recovery | 2 | 1 | 1 | 4 | 50.0% | 75.0% |
| Items & Inventory | 4 | 1 | 1 | 6 | 66.7% | 83.3% |
| NPC & AI | 1 | 10 | 2 | 13 | 7.7% | 84.6% |
| Optional / Rankings / Extensions | 0 | 3 | 0 | 3 | 0.0% | 100.0% |
| Parties / Communication / Organizations | 10 | 4 | 1 | 15 | 66.7% | 93.3% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% | 100.0% |
| Quests / Scripts / Triggers | 0 | 2 | 0 | 2 | 0.0% | 100.0% |
| Rooms & World | 11 | 10 | 0 | 21 | 52.4% | 100.0% |
| Spells & Effects | 1 | 2 | 0 | 3 | 33.3% | 100.0% |
| Timers / Resets / World Rules | 6 | 8 | 1 | 15 | 40.0% | 93.3% |

Pass 49 promotion: `character.death-xp-loss-exact` UNKNOWN -> STRONG. Native `_check_kill_user` proves ordinary non-final death preserves XP exactly. Native `_save_permanent_info` proves that the 10% arithmetic is final-death/reroll retention, selected as the greater of floor(total XP/10) and the stock half-level threshold plus one when both KEEPEXP gates are enabled. The ordinary-death runtime is live; final-character reroll transfer remains disconnected and prevents PASS.
