# MajorMUD Parity Scorecard

- PASS: **200**
- STRONG: **0**
- UNKNOWN: **0**
- Total scored requirements: **200**
- Strict parity: **100.0%**
- Evidence-backed parity: **100.0%**

| Category | PASS | STRONG | UNKNOWN | Total | Strict |
|---|---:|---:|---:|---:|---:|
| Administration & Host | 2 | 0 | 0 | 2 | 100.0% |
| Character & Status | 46 | 0 | 0 | 46 | 100.0% |
| Combat | 18 | 0 | 0 | 18 | 100.0% |
| Commands & Help | 13 | 0 | 0 | 13 | 100.0% |
| Data Reconstruction | 3 | 0 | 0 | 3 | 100.0% |
| Economy & Services | 16 | 0 | 0 | 16 | 100.0% |
| Host / Persistence / Worldgroup | 9 | 0 | 0 | 9 | 100.0% |
| Import / Export / Limits / Recovery | 4 | 0 | 0 | 4 | 100.0% |
| Items & Inventory | 6 | 0 | 0 | 6 | 100.0% |
| NPC & AI | 13 | 0 | 0 | 13 | 100.0% |
| Optional / Rankings / Extensions | 3 | 0 | 0 | 3 | 100.0% |
| Parties / Communication / Organizations | 15 | 0 | 0 | 15 | 100.0% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% |
| Quests / Scripts / Triggers | 2 | 0 | 0 | 2 | 100.0% |
| Rooms & World | 21 | 0 | 0 | 21 | 100.0% |
| Spells & Effects | 3 | 0 | 0 | 3 | 100.0% |
| Timers / Resets / World Rules | 15 | 0 | 0 | 15 | 100.0% |

## CodeUpdate0369 Final STRONG -> PASS closure

- Before: **189 PASS / 11 STRONG / 0 UNKNOWN** (**94.5% strict**).
- After: **200 PASS / 0 STRONG / 0 UNKNOWN** (**100.0% strict**).
- The final 11 scored boundaries close from supplied WGSERV native artifacts and retained runtime evidence: SYSOP/Worldgroup access ownership, combat/spell owner census and classification, UPDATE_SPEED ownership correction, native HELP resolver/TOPICS behavior, player command-table census, casino/arena mechanics, TOP ranking mechanics, and Classic-vs-Reforged extension scope.
- Separately scoped native owner-local combat/spell formulas, branch ordering, RNG draw ordering, failure text, and casino terminal body mapping are **not** promoted by this scorecard; their fail-closed authorities remain explicit.
- Focused local gate `--smoke-classic-final-strong-closure` and cumulative gate `--smoke-classic-parity-closure` both pass.

## CodeUpdate0368 Timers / Resets / World Rules closure

- Before: **183 PASS / 17 STRONG / 0 UNKNOWN** (**91.5% strict**).
- Timers / Resets / World Rules: **15 PASS / 0 STRONG / 0 UNKNOWN** (**100% strict**).
- After: **189 PASS / 11 STRONG / 0 UNKNOWN** (**94.5% strict**).
- WCCMMUD-owned scheduling is exhausted as independent one-shot 1/3/5/5/30-second owners plus separately configured save callbacks; restart freshly arms callbacks and does not restore synthetic deadlines or catch up missed generations. Worldgroup simultaneous-callback arbitration and idle-poll wall-clock frequency are host-owned boundaries, not guessed MajorMUD rules.
- Ordinary Room.Delay semantics, room-entry/background respawn admission, narrow SYSOP RESET ROOM/AREA generation-counter ownership, WCCMMUTL rune-contest RNG/draw order, and all 44 visible WCCMMUD configuration prompts/ranges/load owners are exact at the recovered executable boundary.
- The focused local `--smoke-classic-timers-resets-world-rules` gate passes 22/22 and is included in the passing cumulative parity gate.

## CodeUpdate0367 Rooms & World closure

- Before: **176 PASS / 24 STRONG / 0 UNKNOWN** (**88.0% strict**).
- Rooms & World: **21 PASS / 0 STRONG / 0 UNKNOWN** (**100% strict**).
- After: **183 PASS / 17 STRONG / 0 UNKNOWN** (**91.5% strict**).
- Direct WGSERV RoomRec/WCCMMUD recovery closes key/door/gate access, SEARCH, trap movement/disarm/reset/presentation, the full room-description corpus, and the final map 12/2248 rug boundary.
- The legacy 12/2248 timer assumption is corrected rather than preserved: executable evidence proves state 8 persists because Para2=0; no 300-second reset is scheduled for that record.
- The focused local `--smoke-classic-rooms-world` gate passes 29/29 and is included in the passing cumulative parity gate.

## CodeUpdate0366 Character & Status closure

- Before: **169 PASS / 31 STRONG / 0 UNKNOWN** (**84.5% strict**).
- Character & Status: **46 PASS / 0 STRONG / 0 UNKNOWN** (**100% strict**).
- After: **176 PASS / 24 STRONG / 0 UNKNOWN** (**88.0% strict**).
- Native identity validation, Shadow LOOK precedence, formula-owner modifier ordering, player-vs-player level/EP/law admission, FORGIVE relation accounting, and guard/jail ownership are closed without promoting generic Reforged law/PvP policy as Classic.
- The focused local `--smoke-classic-character-status` gate passes 47/47 and is included in the passing cumulative parity gate.
