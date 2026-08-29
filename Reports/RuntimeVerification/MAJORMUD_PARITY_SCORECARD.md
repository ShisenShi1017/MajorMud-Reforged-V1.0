# MajorMUD Parity Scorecard

- PASS: **126**
- STRONG: **74**
- UNKNOWN: **0**
- Scored assertions: **200**
- Strict parity: **63.0%**
- Evidence-backed parity: **100.0%**

| Category | PASS | STRONG | UNKNOWN | Total | Evidence-backed |
|---|---:|---:|---:|---:|---:|
| Administration & Host | 0 | 2 | 0 | 2 | 100.0% |
| Character & Status | 36 | 10 | 0 | 46 | 100.0% |
| Combat | 12 | 6 | 0 | 18 | 100.0% |
| Commands & Help | 10 | 3 | 0 | 13 | 100.0% |
| Data Reconstruction | 0 | 3 | 0 | 3 | 100.0% |
| Economy & Services | 6 | 10 | 0 | 16 | 100.0% |
| Host / Persistence / Worldgroup | 4 | 5 | 0 | 9 | 100.0% |
| Import / Export / Limits / Recovery | 2 | 2 | 0 | 4 | 100.0% |
| Items & Inventory | 4 | 2 | 0 | 6 | 100.0% |
| NPC & AI | 7 | 6 | 0 | 13 | 100.0% |
| Optional / Rankings / Extensions | 0 | 3 | 0 | 3 | 100.0% |
| Parties / Communication / Organizations | 12 | 3 | 0 | 15 | 100.0% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% |
| Quests / Scripts / Triggers | 0 | 2 | 0 | 2 | 100.0% |
| Rooms & World | 12 | 9 | 0 | 21 | 100.0% |
| Spells & Effects | 1 | 2 | 0 | 3 | 100.0% |
| Timers / Resets / World Rules | 9 | 6 | 0 | 15 | 100.0% |

## Remaining UNKNOWN assertions

None.

## CodeUpdate0344 native organization persistence/services promotion

- Before: **125 PASS / 75 STRONG / 0 UNKNOWN**.
- After: **126 PASS / 74 STRONG / 0 UNKNOWN** (**63.0% strict**).
- Promoted: `native.organizations.persistence-services`.
- Native 0344 verifier: **204/204 PASS** against the retained original WCCMMUD.DLL.
- Daily `mcurou` world/deed cleanup, exact `CLEARITEM 0`, decoded gang-house healer/guard/key/emblem/rubbish services, native `ADDDELAY` command queuing, and type-11 player-managed gang-shop STOCK/UNSTOCK/MARKUP/LIST/BUY/SELL ownership are now executable end-to-end.
- Global Btrieve/MKDE durability, general communication presentation, and generic non-gang service gaps remain separate STRONG assertions.

## CodeUpdate0342 native gang modal + transient invitations

- Parity remains **125 PASS / 75 STRONG / 0 UNKNOWN** (**62.5% strict**).
- Native 0342 verifier: **85/85 PASS** against the retained original WCCMMUD.DLL.
- Worldgroup state `0x88` now owns interactive `DISBAND GANG`, with exact Y-token confirmation/cancel semantics and restoration to state `0x82`.
- Gang invitations now use the recovered process-local target+gang registry instead of persisted JSON; exact duplicate, cleanup, JOIN-consumption, and restart-lifetime behavior are connected.
- Explicit `DISBAND PARTY` now routes through native `_stop_following(user,-1)` inside the already-PASS party owner.
- `native.organizations.persistence-services` remains STRONG because automatic gang-house sundown event production/cadence and remaining embedded house-service behavior are not yet owned end-to-end; communications presentation remains separately gated.

## CodeUpdate0341 native gang + communications edge recovery

- Parity remains **125 PASS / 75 STRONG / 0 UNKNOWN**.
- Native 0341 verifier: **66/66 PASS** against the retained original WCCMMUD.DLL.
- Communication profile defaults/serialization, TELEPATH polarity, MAX* byte/counter semantics, gang offline-disband drainage and cleanup-time gang deletion/name reuse are now executable-evidence bounded.
- No broad assertion was promoted because exact Worldgroup page/telegram presentation and remaining gang confirmation/invitation-storage edges are still meaningful.

## CodeUpdate0340 native party-coordination promotion

- Before: 124 PASS / 76 STRONG / 0 UNKNOWN.
- After: **125 PASS / 75 STRONG / 0 UNKNOWN**.
- Strict parity: **62.5%**.
- Promoted: `native.party-coordination`.
- Direct WCCMMUD.DLL ownership now closes the five-slot/six-character model, invitation slot reservation and full-slot quirk, FOLLOW/JOIN acceptance, exact party ranks, bare LEAVE versus actual departure, ordinary follower movement and failed-follow cancellation, special-movement non-propagation, and target type 12/13 party filtering.
- CodeUpdate0339 lifecycle evidence supplies exact QUIT/disconnect/death group cleanup with no successor election.
- `native.organizations.persistence-services`, communication persistence/limits, and `native.save-coordination` remain STRONG.

## CodeUpdate0339 native character/session lifecycle promotion

- Before: 123 PASS / 77 STRONG / 0 UNKNOWN.
- After: **124 PASS / 76 STRONG / 0 UNKNOWN**.
- Promoted: `character.native-lifecycle-boundary`.
