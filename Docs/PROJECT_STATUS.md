# MajorMUD Reforged Project Status

- `SYSOP LIST LIMITED` now reports the authoritative live presence of generated limited items and their current room/user/owned-domain location. Definitions without an instantiated item are omitted, matching the user-confirmed rule that omission means the item is not presently in the world.

- Native WCC `.HSE` evidence closes the ordinary gang-house interior-location gap for 47 map-qualified rooms. After preserving ten stronger overlapping service bindings, generated Classic exposes 57 total house locations. The movement gateway now enforces persistent owning-gang membership, so native guardians bar non-members from entrances and interiors. Exact sundown event production and any house behavior not described by the retained files remain evidence-bound.

- Gang-house cleanup retention is connected to authoritative persistent ownership. Active, non-disbanded assignments protect contents in generated gang-house service locations; repossession clears ownership and removes that protection. The obsolete character-owner resolver was removed because room-ground items deliberately carry no owner.

- The gang-house transaction is connected to an explicit authoritative Classic Sundown event consumer: exact bank-gold debit, actual-payment timestamp, idempotent cycle key, rollback, and insufficient-funds repossession. The decoded catalog explicitly disables Classic day/night simulation until native schedule evidence exists, so automatic event production remains blocked without inventing a UTC cadence.

- Shop currency settlement now consumes decoded item `cost_type` for every generated item. All 1,950 records carry the field; buy and sell apply the five-denomination multiplier and fail closed on unknown types. Gang deeds price correctly in platinum or runic.

- Gang leaders can activate a carried parchment deed with `USE`. The runtime consumes the generated activation contract, grants its four exact items, emits message 2629, spends a charge, and persists exclusive gang-house ownership. Shop `cost_type` currency settlement and upkeep timing/payment remain open.

- All ten gang-house deed activation scripts are now represented in Librarium and generated Classic data: textblocks 9000-9009, message 2629, and 40 exact key/emblem/keyring grants. The runtime catalog rejects incomplete records. Live deed activation remains pending.

- Gang XP accrual covers all currently executable XP award routes: NPC combat, decoded ASK acquisitions, and generated TALK quest scripts. Awards use committed positive deltas only. Alignment reward catalogs are still projection-only and therefore do not create a second unverified award route. TOPTEN gang ranking remains live with visibility/disband filtering.

- Gang lieutenant promotion/demotion and one-time disband login notifications are connected to persistent state. Remaining gang parity includes earned-experience accrual, TOPTEN gang rendering, deed/house acquisition, keys/emblems, house access, customization, tax, guardians, and gang shops.

- Player gang lifecycle first slice is live: create, invite/uninvite (including offline targets), join, leave, roster/broadcast, and leader disband. Exact Classic command phrases and 100,000-experience/19-character creation gates are enforced. Promotion/demotion, gang experience accrual, rankings, login notification consumption, and deeds remain open.

- Gang runtime foundation is persistent and authoritative. LIST GANG and four administrative commands (DISBAND, DISABLE, ENABLE, GANGSIZE) are live; UNWRAP remains evidence-blocked. Player-facing gang creation, membership lifecycle, member login notification consumption, TOPTEN rendering, and deed/house acquisition are not yet connected. Classic regeneration, Release build, and SYSOP smoke 26/26 pass.

- SYSOP LIGHTNING contract corrected by user evidence: it is an administrative attack with no alignment-point mutation; LIGHTEVY restricts eligible targets only. Exact damage and presentation remain evidence-blocked.

- Correction: MajorMUD gang functionality is required; only guilds are absent. LIST GANG and the five gang administration roots remain blocked required work pending authoritative roster, rankings, size, progression, and notification persistence. The catalog no longer marks gangs non-applicable.

- WCCMMUD GOD inspection parity now includes INVENTORY and SPELLS for active characters. Results come from authoritative persistent item/spell state and generated catalogs, with item quantities/equipment/gold and spell levels/resource costs exposed. Unsupported GOD branches remain fail-closed. Clean Classic regeneration produced 34 families / 103,135 objects / 39 files; Release build and SYSOP smoke 23/23 pass.

- WCCMMUD GOD parity now includes active-target STATUS diagnostics and direct god-style SPEAK messaging in addition to TOPTEN controls. Both new routes enforce the native in-game character requirement; unsupported GOD routes remain fail-closed. Clean Classic regeneration produced 34 families / 103,135 objects / 39 files; Release build and SYSOP smoke 23/23 pass.

- WCCMMUD TOPTEN administration is connected end-to-end: authenticated GOD disable/enable commands persist the character flag and require an active target, while REPORT NOTOPTEN enumerates excluded saved characters. Other GOD operations remain fail-closed. Clean Classic regeneration produced 34 families / 103,135 objects / 39 files; Release build and SYSOP smoke 23/23 pass.

- WCCMMUD reporting parity advanced with live `SYSOP REPORT USERS [mask]`. Active account/character identities and world are reported, and the decoded optional 19-character mask filters local account ID or full character name case-insensitively. `REPORT NOTOPTEN` remains explicitly fail-closed pending authoritative TOPTEN participation state. Clean Classic regeneration, Release build, and SYSOP smoke 21/21 pass.

- WCCMMUD SYSOP parity advanced with live `SYSOP LIST USERS`, available to decoded GAMEOP/SYSOP authority and backed by active session plus persisted character state. Output includes account ID, character identity, map, room number, canonical room ID, and total users. LIST EVIL, GANG, and LIMITED retain explicit fail-closed blockers until their source state is authoritative. Clean Classic regeneration, server Release build, and SYSOP smoke 19/19 pass.

- Generated-runtime decode exhaustion now reports 39/39 fully connected families, zero invalid families, zero unresolved records, and zero disconnected fields across 104,198 generated records. Race-description native message numbers, symbols, and hashes remain in Librarium for source validation but no longer inflate runtime data; all 13 ANSI descriptions remain live. Compiler regression and clean Classic regeneration pass. The separate master retirement, complete board/Sysop/MajorMUD Plus parity, MegaMUD emulation, and immutable external archive requirements remain open.

- Closed the ASK success-output evidence boundary. Textblocks 9013/9026 are exact inline MDB empty strings (declared length 2), so successful acquisition is intentionally silent beyond textblock 9046's native messages 2639/2640. The generated two-record family and runtime loader now enforce that disposition. Classic regeneration remains 34 families / 103,135 objects / 39 files; Release build and Bundle A pass.

- The Perfect Stealth and Meditate acquisition disconnect is closed through the Classic `ASK` command. Exact decoded NPC/keyword routes, class and level gates, item 1351 turn-in, messages 2639/2640, 5,000-XP reward, one-time ability grants, and character persistence are runtime-consumed from one generated two-record family. Missing native payloads for success nodes 9013/9026 remain an explicit presentation-only evidence gap; no replacement text was invented. Classic regeneration reports 34 families / 103,135 objects / 39 files. Server Release build, Bundle A, character-foundation smoke, and F11 pass locally.

- the final numbered decoded ability family, 186-187, is runtime-closed at its evidence boundary. Neither ability has item/spell/NPC owners; both are persisted character capabilities. Perfect Stealth 186 now requires both the stored grant and one of the seven exact decoded class/minimum-level gates before Hide/Sneak bypasses ordinary percentile checks. Meditate 187 now requires both its stored grant and one of ten decoded class/minimum-level gates before focused mana regeneration begins; untrained characters can no longer meditate merely because the global parser recognizes the command. Existing ten-second meditate cadence and Perfect Stealth/See Hidden interaction remain authoritative. The live acquisition conversations referenced by textblocks 9029 and 9046 remain a separate recovery boundary because those source scripts are not present in the current generated executable script family. Release build, character foundation smoke, and the complete Bundle A character audit pass.

- decoded abilities 181-185 are connected to their authoritative families. Gang-house selectors now resolve through one generated catalog: 10 GHouseDeed owners map selectors 1-10 to the exact deed/house records, all 10 GHouseTax values remain attached to those deeds, 59 GHouseItem owners and 11 GShopItem controllers resolve to those same houses, and conflicting/unknown selectors fail closed. Persistent deed acquisition/upkeep mutation and ordinary interior ownership remain separate evidence gaps. BadAttk 185 is live for all 18 NPC owners; every positive value resolves to a generated item, is retained in living-NPC metadata, suppresses automatic aggression, and suppresses NPC combat-round attacks while the targeted character owns the exact item. It does not make the NPC invulnerable or invent broader faction immunity. Release build, Bundle H gang-house audit, and combat-profile promotion smoke pass.

- decoded ability family 165-180 is inventory-closed. Abilities 165-177 have zero generated spell/item/NPC owners, so no executor is invented. Shadowform 178 has two spells selecting native textblock 4157, `item.1579` (`death shroud`) selecting 9652, and NPC 384 carrying the zero marker. Spell and equipment selectors now reach authoritative character state and the live look/examine override path; both positive WCCTEXT2 payloads are absent and therefore fail closed until recovered. FindTrapsValue 179 and PickLocksValue 180 remain exact equipment-derived bonuses; PickLocks is live in door lock admission, while FindTraps stays trap-only and cannot be attached to general Search because no live decoded trap placement/command binding currently exists. Release build and Bundle B pass. Item smoke reaches the new Shadowform assertion and retains only its two pre-existing corpse equipment-restoration failures.

- Ability 157 (`ScatterItems`) is connected for all positive decoded relationships: three spells move current-room ground items to same-map room 1076, 2371, or 2379 with persistence and exact rollback. Nine zero-valued owners remain explicit non-mutating markers pending recovery of their native destination ranges. Ability group 155-164 is now inventoried and connected or truthfully fail-closed at its remaining evidence boundaries.

- Ability 155 (`DeathText`) is connected to the death-path evidence gate for its sole owner, tournament badge, and exact value 1643. It is not behaviorally executable yet: native textblock 1643 contains unrelated Salca-dart shop dialogue, proving that treating the value as a direct textblock ID would be wrong. The unresolved native namespace/dispatch is recorded fail-closed. ScatterItems 157 remains the last field in this group.

- Ability 159 (`KaiBind`) is complete for `spell.858`. The decoded zero-valued control marker prevents Kai martial powers: Punch, Kick, and JumpKick fail at initiation and remain suppressed if the status becomes active mid-combat, while hostile NPC actions continue. Remaining fields in 155-157 are DeathText and ScatterItems.

- Spell-side ability 158 (`ReqToHit`) is complete: `spell.307` projects defensive strike requirement 9 while active. Incoming physical attacks must match code 9; decoded ordinary NPC attacks do not and therefore miss. Spell immunity remains a separate generated field/path. Remaining 155-159 work is DeathText, ScatterItems, and KaiBind.

- Ability 161 (`OpenDoor`) is complete for `spell.750` (`wizard knock`). The room-targeted spell resolves one closed current-room door (direction/name/generated ID disambiguation), opens and unlocks both generated reciprocal states, persists the result, and rolls both sides back on failure. Remaining 155-159 work: DeathText, ScatterItems, spell-side ReqToHit, and KaiBind evidence/behavior.

- Ability 160 (`GiveTempSpell`) is complete for its one decoded owner. `spell.747` grants exact `spell.13` during its 30-second active effect, including reconnect-safe reconstruction and expiration cleanup without permanent spellbook mutation. The next 155-163 work remains DeathText, ScatterItems, spell-side ReqToHit, KaiBind evidence classification, and OpenDoor.

- Ability pass 155-164: `CastOnEnd%` (164) is complete for all three decoded owners (20%, 25%, 33%). EndCast chains now run immediately for duration-zero sources or at expiration for timed sources, with rollback on failed follow-up execution. Positive links without a chance field remain deterministic; zero links remain inert. Abilities 156, 162, and 163 have no decoded owners; the remaining active fields in this group require later family work.

- MonsGuards 146 closes exact NPC guard-assist relationships for 112 owners without faction inference. All targets resolve and living instances retain the relationship. Nine NPC ResistWater values are projected; no unsupported mapping to cold/acid is invented. Release build, Bundle B, and living world pass.

- Visible@Maint 154 is connected for all 16 non-gettable room fixtures, which now remain visible through cleanup. Remove@Maint 149 is not conflated with Del@Maint until its distinct lifecycle meaning is recovered. Release build and Bundle D cleanup pass; full item smoke retains two unrelated corpse restoration slot failures.

- TextBlock/Rune classification prevents unsafe generic execution. TextBlock has 74 native-resolvable distinct targets and 70 contextual control targets requiring dedicated recovery; Rune consists of four zero markers with no decoded placement ID. Counts and boundaries are regression-locked; Release build and Bundle B pass.

- EndCast 151 closure executes 77 decoded follow-up relationships at effect expiration and preserves 11 zero sentinels. Follow-up effects share authoritative spell behavior, persistence, transactional rollback, and safe chain depth handling. All targets resolve and no decoded cycle exists; Release build, Bundle B, and live powers pass.

- KillSpell 153 closure executes all five decoded removal relationships across three spells, with exact generated target resolution and atomic status synchronization. Release build, Bundle B, and live powers pass. EndCast remains separate follow-up-spell execution work.

- Ability inventory 145-154 confirms live ManaRgn and HealMana coverage and regression-locks exact decoded control-family ownership. TextBlock (180), EndCast (88), Rune (4), and KillSpell (3) remain dedicated script/lifecycle work rather than generic status modifiers; MonsGuards, ResistWater, Remove@Maint, and Visible@Maint owner counts are preserved for their subsystem passes.

- Ability closure 142-144 adds active spell HitMagic to persistent/equipment HitMagic in the combat profile, covering Combat Fury and Globe of Invulnerability. ClearItem has no decoded owners. All 318 NonMagicalSpell records retain their live general-dispel protection. Release build, player combat profile, and Bundle B pass.

- Fixed spell teleport closure activates all 12 complete positive ability 140/141 pairs. Live casts validate generated map-qualified rooms, move the intended active character transactionally, persist world/room state, and remove the instant helper effect. Bundle D teleporter audit and live-powers smoke pass; incomplete pairs remain fail-closed.

- Ability-family review 137-141 closes NPC SpellImmu: 387 decoded NPC thresholds are projected into living instances and enforced against incoming player spells, combined safely with temporary immunity. Shock was already connected across its item/spell owners. RoomVisible remains an evidence boundary, and fixed spell teleport execution is the next slice (12 complete map/room pairs; incomplete records remain quarantined).

- Decoded equipment level-range closure now consumes item abilities 135 (`MinLevel`) and 136 (`MaxLevel`) in generated `equipment.json` and live equip admission. The multicoloured staff (`item.1916`) is exact-level gated at 65. Compiler generation, Release build, and both focused Bundle C equipment audits pass. The full item smoke remains non-green only on its existing corpse resurrection slot-record/conflict checks.

- RemovesSpell ability 122 is connected across 107 spell definitions and all 334 decoded relationships. The 331 positive references resolve exactly and remove only matching active effects on the cast target; three zero sentinels do nothing. Instant cure/removal helpers cannot persist as empty permanent statuses, while timed replacement effects remain active after their authored conflicting spells are removed.

- Recharge ability 121 is connected for all 78 decoded owners. At each Classic cleanup, the 77 items with finite maximum-use capacity recover the exact decoded number of uses without exceeding that capacity; the same cleanup event is idempotent and includes carried, equipped, contained, and other surviving item state. Shadowy ankh remains fail-closed because its Recharge +3 has no finite capacity or cast relationship.

- StartMsg ability 120 is live for all 229 decoded spell owners. Verified WCCMSG2 slots now produce distinct caster, target, and same-room observer output after successful player-cast commit; failed casts and rollback paths announce nothing. Native `%s` placeholders receive ordered target or caster/target names, empty slots remain silent, and weapon spell procs use the same decoded caster presentation.

- Del@Maint ability 119 is live for all 460 generated item definitions at the Classic maintenance boundary. Cleanup deletes marked room-ground instances regardless of hidden state, retains ordinary room objects as hidden, and does not extend into character inventory, equipment, or other ownership domains. Recursive atomic removal now prevents a deleted portable container from leaving orphaned nested contents.

- Backstab damage range consumes all decoded BsMinDmg/BsMaxDmg relationships. Forty-seven item minimum relationships and 35 item maximum relationships remain independent, preserving negative values and incomplete pairs. All three spell owners are live: mantle of the woodsman adds +25 to both endpoints, while natural prowess and way of the cat contribute their exact zero values. Timed spell contributions expire through the shared status ledger.

- Backstab accuracy consumes every decoded BSAccu relationship. The 239 item owners retain 114 negative, 68 zero, and 57 positive values, while camouflage spell 1314 supplies the only timed spell value at +10. Equipment and active status values combine once in the stock halved BSAccu term; normal attack accuracy remains separate and expiration removes the spell contribution.

- DescMsg presentation is connected for all 539 decoded spell owners. Their 540 effective selectors preserve native player/secondary/tertiary WCCMSG2 channels for effect expiration, observer LOOK output on affected players or NPCs, and affected-character SCORE/STAT output. The two spells with repeated DescMsg relationships retain both; verified empty slots emit nothing; spell 776's zero selector is inactive; BCNS uses its evidence-backed 2531 correction.

- Item `%Spell` procs now honor decoded ordered relationships. The runtime scans forward from ability 114 to the next `CastsSp` 43 while preserving the stated percentage, recovering 127 distinct executable pairs from 131 `%Spell` rows instead of the prior 109 adjacent-only pairs. Caladbolg's duplicate identical row cannot double-trigger. AngelCrusher, skull flail, and tonfa have no subsequent spell relationship and remain safely inactive pending exact evidence.

- Spell alignment exclusion is live for the complete generated family: one NotGood spell, three NotEvil spells, ten Neutral-only spells, and no current NotNeutral spell owner. Casting shares the authoritative three-way reputation grouping already used by equipment and rejects a restricted, absent, or unknown alignment before target lookup, costs, cooldowns, effects, or world mutation.

- AffectsLiving is live for all 64 decoded ability-108 spells. Generated NonLiving ability 109 classifies 184 NPC definitions and is retained in compact metadata for normal spawns and summons. Direct and weapon-triggered spell delivery rejects those NPCs before resource or state mutation while continuing to admit player characters. The NPC parser now also consumes the generated boolean `undead` field correctly for all 106 undead definitions; NonLiving remains a separate category, with 83 non-undead members. Race/Class stealth ownership is still an explicit recovery gap because no generated race/class relationship proves the ability-102/103 owners.

- LoyalItem death retention is live for all 101 ability-100 item definitions. Combined with the two CURSED-only definitions and one overlap, exactly 103 items remain owner-held through death. Ordinary items still transfer to the corpse, and retained items do not corrupt corpse counts, limited-item reservations, or resurrection recovery.

- Spell alignment admission is live for every decoded Good/Evil relationship: 19 Good-only spells and 20 Evil-only spells. The caster's authoritative reputation is checked before target selection and before all resource, cooldown, effect, or world mutation. Shatter remains intentionally unavailable because its only retained Quality threshold description marks that interpretation as theoretical, so destructive inventory behavior is not guessed.

- Imported and decoded evidence is now permanently retained by user direction. Once 100% gameplay, board/Sysop/MajorMUD Plus, and MegaMUD emulation parity is proven, material not needed by the live server will be packaged into a local evidence ZIP and excluded from Git; it will not be deleted. No packaging/exclusion is authorized before that gate.

- Curse lifecycle is live for all 15 Curse-82 items, three CURSED-83 items, and both remove-curse/Rcrs-84 spells. Cursed equipment cannot be manually removed; CURSED items additionally remain with the owner through death rather than entering the corpse. Remove curse unequips every cursed item on its character target and commits the resulting inventory, equipment, weight, and derived-stat state.

- AffectsAnimals is live for charm animal through exact decoded classification. The generated NPC catalog identifies 155 Animal ability-78 definitions, and materialized NPCs retain that flag in compact runtime metadata. Charm animal accepts only those live NPC targets; characters and non-animal NPCs are rejected before any resource, cooldown, status, or world-state change.

- Percep is live for all 14 decoded spell relationships through hidden-search resolution. Active positive and negative timed values combine with the character's trained Perception and equipment ability-77 total, with rolled zero-parameter spells retaining their actual cast roll. The final search input cannot fall below zero. This pass does not alter NPC AI perception because the decoded spell family does not prove a replacement NPC acquisition rule.

- HoldPerson is live as movement-only immobilization for all 41 decoded spell relationships. The status prevents leaving the current room and suppresses active Dodge, but it is no longer mislabeled as paralysis and therefore does not remove physical attacks or spellcasting. Movement rejection occurs before the active-combat flee boundary, and Freedom remains the authoritative removal path for Hold Person/Entangle effects.

- DispellMagic now follows its decoded ability value across all 16 spell records. Eight value-zero definitions perform a general magical dispel while preserving explicitly nonmagical spell effects; four value-107 definitions remove blindness only; four value-19 definitions remove poison only. Targeted antidote and blindness cures no longer strip unrelated beneficial or harmful magic, and dispel casts cannot leave empty permanent helper effects.

- Spell immunity is live as an exact numeric level gate. Globe of invulnerability grants 9999 and ray of petrification grants 999; incoming spells at or below the active threshold leave no damage or duration effect. The rule covers player, proc, item, trap, NPC-hit, and room-area delivery while preserving normal cast costs and independent bystander checks. Higher-level spells are not blocked, and status expiration removes the threshold.

- BlindUser is live for all 20 decoded spells as a character-vision presentation state. Room views, look directions, examination, and searches reveal nothing while blinded and emit ANSI blindness output. Blindness does not prevent navigation by known commands or stop combat admission, and it adds no synthetic accuracy rule; spell-specific Accuracy/AC/Crits/damage relationships remain independently authoritative.

- Mute and MageBind now retain distinct decoded semantics. Four Mute spells prevent room/direct speech and NPC dialogue without suppressing casts. Three MageBind spells remain visible in live status evidence but intentionally do not block spellcasting because the only retained behavior description says that former behavior may no longer work. This removes the prior false shared `silenced` rule without inventing a replacement.

- Fear is live for all 14 decoded duration spells. If the affected character is engaged, the next combat resolution uses the existing authoritative flee cleanup before attacks occur. If the affected target is the live NPC in the encounter, the NPC releases that encounter and its engagement ownership. Fear does not fabricate room movement: neither side teleports or bypasses an exit, and ordinary non-combat commands remain separate from paralysis/silence rules.

- Confusion now follows decoded Classic behavior for all 84 spell records: each active effect supplies a 1-100 fumble chance, with zero parameters resolved from the retained cast roll. A successful check permits the combat action; a failed check consumes that action and emits the paired native ConfuseMsg payload when present. Seventy-seven selectors resolve exactly, including the native empty-output record, while seven unpaired effects remain presentation-silent rather than guessed.

- Per-cast status rolls are now persisted and reused for the duration of an effect, closing the prior midpoint approximation. Status-only spells no longer cause synthetic direct damage. Both AlterDRpercent/frail records modify the target's fully assembled damage resistance by their rolled signed percentage and revert on expiration/removal.

- Freedom is live for all eight generated spells. It removes active holding/entangle effects and all spells altering movement or attack speed through HoldPerson, Slowness, and Speed abilities, while preserving unrelated status state. AlterDRpercent remains explicitly pending until the rolled percentage can be retained in the active-effect snapshot without approximation.

- Timed maximum-resource and encumbrance effects are live: 4 MaxMana spells, 1 maximum-HP spell, and 9 encumbrance spells. The status ledger owns reversible application, refresh, expiration, persistence, and safe current-resource clamping. Spell encumbrance participates in the same live attack-cost and quick-critical path as equipment encumbrance.

- HealMana is connected for all 26 generated spell relationships. Fixed positive/negative parameters and zero-parameter effect rolls retain their decoded distinction. Persistent characters update bounded mana; hostile NPC effects update the bounded casting-energy resource already used by NPC spell attacks and resistance refunds. Three `mana dead` weapon procs now drain that live NPC resource instead of failing at the target boundary.

- Timed spell mana regeneration is live for all 26 ManaRgn relationships. Spell percentages stack with equipped-item percentages and feed both passive and meditation mana ticks through the existing decoded cadence/formula authority. Debuffs can suppress regeneration to zero, and the shared status lifecycle reverses the modifier on expiration or removal. HealMana remains the next bounded resource-effect family because hostile NPC mana ownership is not yet proven.

- Damage-shield retaliation is connected for all eight item sources and all six spell sources. Each source retains its decoded damage amount and remains independently attributable. Five spell sources plus every item use exact Shock WCCMSG2 player/observer slots. Chaos shield 405 correctly applies its spell-owned DamageShield 10 but has no retaliation output because no Shock selector exists. The sole Shatter/Quality relationship is still quarantined rather than guessing destructive inventory behavior.

- Decoded weapon spell effects are live for all 109 evidence-complete item pairs. The ordered item ability vector requires `%Spell` immediately followed by `CastsSp`; every linked generated spell resolves as a single-hostile-target effect. Successful weapon hits/criticals roll the exact percentage and apply through shared spell/living-world state. Twenty-two unpaired markers remain quarantined, Shock remains presentation metadata for damage shields, and Shatter remains blocked pending exact Quality comparison evidence.

- Magical-creature physical admission is live for 386 NPC Magical requirements and eight nonzero NPC ReqToHit codes. Runtime consumes 636 nonzero item Magical ranks, 31 HitMagic ranks, and five ReqToHit weapons. HitMagic reduces the creature requirement before weapon Magical is checked; exact ReqToHit mismatches, bare hands, and martial attacks fail before combat mutation unless HitMagic fully bypasses the separate Magical requirement.

- Mystic martial combat is live through `punch`, `kick`, and `jumpkick` (`jk`). Decoded class capability gates prevent unlearned attacks, abilities 89-94 apply only to their corresponding attack, martial strikes use the unarmed range rather than a wielded weapon, and the existing native resolver supplies Punch/Kick/Jumpkick's exact 0%/33%/66% pre-DR scaling. Command authority passes at 100 registrations and 46 aliases.

- Alignment-sensitive equipment is now live. All eight Damage Shield items retaliate only after a successful positive-damage physical hit; Protection from Evil/Good augments armor class against the matching decoded attacker group; and the six positive/negative alignment eligibility markers are enforced from authoritative character reputation. Equipment removal recalculates these values, and active spell versions use the same combat paths.

- Classic REST and MEDITATE now execute with decoded activation/cadence behavior, mutual exclusion, combat blocking, and action cancellation. Passive regeneration remains a separate 30-second path. Command authority passes at 97 registrations, 45 aliases, and all contextual verbs active.

- Passive character regeneration is live at the decoded 30-second cadence. HP and mana amounts use the retained MMUD Explorer formulas, equipment percentage modifiers apply universally, and active-session scheduling prevents immediate-login or duplicate-session ticks. REST/MEDITATE's 20-second state bonus is still separate.

- Equipment resource closure now applies decoded maximum HP, maximum mana, and encumbrance changes from every applicable weapon, wearable, and cloak. Repeated commits cannot accumulate bonuses, and removal restores base maxima safely. Regeneration modifiers remain projected but intentionally do not tick until the exact Classic cadence is established.

- Defensive equipment closure now routes Magic Resistance (36), Anti-Magic (51), and poison immunity (21) from all equipped items into the existing spell and poison resolution authorities. Equipment removal recalculates and removes the effects. Release server build and focused item/combat verification pass.

- Universal equipment modifier consumption now covers weapons, wearable items, and cloaks through the same decoded ability-ID projection. Maximum Damage, Stealth, Dodge, Perception, and Backstab damage modifiers now reach live combat/search alongside the already connected defensive, accuracy, critical, regeneration, resistance, Find Traps, and Picklocks paths. Release build and focused equipment/combat regressions pass.

- Blacknight item 1316 is connected as the decoded level-40 Thief reward from Textblock 4310. New cloaks carry five uses; `use blacknight` casts spell 1220 on the owner without adding it to the spellbook, applies Picklocks +200 for 15 combat rounds, decrements one use, and retains the cloak. Equipped Picklocks +20 and the timed bonus both participate in live lock gates. Focused item, power, and combat gates pass.

- Hooded man NPC 1000 now consumes self-cast spell 1220 as a persisted living-NPC status: Picklocks +200 for 15 combat rounds, with refresh, expiration, save/restore metadata, and death cleanup. The value is available to future proven NPC actions; autonomous door picking remains evidence-blocked because no decoded route/action binds this NPC to a lock.

- Plague-crafter NPC 361's spell 446 is connected end-to-end: one Full Area cast applies 4-10 damage and, to survivors, a 30-combat-round effect carrying Dodge -10, Armor Class -5, and Maximum Damage -4. The shared status lifecycle persists, refreshes, projects, and expires all three modifiers. Focused combat/live-powers verification passes.

- Stone-sphinx spell 686 is connected end-to-end: generated Textblocks 2510-2515 drive the live 25% branch, native message sequence, two Map 12 teleports, fall/fire damage, final character persistence, and combat release. The runtime fails closed before movement on invalid generated data and rolls location back if persistence fails. Focused live-combat smoke passes.

- Sphinx spell `686` now has an exact compact Librarium/generated script chain recovered from the structured MDB decode: textblock `2510`, random table `2511`, and four 25% branches `2512-2515`. All branches retain native messages `1960-1963`, their exact two-stage Map 12 teleport destinations, and fall/fire-pit casts `689`/`688`. Compiler and generated-data verification pass. Live combat does not execute this newly generated chain yet; that is the next bounded runtime pass.

- NPC spell target type `1` now uses its original `Self` meaning. All six generated combat uses are protected from accidental player mutation: five stone sphinx casts of spell `686` and one hooded man cast of spell `1220`. Their NPC-side payloads remain explicitly blocked rather than invented: textblock `2510` is malformed/truncated in current generated evidence, and the hooded man's temporary Picklocks modifier has no proven NPC action consumer. Release build and focused combat gates pass.

- Decoded NPC spell target type `11` is now live as its proven `Full Area` scope. NPC `361` plague-crafter's spell `446` executes one cast against persistent characters in the room, applying its exact `4-10` untyped, never-resisted direct damage with normal persistence and lethal corpse/death resolution. The decoded Dodge, AC, and Max Damage duration modifiers remain fail-closed until their exact type-0 effect lifecycle is connected. Release build and focused combat gates pass.

- Decoded NPC target type `12` area-hostile spells are now live room-wide. One selected-target combat tick owns the cast; every other persistent character in the same room receives independent resistance, immunity, damage/status persistence, and lethal death/corpse handling. Single-target spells remain isolated, and only their resisted casts receive the decoded half-energy refund. Release build, live-round, admission, and combat-completion verification pass.

- The final 735/736 native NPC combat-format boundary is now exhaustively documented. Message `8480` remains partial for five NPC `1006` Nahr attacks because every retained source omits its caller-owned miss verb and body-part/weapon arguments. No values were guessed. A separate confirmed runtime disconnect remains for decoded target type `12`: area-hostile NPC spells currently apply only to the engaged character rather than one authoritative room-wide target set.

- Native NPC combat formatting is fully exact for 735/736 reachable WCCMSG2 formats. Physical body-part rows are 780/785 exact; type-2 spell rows are excluded from impossible physical misses. IDs 2939 and 8310 are closed. Only ID 8480 remains partial for five Nahr attacks whose retained data contains hit prose/anatomy but no exact miss verb/body-part argument pair.

- Decoded type-2 NPC attacks now execute as spells rather than malformed physical attacks. Their encoded spell ID is resolved through generated/canonical spell data, physical double damage is suppressed, resistance/effect/status mechanics run once, and exact native cast messages receive the resolved spell damage.

- Native body-part dodge coverage is now 786/822 executable rows after strict duplicate-definition consensus recovery. Eight missing argument pairs were recovered from identical NPC-name/slot/hit-message definitions with unanimous values; conflicting candidates remain rejected. Thirty-six rows across IDs 2939, 8310, and 8480 still lack unique evidence. Fully exact native format coverage remains 733/736.

- Native NPC combat wording is fully exact for 733/736 WCCMSG2 formats. All five type-2 spell/song formats now resolve their spell name from the decoded spell-ID slot and use the correct target-facing native payload. Only IDs 2939, 8310, and 8480 remain partially fallback-bound because 44 executable attack rows have no retained miss verb or weapon/body-part name.

- Native NPC combat behavior now reaches 728/736 fully exact WCCMSG2 formats. Runtime consumes decoded miss verbs and weapon/body-part names for 778/822 executable five-token dodge rows; IDs 806, 2490, and 8372 are complete. Forty-four rows under IDs 2939, 8310, and 8480 remain on safe fallbacks because their retained evidence has no verb or weapon name. Five spell/song formats remain blocked on original caller-owned names.

- Native NPC combat outcome behavior now reaches 725/736 exact WCCMSG2 formats. IDs 1 and 66 are verified native no-output payloads and execute silently without fallback prose or empty ANSI lines. Eleven IDs remain bounded by missing decoded caller arguments: six body-part dodge formats and five spell/song formats. Directly self-contained native formats pass at 719/736, with six more resolved from paired-hit context.

- Native NPC combat wording now reaches 723/736 exact WCCMSG2 outcome formats. Six generic armor-deflection/attack-dodged formats recover their action phrase from the same decoded attack row's paired hit message. The remaining 13 are explicitly bounded: two verified no-output payloads, six body-part dodges missing the native body-part argument, and five spell/song formats missing original spell-name/caller ownership. No verbs or body parts were hard-coded or guessed.

- Native NPC combat wording is connected for 717 of 736 generated WCCMSG2 outcome templates with proven formatting arguments; all 736 IDs resolve and 735 are reachable from executable attack rows. The remaining 19 IDs are two verified empty/no-output payloads plus generic verb/body-part and spell/song formats that lack their original call-site arguments in the generated outcome record. They retain safe fallback prose; no substitutions were guessed.

- Live combat output is locally repaired: scheduled rounds wake idle Telnet sessions, serialized network writes prevent output/echo corruption, and the complete combat presentation plus HP prompt is ANSI-colored. Combat admission, full round-resolution, and completion/death/reward gates pass after the change.

- Bundle F description attachment is locally closed: clean generation now includes 13 exact native race-help payloads attached by canonical race number/name and evidence symbol, alongside complete direct-description coverage for 1,100 NPCs, 1,950 items, and 26,668 rooms. F2A static/dynamic and F1 authority audits pass with zero description disconnects. Exhaustion remains 37/37 connected with zero pending/disconnected fields; generated runtime is 38 files / 84,050,435 bytes. This does not approve removal of decoded/imported evidence.

- Full Classic parity/source-retirement work continues under the corrected board-capable standard. The reproducible baseline inventories 3,650 source/reference files (262,726,339 bytes), including 32 native-data files, 8 MajorMUD Plus files, 54 board house/state files, 1,957 MegaMUD automation references, and 1,531 OmniVerse public-reference files. The corrected generated-data audit checks 104,176 records / 1,974 scalar paths with zero disconnected fields and reports 28 connected families / 9 pending-unresolved families / zero disconnected families. Required `client_projection.json` is structural-only; all 111 status effects, all 178 shops, and 26 room-bound banks carry truthful runtime-authority/evidence labels. The blank `bank.2` sentinel remains pending. Current generated Classic runtime is 32 gameplay families / 37 files / 83,974,298 bytes. Retirement is not approved: remaining record markers, board/Sysop/MajorMUD Plus parity, known gameplay blockers, and the immutable external archive requirement remain.
- Generated-field consumption closure is locally green. Native-message payloads retain the live header/payload/order data needed for all 3,869 messages while physical-offset arrays remain Librarium-only; rooms retain 26,668 definitions and 61,976 generated exits without embedded decoded-slot copies; hidden item visibility remains live through `hidden_searchable`; 20 Sysop roots and six invasion definitions pass without duplicated prose/path notes; all 1,100 NPC HP fields have exact runtime-consumer proof.
- Runtime character data retains all 13 races and 15 classes after duplicate extraction print segments were removed. Runtime commands retain all 25 decoded room verbs after duplicated room-sighting indexes were removed. The consolidated F11 command gate passes with 95 live registrations, 42 aliases, contextual `eq`/`ba` routing, and one live dispatch authority; no standalone command-authority sidecar was recreated.
- The eight Classic correction contracts remain immutable Librarium compiler input but no longer form a redundant runtime family. Clean compiler regression proves their projected effects remain authoritative: Bard shop percentage floors at 10, NPC 1086 awards 10,000 XP, four masks equip on FACE, and the Lyrists Companion item/spell binding remains generated.
- Decoded spell 1055 now consumes required WCCMSG2 hooks 2520/2531 for exact successful-cast and effect-expiration output. Derived NPC spell counts and duplicate local fix annotations are excluded from runtime while all 1,100 decoded NPC HP values remain live. Bundle E validates the compact relationship catalog derived from 962 generated spawn records rather than requiring redundant retired sidecars; all 13 NPC subsystems pass.
- MajorMUD Plus has native local evidence (`WCCMMPLS.DOC/.h/.mcv/.MDF/.msg/.REL` and `wccmmpls.dll`) covering character descriptions, registries, life purchase, offline action editing, `/mudreg`, and sysop approval/management. User correction reclassifies `code_of_twinks_78` as the configured WCCMMUD SYSOP unlock/key, not a separate addon. Native WCCMMUD evidence confirms that `SYSOPKEY` grants the in-game `SYSOP ...` command surface and `GAMOPKEY` grants a subset. Reforged must enforce the equivalent through authenticated Sysop/Administrator roles and explicit capabilities; the plaintext legacy key must not become a player command.
- MajorMUD Plus now compiles as the 32nd Classic family and has authoritative server consumers for descriptions, registry editing/search/lookup, life purchase and charges, authenticated sysop approval/deletion, persistent action editing and live custom action execution, confirmed data reset, and visible/hidden contest lifecycle controls. Focused smoke passes 15/15 and compiler regression passes 107,182 records across 43 Librarium families. Exact contest rune placement/ranking remains evidence-blocked because rune IDs and the native distribution/reward algorithm are not present in the decoded documentation.

- Map 17 boulder failure execution is locally complete: textblock 4235 renders message 3309, casts generated spell 1324, applies decoded level-scaled fire damage, and persists character HP. Compiler.Tests builds cleanly and passes 107,181 records across 42 families.

- Native message/output closure passes locally: generated Classic data contains 3,869 WCCMSG2 records, all active dialogue and interaction presentation references resolve, and the focused audit reports zero disconnects.

- CodeUpdate0139 economy/service closure is locally complete with 205 classified services and zero unclassified records. Live buying uses decoded markup; restocking uses decoded time, quantity, percent, and stock limits.

- CodeUpdate0139 spell/status/effect closure is locally complete: 1,350 spells, 105 decoded ability IDs, and 111 runtime-authoritative status effects have explicit execution-family and runtime-consumer coverage with zero unclassified records.

- CodeUpdate0139 remoteaction corpus closure is locally complete: 139/139 decoded invocations and 85/85 signatures are map-qualified and executable from generated `room_interactions.json`; native audit and relevant smoke gates pass.

This is the current live handoff for Codex, ChatGPT, and human project work.

## Mode

- Active mode: Codex local repository work.
- Repository: `ShisenShi1017/MajorMud-Reforged-V1.0`.
- Local path: `E:\MajorMud-Reforged Github`.
- Current user request: apply CodeUpdate0139 handoff work locally and verify; Codex is not pushing unless explicitly asked.

## Current Baseline

- Current Code Update: `0139`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0139 - Telnet Runtime Cohesion`.
- Milestone 1: complete and frozen.
- CodeUpdate0137: complete and folded into the local baseline.
- CodeUpdate0138 status: green by user report.
- CodeUpdate0139 local status: runtime cohesion, room-description recovery, final native closure/WCC decode archive intake, verified/strong Librarium activation, Librarium app dashboard, Bundle H baseline/final audit capture, and server/native-runtime audit repairs applied and locally verified.
- User-confirmed room-action vocabulary: all 25 clue-harvest verbs are evidence-gated active contextual commands. The candle trainer route is now closed: in `room.map1.2252`, all three decoded candle aliases traverse `exit.map1.2252.n` to `room.map1.2254`. The runtime double-gate evaluation that blocked valid text commands was removed, and exact teleporter plus living-world tests pass.
- Fresh F11 verification reports 95 registrations, 44 aliases, and all 25 user-confirmed contextual room-action verbs active with no failures.
- World-contract marker closure reconciles all three generated Classic/custom/AI world definitions with the live server world projection. Exhaustion is now 30 connected / 7 pending families with zero disconnected fields/families.
- Ability-marker closure identifies all 13 prior ability markers as exact `Unknown` public-reference description placeholders, not executable decoded values. The compiler omits only these non-values from generated runtime output while preserving Librarium evidence and meaningful descriptions. All 188 abilities pass compiler regression and the 14-check live actors/abilities smoke. Fresh exhaustion is 31 connected / 6 pending families with zero disconnected fields/families; runtime is 37 files / 83,983,193 bytes.
- Learned-skill identity closure reconciles all 13 skill records with their authoritative runtime purpose: validating persisted character skill IDs and enforcing separation from numeric quest abilities. Generated metadata now states that identity authority explicitly. Empty acquisition relationships remain unchanged and fail closed, so no trainer/effect semantics were inferred. Compiler regression, clean generation, Release build, and Bundle A pass. Fresh exhaustion is 32 connected / 5 pending families with zero disconnected fields/families; runtime is 37 files / 83,984,064 bytes.
- Character-foundation marker closure removes one evidence-only unresolved lawful-target penalty note from generated runtime while preserving it in Librarium. The executable eight-reputation evil-point table remains authoritative and regression-protected. Clean generation, Release build, and character creation/save/restore smoke pass. Fresh exhaustion is 33 connected / 4 pending families with zero disconnected fields/families; runtime is 37 files / 83,983,886 bytes.
- Loot-rule closure repairs an actual compiler omission: the verified Classic integer-percent drop rule now generates beside the older evidence catalog. Live NPC kill drops read and validate the generated 1..100 inclusive range and `roll <= drop_percent` condition. Non-runtime explanatory fields remain only in Librarium. Compiler regression plus Bundle C loot, loot materialization, and combat completion pass. Fresh exhaustion is 34 connected / 3 pending families with zero disconnected fields/families; runtime is 37 files / 83,987,651 bytes.
- Bank compaction removes the explicit `bank.2` / `Leave this blank` sentinel from generated runtime while retaining it in Librarium. Classic now generates exactly 26 evidence-backed room-bound bank/vault services. Compiler regression, Release build, full economy coverage, and live deposit/withdraw smoke pass. Fresh exhaustion is 35 connected / 2 pending families with zero disconnected fields/families; runtime is 37 files / 83,986,084 bytes.
- Economy-rule marker closure replaces stale pending/unscored labels on 22 decoded shop-type/service relationship indexes with truthful runtime-authority and service-evidence classifications. The Bard 10% pricing floor remains its separate executable rule. All 23 rules pass compiler regression, full economy coverage, and live services smoke. Fresh exhaustion is 36 connected / 1 pending family with zero disconnected fields/families; only NPC markers remain. Runtime is 37 files / 83,987,052 bytes.
- NPC marker closure removes 45 exact `Unknown` sentinels from generated MMExplorer printable-segment fallback arrays while preserving meaningful text and Librarium evidence. All 1,100 NPC definitions, 962 spawns, and all 13 Bundle E subsystems pass. Decode exhaustion now reports 37/37 connected families, zero pending/unresolved families, zero disconnected families, and zero disconnected fields. Runtime is 37 files / 83,986,062 bytes. This is generated-data marker closure, not authorization to delete decoded/imported evidence; the retirement ledger remains NO. A separate missing generated Human race-description authority blocks the broad F2A static-description audit.
- Current compact Classic projection: 37 top-level runtime files / 83,983,674 bytes. The refreshed retirement ledger still forbids removal of decoded/imported evidence.
- Latest local server build: PASS for `MajorMud.Reforged.sln` Release with warnings-as-errors after Bundle H final gate repairs.
- Latest local final gate: PASS 63/63 Bundle H audits; all final smoke segments pass after item-runtime, command-alias, living-world, and player-combat-profile repairs; published server EXE passes Classic data, item runtime, live gameplay, living-world, player combat profile, and telnet listener startup on `127.0.0.1:4000`; published app EXE passes shell/settings/cleanup smoke routes and brief GUI launch/startup check after desktop logger hardening.
- GitHub Actions corrective fix: generated `Data/Classic` is no longer tracked in Git; the workflow now restores/builds the Librarium compiler and regenerates Classic runtime data before server bootstrap diagnostics.
- Clean generated-data repair: compiler output now includes the Librarium-backed `textblock_command_ability_contracts` family, generated JSON readers support chunk manifests, and the runtime derives NPC AI spawn relationships from generated `spawns.json` when optional `npc_ai_runtime.json` is absent.
- Latest disconnect pass: exact decoded stone-sphinx answer chains now bind the Great Pyramid upward exits at rooms `12/1920` and `12/2001`; 193/206 action-gated exits are fully bound and the remaining 13 fail closed.
- Ten previously closed Map 17 boulder exits are now bound to exact decoded `move|push boulder`, `delay 5`, `testskill strength 60 4235`, and remote-action records.
- The `room.map9.1012` rubble route now executes its decoded `testskill strength 0 1423` roll for all nine exact aliases and activates `remoteaction 1012 1840 0 0` only on success.
- The four decoded Map 17 pedestal scripts now consume their exact diamond, pearl, moonstone, and marble items and bind `exit.map17.3042.n`.
- NPC `550` owns command map `textblock.2530`; exact answers `stars`, `star`, and `the stars` route through 2531/2532 to `remoteaction 2051 0 0 8`, binding `exit.map12.2051.u`.
- The living-world audit now binds all 206/206 action gates with zero native/passive placeholders and zero ordinary action-semantic gaps.
- Static room-item reconciliation now generates and materializes 363 shared-world placements across 287 rooms: 102 visible and 261 hidden-searchable. Room rendering, pickup, visible re-drop, Search, and living-world persistence are connected. Seven BBS-only Sysop-room rows and one invalid item-zero row are rejected.
- Static room-item reset is explicit and idempotent at the host maintenance boundary: missing ordinary placements restore once even when another copy is character-owned, existing ground placements do not duplicate, and decoded positive item `game_limit` values block restoration only when the global world-plus-owned count is already at the limit. No global wall-clock schedule or unlinked dedicated item timer is guessed.
- Local limitation: Python validator could not be rerun in this Codex process because `python`/`py` are not accessible.
- GitHub Actions status for CodeUpdate0139: pending until pushed and confirmed green.
- Guardian Hall engineering department is implemented locally across phases 1-10 with persisted sessions/tasks/memory/reports/statistics, isolated workspaces, guarded build/validation/runtime commands, repository analysis, diagnosis, and cumulative candidate ZIP packaging.
- Current Guardian Hall policy: planning, engineering, build, validation, runtime/gameplay testing, optimization, documentation, candidate ZIP generation, repository updates, and overnight execution are independently controlled. Candidate ZIP generation defaults on; autonomous and repository-update capabilities default off. Emergency Stop cancels execution without changing saved preferences.
- Borin can be assigned a persisted free-form Engineering Objective or the `Fix Runtime Disconnects` preset, which queues only the work authorized by the corresponding capability switches.
- Autonomous repair now uses The Forge's selected LM Studio model to author a unified diff from captured diagnostic evidence. The diff is validated/applied only to the engineering worktree; subsequent gates determine whether the resulting cumulative ZIP is suitable for operator review.
- Guardian Hall and the full solution pass Release compilation with warnings-as-errors after generated shop markup was connected to the existing native economy markup authority and strict nullable diagnostics were resolved.

## Architecture Rules

- Librarium is the only gameplay authority.
- `/Data` is generated runtime output.
- Clean checkouts must regenerate `/Data` from Librarium before server/runtime bootstrap validation.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- `ImportedDatabases/` remains untouched unless the user explicitly approves a reviewed cleanup.
- Preserve real `Saves/`; do not commit smoke-save artifacts.

## CodeUpdate0139 Local State

- Telnet sessions route through the authoritative runtime command dispatcher.
- Live room rendering uses living-world NPC instances, players, ground items, exit state, and stat line.
- Combat targeting now selects visible live NPC instances instead of static spawn definitions.
- Combat round damage now updates shared living-world NPC state.
- Spell/ability NPC damage and healing now update shared living-world NPC state and synchronize active combat encounter HP.
- Movement now checks the live combat guard before traversal.
- Classic runtime audit tooling now reports compiler output counts, runtime load counts, room inspection details, and spawn materialization.
- Latest audit finding: regenerated `Data/Classic/rooms.json` has 26,668 rooms with descriptions and 0 rooms without descriptions.
- Latest audit finding: live-world materialization creates 1,015 live NPC instances and 628 live monster instances after hard map-qualified placement validation. All 35 former unresolved spawn labels are reconciled without changing population: 28 exact `no_spawn` records use their generated NPC names and seven decoded monster-group-9 references remain grouped rather than materializing all 30 candidates.
- CodeUpdate0139 room-text recovery work now adds a compiler-side native map text resolver, `audit-room-text`, and `export-room-text`.
- Librarium now contains 2,417 binary-backed room-description records promoted from clean available native map DAT evidence and proved against required map 1 probes such as `room.map1.2141`.
- Librarium also contains 800 lower-priority OmniVerse map-qualified reference gap-fill room descriptions for canonical rooms not already covered by native evidence.
- Librarium now contains 23,309 WCCMP002 default-install binary gap-fill room descriptions recovered from `ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat`.
- Librarium now contains 140 additional WCCMP002 placeholder room descriptions recovered from decoded native offsets; WBBMP002 corroborates all 140 titles and first description lines.
- Compiler room-text projection merges all room-description files with native/binary evidence taking priority; current compiler audit resolves 26,668/26,668 room descriptions.
- Room-description reference recovery report: `Reports/DataDecode/room_description_reference_recovery.md`.
- WCCMP002 room-description recovery report: `Reports/DataDecode/wccmp002_room_description_recovery.md`.
- WCCMP002 placeholder room-description recovery report: `Reports/DataDecode/wccmp002_placeholder_room_description_recovery.md`.
- Read-only WCC exhaustion reporting now records current source coverage at `Reports/DataDecode/wcc_librarium_exhaustion_audit.md`.
- MMExplorer SQL/MDB and OmniVerse public codex reconciliation now records structured reference-backed gap fills at `Reports/DataDecode/mmexplorer_omniverse_librarium_reconciliation.md`.
- Matched item, spell, NPC, and shop records now include MMExplorer and OmniVerse reference blocks; unmatched MMExplorer records are retained as supplements; OmniVerse abilities are now represented as a new Librarium family.
- Live `Data/Classic` was regenerated from Librarium after the room-description promotion and now audits with complete room-description coverage.
- WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this recovery pass. WBBMP002 remains secondary comparison evidence only.
- CodeUpdate0139 command abbreviation work now uses a centralized command registry with exact aliases, unique-prefix resolution, and ambiguity rejection for unsafe prefixes such as `s`, `se`, and `st`.
- Live spawn placement now requires exact map-qualified room resolution. Legacy numeric room fallback was removed so unresolved spawns are logged and skipped instead of materializing in arbitrary rooms.
- Live room rendering now consumes only room-scoped living NPC instances and colors by runtime disposition/alignment instead of treating every high-HP NPC as hostile.
- Focused placement audit for `room.map1.21` proves one visible neutral NPC from `spawn.21.5`, with `sourceRoom=room.map1.21`, `resolvedRoom=room.map1.21`, and `liveRoom=room.map1.21`; no storm giants, woodelf entries, bosses, or unrelated group candidates are visible there.
- Current generated evidence for `room.map1.21` resolves the occupant as `guardsman` (`npc.14`) from `(Max 1): 14,[5-1-1-1]`. The user-expected `elite guardsman` remains a separate evidence-backed Librarium/data correction if the source record is wrong.
- Current hard placement validation skips two classified invalid/non-spawn records: `spawn.288.43` points to non-existent `room.map1.288`, and `spawn.2306.213` points to `room.map1.2306`, which the user verified is a shop with no NPC.
- Current generated Classic data after compaction: 26,668 rooms, 61,976 exits, 397 room interactions, 83 live NPC quest-entry triggers, 182 executable stage scripts, and 83 executable quests. The 2,100 pending quest candidates, 284 duplicate room/exit analysis triggers, and tooling script catalog remain in Librarium evidence only.
- Runtime consolidation now emits 33 gameplay families, 103,127 primary objects, and 38 top-level files totaling 84,852,759 bytes. Equipment is a specialized 914-record projection covering all 383 generated weapons; `eq <item>` and `equip <item>` execute through the same authoritative engine. Proven non-runtime authoring metadata, duplicate reference twins, analysis records, derived toll display blocks, and unbound offset-message policies remain Librarium-only while authoritative gameplay fields remain live.
- The remaining room native/passive effect linkage gaps are closed: 7 distinct native/passive effects across 12 exit rows now compile as `native_passive_effect_resolved` with no promoted player command.
- MegaMUD `/mp` route replay was used for path evidence against the affected map-qualified exits and found 0 command hits, so no command binding was guessed.
- Native WCCMSG room-action, door, hidden-exit, lock/key, search, and failure strings are cataloged as 117 unique offset-backed message records in Librarium and generated `/Data`.
- Room native effect/message report: `Reports/DataDecode/room_native_effect_linkage_and_message_decode.md`.
- Quest dialogue dispatch intake from `MajorMUD_Reforged_Cumulative_Update_0206.zip` is loaded into Librarium as current script/trigger source records.
- Generated `Data/Classic/scripts.json` now contains exactly 182 executable decoded textblock stage scripts; the tooling catalog remains Librarium-only.
- Generated `Data/Classic/triggers.json` now contains 367 trigger records, including 83 NPC quest-entry triggers and 182 linked stage script references.
- Runtime NPC dialogue dispatch is connected to `talk <npc>` through room-scoped live NPC lookup, generated NPC quest-entry triggers, decoded stage-order script evaluation, atomic local mutation, and fail-closed rollback.
- Runtime quest state commands expose generated `journal`, `quest`, `accept`, `abandon`, `complete`, and guarded `quest reset` state markers. Classic completed quests cannot be repeated on the same character; replay requires starting a new character.
- Generated random textblock tables execute through decoded independent 1..100 threshold semantics.
- Generated quest world-intent targets are validated for cast, summon, teleport, and remoteaction before commit.
- Quest teleport intents update the active character/session room; summon intents materialize generated NPCs into the living world.
- Native quest remoteaction closure is locally verified: `1423.66.0.3` is the sole generated quest-stage signature and is executable with zero evidence-gated signatures. Corrected native argument ownership identifies action slot 3; WCCMP002 proves its reciprocal type-7/state-2 door link between rooms `9/1423` west and `9/1425` east. Runtime toggles and persists both sides, rejects malformed arity/action slots, and journals the exact native result state.
- Room/object interaction commits now emit durable `world-object-interacted` living-world events.
- Final native closure/WCC decode archive intake is applied: 1,673 new files copied into the repo, native decode/Librarium evidence added, `Native*` registry source files compile, and 241 user-confirmed server implementation slices were merged into active runtime code with duplicate contracts consolidated instead of leaving parallel copies.
- Existing differing files skipped during archive intake are listed in `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Verified/native and strongly-correlated Librarium records now flow into executable generated `/Data` where evidence supports activation. `room_interactions` compiles from the active base records plus strongly-correlated sidecars, generated `triggers` contains 367 strong records including 83 NPC quest-entry triggers, and compiler cross-family validation blocks unresolved exit targets.
- The Reforged app Librarium workspace is now a readable read-only Classic data browser with Weapons, Armor, Spells, Abilities, Classes, Races, Items, Monsters, Quests, and Rooms tabs, selected-tab search, name-only left-side record lists, character-foundation class/race gameplay details, and nontechnical quest guidance showing who to talk to, what to say, needed items, and plain-language steps.
- The Reforged app Client workspace is now oriented around the MegaMUD-style play flow: choose a server/session in Play, open a dedicated black terminal window, and keep client settings/tools in menus or a separate tabbed options window.
- Telnet character creation now prompts for Male/Female before final character build, saves the selected sex on the character record, and keeps older saves backward-compatible as `Unspecified`.
- Compiler room-description loading now recognizes both `*_records.json` and `*_records_*.json` source files, so the WCCMP002 room-description corpus projects into generated `Data/Classic`.
- Runtime quest dialogue and item catalog readers now accept generated integer-compatible numeric strings for quest arguments and item economy/field values, fixing the local JSON `String` versus `Number` server smoke crash.
- Current Classic runtime audit counts: 26,668 rooms loaded, 26,668 room descriptions loaded, 1,100 NPC definitions, 1,078 monster definitions, 962 spawn records, 1,015 live NPC instances, 628 live monster instances, 1,648 doors, 178 shops, 27 banks, 44 trainers, 83 executable quests, 83 live quest-entry triggers, and 182 stage scripts.
- Bundle H final verification counts: generated/runtime room coverage is now 26,668 rooms with 26,668 descriptions; living-world smoke reports 962 generated spawns, 61,976 exits, 367 generated interactions, 1,015 runtime NPC instances, 1,932 world objects, 17 classified gate kinds, and the classified nonblocking `npc-ai-catalog-invalid` signal.
- Decode Exhaustion Bundle G Revision 0002 is installed locally and closes the prior Bundle F native presentation payload gap. `message 2044` and `message 3290` resolve through exact WCCMSG evidence, verified no-output decoded text nodes are mapped explicitly, unknown message IDs fail closed, and local F10 native payload plus F10 complete through F15 audits pass.
- Native presentation data is now reproducible from Librarium: 3,467 verified WCCTEXT2 payloads compile into one runtime file, and a clean Classic build reports 30 families, 101,731 objects, and 35 files.
- Bundle H baseline is recorded at `Reports/BundleH/BASELINE_RUNTIME_CONSOLIDATION_AUDIT.md`. Current gate logs are preserved under `Reports/BundleH/CurrentGate/`. Server and compiler builds pass with warnings-as-errors. Bundle A character, Bundle D search, door, corrective, environment, and final audits now pass after adding `search` to the supported command catalog, aligning search audit semantics with transient reroll/replace behavior, scoping door validation to door/object failures, separating Bundle A evidence-name-only warnings from true generated-unconsumed disconnects, and promoting the two imported WCC room-anchor environment records that were absent from the MMExplorer-derived environment catalog.
- Bundle D final now passes locally with `AuditPassed=true` and `BundleDComplete=true`. D9 now reports 26,668 runtime rooms and 26,668 decoded environment records. `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` are backed by imported WCC room records and source-classified as `decoded_imported_wcc_room_record_anchor`. D9 explicitly asserts both anchor records and their decoded values. Neither room is a gang house, so gang-house environment rows remain 0.
- Bundle E zero-disconnect audit now passes locally with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths. Focused companion, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service closure audits also pass.
- Bundle H gang-house service-location audit is recorded at `Reports/BundleH/CurrentGate/bundle_h_gang_house_audit.json`. Native/default WCC gang files, ten MudInfo gang-house maps, ten deed items, 22 gang-house/deed shops, and deed/upkeep cleanup protection are verified. Librarium now contains `Librarium/Objects/gang_houses/gang_house_records.json`, generated Classic data contains `Data/Classic/gang_houses.json`, and runtime consumes the generated catalog through `RuntimeClassicGangHouseCatalog` plus `RuntimeClassicGangHouseProtectionService`.
- Gang-house service-location scope is 10 Classic houses, 20 WCC shop API-backed service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted locations resolve to runtime rooms and current/expired/non-gang-room protection checks pass. Normal interior house ownership coverage and live deed acquisition/upkeep mutation remain blocked until exact evidence is recovered. `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, MudInfo maps remain reference-only, and `LibrariumLocal/core_graph.json` plus `LibrariumLocal/runtime_index.json` are absent from the repo.
- Bundle H baseline blockers from the first sweep are cleared locally. GitHub Actions remains pending until CodeUpdate0139 is pushed and confirmed green.
- Remaining native decode gaps are recorded at `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md`.
- Native runtime evidence policy for this milestone: proven and strongly-correlated native behavior should be wired into executable Classic runtime; exact native behavior that remains unproven, such as unresolved remote mutation semantics, stays evidence-gated and must not be guessed.
- Classic completed quests cannot be reset or restarted on the same character.
- One-time room semantic and trigger/script/quest validations are accepted and recorded in `ValidationManifest.json`.
- Latest audit report: `Reports/RuntimeVerification/classic_runtime_audit.md`.
- `Saves/` and `ImportedDatabases/` remain preserved.

## Verification

Latest local verification:

```text
python Tools\validator.py
```

Result:

```text
PASS 13/13
All current-health validator checks passed.
Living-world smoke PASS with hard placement audit signal.
Focused room audit PASS for room.map1.21: one neutral room-scoped visible NPC and no group-5 leakage.
Classic audit report written at Reports/RuntimeVerification/classic_runtime_audit.md.
Librarium room-text promotion contains 2,417 native-backed descriptions, 800 reference-backed descriptions, 23,309 WCCMP002 binary-backed gap fills, and 140 WCCMP002 placeholder room recoveries; compiler smoke and live Data/Classic output emit 26,668/26,668 descriptions.
Room native/passive effect closure PASS: 12/12 target exits resolved, 0 old unbound statuses remain, and 117 unique room message records compile.
Quest dialogue dispatch PASS: 182 decoded stage scripts, 83 NPC quest-entry triggers, 182 linked stage script references, branch dispatch, item consumption/reward transition, and failure/no-mutation behavior.
Quest/script edge closure PASS: random table execution, cast/summon/remoteaction coverage, world-intent target validation, Classic non-repeat quest guard, and exhaustive NPC quest-entry dispatch validation.
Remoteaction and room/object event recording PASS.
Final native closure archive intake PASS; server/test builds and single validator PASS after active merge of implementation slices, duplicate contract cleanup, room-description catalog resolver repair, and generated numeric-string runtime reader repairs.
Single validator PASS.
Compiler smoke PASS: 99,366 records across 37 families.
Reforged app Release build PASS; app publish PASS to `Release/MajorMudReforged/MajorMudReforged.exe`.
Reforged app startup smoke PASS after the MegaMUD-style terminal-window UX repair.
Server Release build PASS after Telnet character sex-selection repair.
Bundle H server build PASS with warnings-as-errors.
Bundle D search audit PASS.
Bundle D door audit PASS.
Bundle D corrective audit PASS.
Bundle A character audit PASS.
Bundle D environment audit PASS with 26,668/26,668 decoded environment rows.
Bundle D final audit PASS with `AuditPassed=true` and `BundleDComplete=true`.
Bundle E companion lifecycle audit PASS.
Bundle E merchant/trainer closure audit PASS.
Bundle E boss/death/respawn closure audit PASS.
Bundle E NPC item/control/persistence closure audit PASS.
Bundle E relationship/service closure audit PASS.
Bundle H current gate PASS for the focused runtime audits with logs under `Reports/BundleH/CurrentGate/`.
Bundle H gang-house service-location audit PASS with `RuntimeGameplayReady=true` for the generated WCC shop API service-location catalog; full live deed acquisition/upkeep mutation and ordinary interior house ownership coverage remain evidence-blocked.
Bundle E zero-disconnect audit PASS with `BundleEComplete=true`.
```

GitHub Actions is not claimed green until the pushed commit is verified.

## Next Step

Push CodeUpdate0139 when approved and wait for green GitHub Actions before beginning the next public update.

## Documentation Cleanup Policy

The repository uses a compact active doc surface. Do not recreate old per-update handoff/changelog/manifest trees unless the user explicitly asks.

Current live continuity should be kept here and mirrored briefly in:

- `AGENTS.md`
- `AI_CONTEXT.md`
- `README.md`
- `VERSION.md`
- `CHANGELOG.md`
- `ReleaseNotes.md`
- `ProjectManifest.json`
- `UpdateManifest.json`
- `Docs/Architecture/PROJECT_ROADMAP.md`

Historical detail should stay in structured evidence/report data when it is still needed, not in duplicate root or Docs handoff chains.
- One-time validation proofs are recorded in ValidationManifest.json.

## Client Terminal Presentation Update
- The dedicated gameplay terminal now uses the MegaMUD Classic layout as its default presentation: six-menu command bar, compact auto-option toolbar, Conversations list, black terminal, command entry, blue status strip, and optional Reforged map/items/character side tools.
- The modern additions are layered around the terminal and can be hidden; they do not replace the text-first MegaMUD workflow or move gameplay ownership into the Client.
- Static XAML and event-handler validation passed in the delivery environment. A fresh Windows .NET build remains required after installation.
## WCCMMUD SYSOP command status

CodeUpdate0139 now generates and consumes all 20 decoded online SYSOP/GAMEOP command roots through authenticated account roles. Six safe roots are executable and the remaining roots are explicit, non-mutating blockers. Exact invasion syntax/payload, five GOTO destinations, native gang state, and several command owner services remain required before behavioral SYSOP parity can be claimed. See `Reports/RuntimeVerification/WCCMMUD_SYSOP_COMMAND_CLOSURE_0139.md`.

GOTO/invasion recovery is closed: NewHaven, SilverMere, Rhudaur, Khazarad, Lost City, and Support GOTO destinations are live and persistent; all six exact invasion phrases and payloads are decoded and executable.

All six invasion phrases are live generated room interactions. The 25 placement children and six NPC-mixture tables were recovered exactly from WCCUPDA2, reproducibly promoted to Librarium, generated into one compact runtime family, and executed through authoritative living-world summons. Only authenticated SYSOP/Administrator accounts in the Support Chamber may activate them. Each consumes its decoded one-use gate item; board cleanup removes surviving invasion NPCs and restores the gates.
Latest local continuation: abilities 123-124 are audited as a group. HPRegen is connected for 25 items and 16 spells. NegateAbility is connected for five spell and two NPC owners with exact positive target IDs; the ambiguous zero target remains inert. Release server build, item smoke, and Bundle B combat audit pass locally.
Latest local continuation: grouped abilities 125-134 are complete as persistent quest-progression state. All ten exact identities persist with provenance and cannot become player powers. Character Bundle A and the complete 83-quest/182-script narrative smoke pass locally.
