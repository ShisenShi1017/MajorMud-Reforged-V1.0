MAJORMUD PARITY SCORECARD
Strict proven parity: 69/172 = 40.1%
Evidence-backed coverage: 122/172 = 70.9%
Unresolved native dependency groups: 50/172 = 29.1%
Remaining-system shells: 36/36 implemented (reported separately; no parity credit)

Scoring rule: PASS = strict credit; STRONG = implemented/evidence-backed but not exact; UNKNOWN = blocks 100%. No half-credit is awarded.
The denominator is the current known requirement set and may grow when new native behavior is discovered.

BY SUBSYSTEM
- Administration & Host: PASS 0, STRONG 2, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Administration / Security / Builder: PASS 0, STRONG 0, UNKNOWN 2 (strict 0.0%, evidence 0.0%)
- Character & Status: PASS 18, STRONG 9, UNKNOWN 9 (strict 50.0%, evidence 75.0%)
- Combat: PASS 12, STRONG 5, UNKNOWN 1 (strict 66.7%, evidence 94.4%)
- Commands & Help: PASS 6, STRONG 1, UNKNOWN 2 (strict 66.7%, evidence 77.8%)
- Data Reconstruction: PASS 0, STRONG 3, UNKNOWN 0 (strict 0.0%, evidence 100.0%)
- Economy & Services: PASS 0, STRONG 9, UNKNOWN 3 (strict 0.0%, evidence 75.0%)
- Host / Persistence / Worldgroup: PASS 0, STRONG 2, UNKNOWN 7 (strict 0.0%, evidence 22.2%)
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

IMPLEMENTATION / DATA COVERAGE (not strict parity credit)
- Decode families: 39/39 connected
- Decode scalar fields: 2094/2096 connected/structural
- Economy services: 204 classified / 0 unclassified
- Native message catalog: 3869 messages loaded
- Runtime status effects: 111/111
- Spells classified: 1350/1350

NEXT NATIVE BLOCKERS
- native.administrative-policy: exact MajorMUD and Worldgroup administrator levels; exact command names and aliases; exact confirmation behavior; visibility to targets and normal players; native audit logging; rollback support; save timing; world-state mutation ordering; ban, kick, mute, freeze and module-lock ownership; builder versus sysop permissions; native remote-action authority
- native.security-permissions: exact MajorMUD player, sysop and builder privilege levels; Worldgroup authentication and authorization interfaces; native script and textblock execution privilege; remote-action authorization and caller identity; native save and file access boundary; cross-character and cross-world access rules; native command sanitization and rendering rules; audit log destination and retention; host/module trust handoff; exact unauthorized-action messages
- character.absolute-xp-cp-schedule: Absolute native XP thresholds and CP award schedule remain unresolved; CalcCPLevel is retained as a lead only and promotion continues to fail closed without evidence-promoted rows.
- character.cross-category-modifier-order: Exact race/class vs acquired ability vs equipment vs aura/status contribution ordering remains unresolved beyond explicit promoted mappings.
- character.death-xp-loss-exact: Death loses experience, but the exact native XP-loss formula, rounding, caps and ordering remain unresolved.
- character.native-lifecycle-boundary: Character runtime is exhausted except for native login/reconnect/link-dead/party/trade/offline-corpse lifecycle dependencies.
- character.picklocks-stock-formula: Retained Picklocks evidence contains conflicting formula branches; exact stock formula/branch ownership is not proven enough to activate.
- native.character-identity-policy: exact Classic name length and allowed characters; reserved-name list and duplicate comparison; title sources and precedence; alignment, class, level, guild, and administrative title tables; hidden and invisible identity rendering; disguise behavior; exact validation and failure messages; Worldgroup versus MajorMUD name ownership
- native.law-bounty-guards: exact crime families and witness rules; Evil Point and criminal-flag coupling; bounty arithmetic and limits; guard alert/summon/hostility behavior; arrest, jail, fines, confiscation, reputation, and reporting; crime decay and pardon authority; exact messages and persistence
- native.pvp-policy: exact Classic PvP availability and consent model; safe/restricted zone encodings; level and alignment restrictions; criminal and Evil Point interactions; party and guild restrictions; kill credit and death penalties; loot and corpse access; guard and bounty reactions; cooldowns and logout restrictions; exact commands and messages
- native.rest-inn-recovery-policy: exact Classic rest/sleep commands and messages; native recovery formulas and tick timing; inn rental persistence; safe logout room rules; status cleanup behavior; login-room behavior; Worldgroup versus MajorMUD ownership
- combat-spells.native-boundary: Combat/spells are non-native exhausted, but exact remaining native DLL/WCC/WBB formulas, branch semantics and ordering still block exact Classic parity.
- ... 38 additional unresolved native dependency groups

AUDIT DIAGNOSTICS
- Pass 12 static economy parity validator passed 409/409 checks.
- The .NET SDK is not installed in this environment; RuntimeMajorMudParityAudit and the C# build must be executed on the Windows build host for compiler/runtime confirmation.
