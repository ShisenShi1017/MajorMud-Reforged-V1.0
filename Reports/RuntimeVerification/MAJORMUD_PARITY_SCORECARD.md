# MajorMUD Parity Scorecard

- PASS: **167**
- STRONG: **33**
- UNKNOWN: **0**
- Total scored requirements: **200**
- Strict parity: **83.5%**
- Evidence-backed parity: **100.0%**

| Category | PASS | STRONG | UNKNOWN | Total | Strict |
|---|---:|---:|---:|---:|---:|
| Administration & Host | 1 | 1 | 0 | 2 | 50.0% |
| Character & Status | 39 | 7 | 0 | 46 | 84.8% |
| Combat | 16 | 2 | 0 | 18 | 88.9% |
| Commands & Help | 10 | 3 | 0 | 13 | 76.9% |
| Data Reconstruction | 3 | 0 | 0 | 3 | 100.0% |
| Economy & Services | 16 | 0 | 0 | 16 | 100.0% |
| Host / Persistence / Worldgroup | 9 | 0 | 0 | 9 | 100.0% |
| Import / Export / Limits / Recovery | 4 | 0 | 0 | 4 | 100.0% |
| Items & Inventory | 6 | 0 | 0 | 6 | 100.0% |
| NPC & AI | 13 | 0 | 0 | 13 | 100.0% |
| Optional / Rankings / Extensions | 0 | 3 | 0 | 3 | 0.0% |
| Parties / Communication / Organizations | 15 | 0 | 0 | 15 | 100.0% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% |
| Quests / Scripts / Triggers | 0 | 2 | 0 | 2 | 0.0% |
| Rooms & World | 14 | 7 | 0 | 21 | 66.7% |
| Spells & Effects | 1 | 2 | 0 | 3 | 33.3% |
| Timers / Resets / World Rules | 9 | 6 | 0 | 15 | 60.0% |

## CodeUpdate0364 NPC & AI closure

- Before: **161 PASS / 39 STRONG / 0 UNKNOWN** (**80.5% strict**).
- NPC & AI: **13 PASS / 0 STRONG / 0 UNKNOWN** (**100% strict**).
- After: **167 PASS / 33 STRONG / 0 UNKNOWN** (**83.5% strict**).
- Direct supplied-native/source/static verification: **32/32 PASS**.
- Native MonsGuards is ordered same-room spell-target substitution; Classic physical attacks and combat ownership no longer synthesize generic protector/assist behavior.
- Native fast/medium/slow generation ownership, one-pending-generation cap, active-slot cursors, slow HPRegeneration, medium three-move quota, monster-type roaming table, confusion and fast-follow retry boundaries are connected.
- Generic Reforged perception radius, threat scoring, low-HP flee and patrol-path AI remain explicit non-Classic extensions.
- Monster spell energy distinguishes pre-cast half-cost failure from post-payment rejection/full refund.
