MAJORMUD PARITY SCORECARD

Strict proven parity: 97/200 = 48.5%
Evidence-backed coverage: 177/200 = 88.5%
Unresolved native dependency groups: 23/200 = 11.5%
Remaining-system shells: 27/27 implemented (reported separately; no parity credit)

Scoring rule: PASS = strict credit; STRONG = implemented/evidence-backed but not exact; UNKNOWN = blocks 100%. No half-credit is awarded.
The denominator is the current known requirement set and may grow when new native behavior is discovered.

BY SUBSYSTEM
- Administration & Host: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Character & Status: PASS 24, STRONG 18, UNKNOWN 4 (strict 52.2%, evidence 91.3%)
- Combat: PASS 12, STRONG 5, UNKNOWN 1 (strict 66.7%, evidence 94.4%)
- Commands & Help: PASS 10, STRONG 1, UNKNOWN 2 (strict 76.9%, evidence 84.6%)
- Data Reconstruction: PASS 0, STRONG 3, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Economy & Services: PASS 1, STRONG 15, UNKNOWN 0 (strict 6.2%, evidence 100.0%)
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
- Timers / Resets / World Rules: PASS 6, STRONG 6, UNKNOWN 3 (strict 40.0%, evidence 80.0%)

PASS 39 CHANGE
- Promoted `character.native-lifecycle-boundary` from UNKNOWN to STRONG after separating proven MajorMUD graceful-exit/follower consequences from unsupported LINKDEATH and modern-server hypotheses.
- Retained exact five-second EXIT/QUIT delay with automatic save and native FOLLOW exit-stop-following behavior.
- Rejected the supplied `ActionDelay = WeaponSpeed - Agility/20 - Haste/10` priority queue because stock combat attack-energy scheduling is already PASS.
- Kept fixed three-minute LINKDEATH, same-user/IP reconnect, player.json serialization, party-leader successor, follower/charm cleanup, and exact cross-owner cleanup order unpromoted.
- Kept `character.death-xp-loss-exact` UNKNOWN: no level<10 gate, 10% formula, 2,000,000 cap, exact de-level count, or post-delevel pool ordering is promoted.
- Raw reconnect/socket/account mechanics remain excluded as Worldgroup host/session plumbing.
