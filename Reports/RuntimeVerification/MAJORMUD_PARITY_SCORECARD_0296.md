# MajorMUD Parity Scorecard — Update 0296

- PASS: **73**
- STRONG: **76**
- UNKNOWN: **29**
- Total scored requirements: **178**
- Strict parity: **41.0%**
- Evidence-backed: **83.7%**

## Pass 25 changes

- `native.doors-locks-access`: UNKNOWN → **STRONG** after separating explicit PICKLOCK/BASH actions from traversal and retaining the unresolved native success roll as an evidence gate.
- `native.environmental-hazards`: removed from Classic parity scope; stock room records have no generic environmental-hazard record family.
- `native.traps`: remains **UNKNOWN**; DISARM TRAP command identity is proven/routed, but detection/disarm/state-transition formulas are not.

## Categories

| Category | PASS | STRONG | UNKNOWN | Total | Strict | Evidence-backed |
|---|---:|---:|---:|---:|---:|---:|
| Administration & Host | 0 | 2 | 0 | 2 | 0.0% | 100.0% |
| Character & Status | 20 | 15 | 7 | 42 | 47.6% | 83.3% |
| Combat | 12 | 5 | 1 | 18 | 66.7% | 94.4% |
| Commands & Help | 6 | 1 | 2 | 9 | 66.7% | 77.8% |
| Data Reconstruction | 0 | 3 | 0 | 3 | 0.0% | 100.0% |
| Economy & Services | 1 | 15 | 0 | 16 | 6.2% | 100.0% |
| Host / Persistence / Worldgroup | 0 | 1 | 4 | 5 | 0.0% | 20.0% |
| Import / Export / Limits / Recovery | 0 | 0 | 3 | 3 | 0.0% | 0.0% |
| Items & Inventory | 4 | 1 | 1 | 6 | 66.7% | 83.3% |
| NPC & AI | 1 | 10 | 2 | 13 | 7.7% | 84.6% |
| Optional / Rankings / Extensions | 0 | 2 | 1 | 3 | 0.0% | 66.7% |
| Parties / Communication / Organizations | 2 | 3 | 2 | 7 | 28.6% | 71.4% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% | 100.0% |
| Quests / Scripts / Triggers | 0 | 2 | 0 | 2 | 0.0% | 100.0% |
| Rooms & World | 11 | 9 | 2 | 22 | 50.0% | 90.9% |
| Spells & Effects | 1 | 2 | 0 | 3 | 33.3% | 100.0% |
| Timers / Resets / World Rules | 4 | 5 | 4 | 13 | 30.8% | 69.2% |
