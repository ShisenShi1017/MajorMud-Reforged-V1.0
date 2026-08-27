MAJORMUD PARITY SCORECARD — UPDATE 0285

Strict proven parity: 71/176 = 40.3%
Evidence-backed coverage: 129/176 = 73.3%
Unresolved MajorMUD native dependency groups: 47/176 = 26.7%

Ownership rule: behavior proven to be solely Worldgroup-owned is excluded from the MajorMUD denominator. Pass 15 additionally credits independently proven subrules while leaving their unresolved parent boundaries UNKNOWN.

BY SUBSYSTEM
- Administration & Host: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Administration / Security / Builder: PASS 0, STRONG 0, UNKNOWN 2 (strict 0.0%, evidence 0.0%)
- Character & Status: PASS 20, STRONG 15, UNKNOWN 7 (strict 47.6%, evidence 83.3%)
- Combat: PASS 12, STRONG 5, UNKNOWN 1 (strict 66.7%, evidence 94.4%)
- Commands & Help: PASS 6, STRONG 1, UNKNOWN 2 (strict 66.7%, evidence 77.8%)
- Data Reconstruction: PASS 0, STRONG 3, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Economy & Services: PASS 0, STRONG 9, UNKNOWN 3 (strict 0.0%, evidence 75.0%)
- Host / Persistence / Worldgroup: PASS 0, STRONG 1, UNKNOWN 6 (strict 0.0%, evidence 14.3%)
- Import / Export / Limits / Recovery: PASS 0, STRONG 0, UNKNOWN 3 (strict 0.0%, evidence 0.0%)
- Items & Inventory: PASS 4, STRONG 1, UNKNOWN 1 (strict 66.7%, evidence 83.3%)
- NPC & AI: PASS 0, STRONG 9, UNKNOWN 2 (strict 0.0%, evidence 81.8%)
- Optional / Rankings / Extensions: PASS 0, STRONG 0, UNKNOWN 3 (strict 0.0%, evidence 0.0%)
- Parties / Communication / Organizations: PASS 1, STRONG 1, UNKNOWN 6 (strict 12.5%, evidence 25.0%)
- Protocol & Legacy Clients: PASS 11, STRONG 0, UNKNOWN 1 (strict 91.7%, evidence 91.7%)
- Quests / Scripts / Triggers: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Rooms & World: PASS 13, STRONG 3, UNKNOWN 6 (strict 59.1%, evidence 72.7%)
- Spells & Effects: PASS 1, STRONG 2, UNKNOWN 0 (strict 33.3%, evidence 100.0%)
- Timers / Resets / World Rules: PASS 3, STRONG 4, UNKNOWN 4 (strict 27.3%, evidence 63.6%)

PASS 15 CLOSURES / PROMOTIONS
- PASS: five-second graceful EXIT/X + automatic character save.
- PASS: REST 20-second activation and interruption contract.
- STRONG: Lawful x3 attack penalty plus Outlaw/Criminal/Villain/Fiend enforcement ladder and 300+ attack lockout.
- STRONG: realm PvP maximum-level-difference configuration and enforcement.
- STRONG: practice-arena no-EP/no-life-loss/Halls-of-the-Dead restoration contract, with exact room identity still name-correlated.
- EXCLUDED: raw reconnect/socket takeover remains Worldgroup host ownership and is not scored.

UNCHANGED HARD BLOCKERS
- character.death-xp-loss-exact remains UNKNOWN: exact normal-death XP loss formula/rounding/caps/order are not recovered.
- character.native-lifecycle-boundary remains UNKNOWN for MajorMUD-owned linkdead combat, party succession, trade escrow, and offline-corpse behavior.
- native.law-bounty-guards remains UNKNOWN for exact guard/bounty dispatch, arrest/jail/fines, witness logic and persistence.
- native.pvp-policy remains UNKNOWN for remaining consent/safe-zone/kill-credit/loot/cooldown/logout semantics.
- native.rest-inn-recovery-policy remains UNKNOWN for inn/sleep/safe-logout/login-room behavior beyond the now-proven REST contract.

NEXT MAJORMUD BLOCKERS
- native.administrative-policy: exact MajorMUD SYSOP command names and aliases; exact MajorMUD command confirmation behavior and target/player visibility; MajorMUD sysop-command audit logging where enabled by WCCMMUD.INI; MajorMUD save timing and world-state mutation ordering for sysop actions; native MajorMUD remote-action authority and exact failure messages
- native.security-permissions: exact MajorMUD command-key authorization checks at the Worldgroup key handoff; native script and textblock execution privilege inside MajorMUD; remote-action authorization and caller identity inside MajorMUD; MajorMUD save/file and cross-character game-data access boundary; native command sanitization/rendering and exact unauthorized-action messages
- character.cross-category-modifier-order: Exact race/class vs acquired ability vs equipment vs aura/status contribution ordering remains unresolved beyond explicit promoted mappings.
- character.death-xp-loss-exact: Death loses experience, but the exact native XP-loss formula, rounding, caps and ordering remain unresolved.
- character.native-lifecycle-boundary: Character runtime is exhausted except for native login/reconnect/link-dead/party/trade/offline-corpse lifecycle dependencies.
- native.character-identity-policy: exact Classic name length and allowed characters; reserved-name list and duplicate comparison; title sources and precedence; alignment, class, level, guild, and administrative title tables; hidden and invisible identity rendering; disguise behavior; exact validation and failure messages; Worldgroup versus MajorMUD name ownership
- native.law-bounty-guards: exact crime families and witness rules; Evil Point and criminal-flag coupling; bounty arithmetic and limits; guard alert/summon/hostility behavior; arrest, jail, fines, confiscation, reputation, and reporting; crime decay and pardon authority; exact messages and persistence
- native.pvp-policy: exact Classic PvP availability and consent model; safe/restricted zone encodings; level and alignment restrictions; criminal and Evil Point interactions; party and guild restrictions; kill credit and death penalties; loot and corpse access; guard and bounty reactions; cooldowns and logout restrictions; exact commands and messages
- native.rest-inn-recovery-policy: exact Classic rest/sleep commands and messages; native recovery formulas and tick timing; inn rental persistence; safe logout room rules; status cleanup behavior; login-room behavior; Worldgroup versus MajorMUD ownership
- combat-spells.native-boundary: Combat/spells are non-native exhausted, but exact remaining native DLL/WCC/WBB formulas, branch semantics and ordering still block exact Classic parity.
- commands.native-player-command-table: Exact native player command table, aliases, minimum prefixes, handlers and argument parsers remain a native dependency.
- native.help-reference: exact Classic MajorMUD help topic record families; native HELP syntax/aliases, formatting and pagination; MajorMUD textblock/help linkage and permission filtering; MajorMUD help search matching and ambiguity behavior
- ... 35 additional MajorMUD-owned unresolved dependency groups
