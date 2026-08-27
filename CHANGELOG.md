## CodeUpdate0313 - Classic world-rules/configuration boundary

- Promotes `native.world-rules-configuration` from UNKNOWN to STRONG.
- Retains the exact 44-prompt/default `WCCMMUD.mcv` setup catalog as native configuration evidence.
- Keeps Reforged free of `WCCMMUD.INI`; the native INI remains evidence only, not a Reforged runtime dependency.
- Connects six exact defaults already consumed by live Classic timing, PvP, and Evil-Point owners: 60s Player save, 300s Buffer save, PvP difference 100, EP/day 18, EP cycle 60m, EP/cycle 1.
- Does not infer native ranges or live-reload/restart behavior from Reforged's generic typed world-rule service.
- Leaves remaining setting owner/effect mappings unresolved until native evidence closes them.
- Parity: **97 PASS / 83 STRONG / 20 UNKNOWN = 200** (48.5% strict / 90.0% evidence-backed).

## Pass 41 / Update 0312 — Classic gameplay failure boundary

- Promotes `native.error-handling-recovery` from UNKNOWN to STRONG.
- Ordinary rejected Classic gameplay actions remain state-preserving no-ops; no rollback/disable/world-load stop is introduced.
- Separates catastrophic wrong/corrupt native-file startup failure from ordinary gameplay rejection.
- Keeps exact failure wording, malformed post-startup reference behavior, and catastrophic save rollback ordering UNKNOWN.
- Parity: 97 PASS / 82 STRONG / 21 UNKNOWN = 200; 48.5% strict, 89.5% evidence-backed.

# CodeUpdate0311 / Pass 40 — Classic identity / Shadow LOOK boundary

- Promoted `native.character-identity-policy` UNKNOWN -> STRONG from the existing native first-name/UserRec anchors plus the retained Shadow ability presentation contract.
- Connected equipped positive ability 9 (`Shadow`) to Classic `LOOK <character>` for other players.
- Black Embroidered Robes (`item.1331`) are retained as `Shadow +100`; LOOK now masks the target heading as `A shadowy figure` and emits `A shadowy figure!`.
- Preserved self LOOK without masking.
- Deliberately left the normal room occupant list using the real character name because the retained Shadow description is LOOK-scoped and does not prove room-list masking.
- Kept the user-provided 3-15 letters-only name grammar, reserved-name list, title precedence and invisibility presentation as hypotheses only.
- Advanced parity to **97 PASS / 81 STRONG / 22 UNKNOWN = 200** (48.5% strict / 89.0% evidence-backed).

# CodeUpdate0310 / Pass 39 — Classic lifecycle / death hypothesis verification

- Verified the supplied combat, LINKDEATH/logout, and death-XP pages as hypotheses rather than native authority.
- Rejected the proposed ActionDelay priority queue because the existing stock attack-energy scheduler is already PASS.
- Promoted `character.native-lifecycle-boundary` UNKNOWN -> STRONG from exact 5-second graceful exit + automatic save and explicit follower-exit stop-following evidence.
- Excluded raw reconnect/socket/account mechanics as Worldgroup plumbing; kept MajorMUD-owned unexpected-disconnect consequences unresolved.
- Kept exact death XP loss UNKNOWN; no level-10 exemption, 10% formula, 2,000,000 cap, de-level count, or pool-recalculation order is guessed.
- Advanced the current parity ledger to 0310: **97 PASS / 80 STRONG / 23 UNKNOWN = 200**.

# CodeUpdate0308 / Pass 37 — Classic REST / MEDITATE Regeneration Correction

- Corrected Classic player HP regeneration to the stock MajorMUD `/750` base with minimum one; the prior `/500` path was the GreaterMUD branch.
- Corrected REST to multiply the stock HP base by 3 before applying HPRegen percentage.
- Corrected types 1-4 MEDITATE mana regeneration to return the recovered base before MPRegen percentage; passive mana still applies MPRegen.
- Preserved Kai/type 5 as the recovered one-point MPRegen branch.
- Retained 30-second passive, 20-second REST, and 10-second MEDITATE cadence evidence plus direct-native 20-second activation/interruption behavior.
- Promoted `native.rest-inn-recovery-policy` UNKNOWN -> STRONG while keeping exact host-clock phase, simultaneous tick ordering, exact MEDITATE presentation, and remaining cleanup ordering unresolved.
- Advanced the current parity ledger to 0308: **97 PASS / 78 STRONG / 25 UNKNOWN = 200**.
- Added Pass 37 evidence registry, smoke route, focused static validation, and cumulative current-ledger regression updates.

# Changelog

## CodeUpdate0307 / Pass 36 — Classic character modifier-order verification

- Treated the operator-supplied modifier-order screenshots as hypotheses and checked them against retained native evidence.
- Promoted `character.cross-category-modifier-order` from UNKNOWN to STRONG based on multiple recovered formula-specific order boundaries.
- Rejected the screenshot class-base/per-level creation equation for Classic: promoted creation evidence starts primary stats at race minima.
- Did not invent a universal equipment percentage stage, disease/curse ordering, blindness -50% Accuracy rule, or sleep/paralysis AC-zero rule.
- Rejected the screenshot's universal 10-second poison tick because periodic spell/status timing is already proven at a 3-second spell/system round.
- Added an evidence-only runtime registry and Pass 36 smoke route; no gameplay modifier formula changed.
- Parity: **97 PASS / 77 STRONG / 26 UNKNOWN = 200** (48.5% strict / 87.0% evidence-backed).

## CodeUpdate0306 / Pass 35 — Classic Worldgroup RNG direct-call boundary

- Pinned the exact retained WCCMMUD/WBBMMUD native versions used by the randomness callsite evidence.
- Added an executable direct-call census boundary registry: 146 `_genrdn` and 42 `_lngrnd` WGSERVER-thunk calls per MajorMUD module variant; retained explicit `_genrdn` addresses remain bounded to 64 per module.
- Preserved the bounded argument analyzer exactly: 64 `_genrdn` observations per module and 42 `_lngrnd` observations per module; the narrower `_genrdn` set is not treated as complete.
- Left indirect calls, gameplay semantic attribution, Worldgroup execution-thread CRT mapping, gameplay draw ordering, and the instruction-equivalent range loop unresolved.
- Made **zero gameplay RNG caller rewrites**; no global Classic stream was invented.
- Parity: **97 PASS / 76 STRONG / 27 UNKNOWN = 200** (48.5% strict / 86.5% evidence-backed).

## CodeUpdate0305 / Pass 34 — Classic Worldgroup PRNG closure

- Rejected the proposed MSVC `214013/2531011` LCG after retained native Worldgroup evidence identified the bound Borland/CW3220MT generator.
- Activated the exact raw Classic transition `seed = seed * 0x015A4E35 + 1 (mod 2^32)` with 15-bit output `(seed >> 16) & 0x7FFF`.
- Activated the recovered WGSERVER startup seed packing `(hour << 11) + (minute << 5) + (second >> 1)`, including two-second resolution.
- Retained `_genrdn`/`_lngrnd` exclusive-maximum and shared-host-PRNG behavior as a proven native boundary without inventing an instruction-equivalent range loop.
- Kept the old xorshift multi-stream `RuntimeGlobalRandomService` explicitly classified as Reforged extension infrastructure, not Classic parity.
- Narrowed the remaining RNG UNKNOWN to gameplay callsite/thread-state mapping, exact draw ordering, and complete native range-loop activation.
- Parity: **96 PASS / 76 STRONG / 27 UNKNOWN = 199** (48.2% strict / 86.4% evidence-backed).

## Pass 27 post-integration compile hotfix

- Fixed the Bundle D remaining-exhaustion audit after `StrengthAlternative` became a non-nullable `bool`; the audit now tests the Boolean directly.
- Fixed C# local-variable shadowing in `RuntimeLiveWorld.ConfigureCommunicationFlag` by renaming the toggle branch local to `toggledEnabled`.
- No parity score or runtime behavior change.

## CodeUpdate0298 / Pass 27 — Trap state and reset boundary closure

- Recovered the stock RoomRec trap storage contract from Nightmare Redux: RoomType/Para2 are signed 16-bit fields, with type 09 damage traps, type 24 spell traps, and Para2 states 0 Active, 1 Inactive, 2 Hidden, 3 Active movement, 4 Inactive movement.
- Enforced the native `DISARM TRAP <direction>` Traps-skill admission rule while keeping SEARCH detection and DISARM success formulas evidence-gated.
- Corrected the trap recovery target: MMExplorer's Rooms projection is lossy and cannot restore Para2; exact per-exit state requires stock WCCMP002 room records or an equivalent native RoomRec export.
- Preserved the Para3/Para4 trap-message ownership conflict between retained Nightmare Redux documentation/editor labels instead of guessing which field is passage vs. disarm-failure text.
- Reclassified generic `RuntimeTrapService` charges/rearm/persistence as Reforged extension behavior, not Classic trap parity evidence; Classic exit traps remain owned by `RuntimeClassicExitTrapCatalog`.
- Kept trap activation, state transitions, reset/rearm interaction, and world-reset ordering fail-closed where native evidence is incomplete.
- Active parity remains 73 PASS / 76 STRONG / 28 UNKNOWN = 177 (41.2% strict / 84.2% evidence-backed).

## CodeUpdate0297 / Pass 26 — Door requirements, trap state, and container scope

- Enforced recovered numeric Picklocks/Strength requirements at the explicit `PICKLOCK <direction>` / `BASH <direction>` action boundary without applying them to ordinary movement.
- Fixed Key-exit `strength_alternative` parsing: the generated field is Boolean, not integer, so keyed exits that permit a Strength/BASH alternative now retain that route.
- Counted the retained 1.11p-derived exit corpus: 1,178 numeric Door Picklocks/Strength requirements, 109 any-value Door requirements, 83 numeric Key Picklocks+Strength alternatives, and 61 numeric Key Picklocks-only requirements.
- Preserved exact PICKLOCK/BASH success rolls, native messages, raw Para2 arithmetic beyond the recovered display projection, durability/break behavior, and key-consumption rules as evidence-gated.
- Kept all 295 decoded exit traps fail-closed where native per-exit Para2 state is unavailable; a public stock 1.11p MDB is located but cannot be decoded with the current binary tooling.
- Removed the generic `native.world-containers` UNKNOWN as a duplicate synthetic subsystem; death piles, banks, gang-house vaults, portable/nested items, and named chest loot remain with their actual native owners.
- Active parity is now 73 PASS / 76 STRONG / 28 UNKNOWN = 177 (41.2% strict / 84.2% evidence-backed).

## CodeUpdate0296 / Pass 25 — Doors, locks, traps, and native room-effect scope

- Corrected Door/Gate Para2 ownership: PICKLOCK/BASH is an explicit action boundary, not an automatic movement stat check.
- Routed `PICKLOCK <direction>` and native `DISARM TRAP <direction>` to the Classic living-world boundary.
- Routed `BASH <direction>` through the decoded door boundary before retaining `BASH <target>` combat behavior.
- Kept exact PICKLOCK/BASH and trap detection/disarm rolls evidence-gated rather than inventing formulas or messages.
- Removed the generic environmental-hazard subsystem from Classic parity scope because the stock RoomType family has no such native record family; actual native effects remain owned by traps, CAST, text/script, remote-action, and other decoded room mechanisms.
- Promoted doors/locks/access from UNKNOWN to STRONG; traps remain UNKNOWN.
- Active parity is now 73 PASS / 76 STRONG / 29 UNKNOWN = 178 (41.0% strict / 83.7% evidence-backed).

## CodeUpdate0295 / Pass 24 — Experience charts and party reward boundary

- Corrected race/class EXP semantics: those values define the character progression chart and required accumulated XP per level; they never multiply monster or other earned XP awards.
- Removed the obsolete race/class XP-award multiplier overload and updated the decoded-character audit to consume `ExperienceChartPercent` instead.
- Preserved active-combat NPC XP sharing: eligibility is characters fighting that exact NPC at death, with integer-quotient awards and unresolved remainder disposition.
- Added the STRONG-evidence Classic party maximum of six total characters without inventing the native party-full text.
- Removed automatic party loot splitting from the parity model; native-era captures show monster currency dropping to the room, while `SHARE` is the explicit voluntary currency distribution command.
- Active parity remains 73 PASS / 75 STRONG / 31 UNKNOWN = 179; this pass corrects semantics and narrows an existing UNKNOWN rather than inflating classification.

- CodeUpdate0279 starts the integrated world editor and enables isolated Custom1 authoring: world settings, Librarium browse/search, scoped object overrides, Custom1 compilation, Host generation safeguards, and CI generation while Classic remains protected.

- Implemented `SYSOP LIST LIMITED` as an authoritative presence-and-location index that omits limited definitions with no live instances.

- Promoted 37 additional native WCC `.HSE` gang-house interiors into generated Classic data (57 total locations) and enforced owning-gang membership at the movement gateway so guardians bar non-members.

- Connected Classic cleanup protection to generated gang-house locations and persistent active house assignments, replacing the unusable synthetic owner/deed resolver so repossession and disbanding correctly expose held contents to cleanup.

- Connected gang-house tax processing to a typed authoritative Classic Sundown event, removed the guessed next-sundown requirement, and preserved fail-closed automatic scheduling while native day/night cadence remains unproven.

- Added idempotent gang-house sundown tax processing using decoded GHouseTax values and exact Silvermere bank gold, with paid-through persistence, rollback, and insufficient-funds repossession.

- Preserved decoded item `cost_type` in generated Classic data and corrected live shop buy/sell settlement to use copper, silver, gold, platinum, or runic denomination values instead of assuming gold.

- Added live leader-only gang-house deed activation with exclusive persistent ownership, decoded four-item grants, native message 2629 output, deed charge spending, and rollback protection for failed character persistence.

- Promoted the ten decoded gang-house deed activation contracts into generated runtime data, including textblocks 9000-9009, native message 2629, and four exact color-specific item grants per deed.

- Extended persistent gang experience accrual to decoded `ASK` ability rewards and generated `TALK` quest-script rewards using the exact positive character-XP delta after successful persistence.

- Added persistent gang experience accrual from live NPC defeat rewards and `TOPTEN GANGS` / `TOP <n> GANGS` rankings ordered by gang experience.

- Added leader-only gang PROMOTE/DEMOTE and one-time queued disband notifications delivered when affected characters enter the game.

- Added Classic player gang commands: CREATE GANG, INVITE/UNINVITE MEMBER, JOIN GANG, LEAVE GANG, BROADGANG/BG, and leader DISBAND GANG, backed by persistent offline invitations and gang state.

- Added persistent Classic gang state and connected SYSOP LIST GANG, DISBAND, DISABLE, ENABLE, and GANGSIZE. Gang state includes leadership, roster, size, experience, TOPTEN eligibility, disband notifications, and gang-house ownership. UNWRAP remains blocked on its exact legacy transformation.

- Recorded the user-confirmed SYSOP LIGHTNING boundary: no good/evil point mutation, with LIGHTEVY affecting target eligibility only.

- Corrected the SYSOP parity ledger: MajorMUD gangs are required and distinct from guilds. LIST GANG, DISBAND, DISABLE, ENABLE, GANGSIZE, and UNWRAP are tracked as blocked required runtime work rather than non-applicable commands.

- Implemented `SYSOP GOD <active-character-id> INVENTORY` and `SPELLS`. These read persistent character state and generated item/spell catalogs while enforcing the native requirement that the target be in the game.

- Implemented `SYSOP GOD <active-character-id> STATUS` and `SPEAK <message>` using authoritative saved character/session state and the decoded active-target rule. GOD SPEAK bypasses ordinary chat preferences through the target's system message queue; unsupported GOD operations remain blocked.

- Implemented persistent Classic TOPTEN exclusion through `SYSOP GOD <active-character> DISABLE TOPTEN` / `ENABLE TOPTEN` and completed `SYSOP REPORT NOTOPTEN`. The target receives a notification, the state survives reconnects, and unsupported GOD operations remain fail-closed.

- Implemented authenticated `SYSOP REPORT USERS [mask]`, including the decoded 19-character maximum and case-insensitive account/name filtering. Local account ID replaces the absent BBS userid. `REPORT NOTOPTEN` now reports its precise missing persisted-state blocker.

- Implemented authenticated `SYSOP LIST USERS` with active account/character identity, exact runtime location, and user count. Preserved decoded GAMEOP access. LIST EVIL, GANG, and LIMITED now report their precise unavailable-state blockers instead of sharing a generic unimplemented response.

- Removed three Librarium-only native provenance fields from generated race descriptions while preserving all 13 ANSI player-facing payloads. Added compiler regression coverage. Fresh Classic decode exhaustion reaches 39/39 connected families with zero unresolved records and zero disconnected generated fields.

- Classified acquisition success textblocks 9013 and 9026 as verified empty inline MDB text (declared length 2). Runtime now requires this generated evidence and preserves native silence instead of treating the nodes as missing payloads or inventing prose.

- Added evidence-backed Classic `ASK <npc> <keyword>` ability acquisition. The dying master assassin's `secret` route grants Perfect Stealth 186; Dhelvanen's `return` route consumes item 1351, presents messages 2639/2640, awards 5,000 XP, and grants Meditate 187 under the decoded class/level branches. Added a compact compiler-generated two-record runtime family and focused persistence/transaction audits. Missing payloads for textblocks 9013/9026 remain silent rather than fabricated.

- Closed the final numbered ability family 186-187: Perfect Stealth and Meditate now require their persisted decoded grant plus their exact seven-class/ten-class minimum-level gates at live command admission. The existing behavior/cadence remains unchanged; missing textblock 9029/9046 acquisition scripts remain explicitly evidence-gated. Release build, character foundation smoke, and full Bundle A audit pass.

- Connected abilities 181-185: the ten deed/tax relationships, 59 house-item relationships, and 11 shop-controller relationships now resolve through the generated ten-house catalog with strict selector validation. All 18 BadAttk NPC item targets resolve and now suppress automatic/live-round NPC attacks only while the targeted character owns the exact decoded item. Release build plus focused gang-house and combat-profile gates pass; deed acquisition/upkeep mutation and ordinary interior ownership remain evidence-gated.

- Closed decoded ability inventory 165-180: zero-owner abilities 165-177 remain intentionally absent; Shadowform 178 now projects exact spell/equipment textblock selectors into live character look handling and fails closed for missing native payloads 4157/9652; FindTrapsValue 179 and PickLocksValue 180 retain their narrow equipment-derived projections, with PickLocks already consumed by live lock admission. Release build and combat bundle pass; item smoke's new Shadowform assertion passes alongside two unrelated pre-existing corpse restoration failures.

- Connected positive decoded `ScatterItems` 157 destinations to authoritative same-map ground-item relocation with exact-instance rollback; retained nine destinationless zero markers fail-closed pending range recovery.

- Added authoritative DeathText 155 relationship consumption and fail-closed semantic validation for tournament badge value 1643; quarantined the mismatched Salca-dart textblock instead of emitting incorrect death output.

- Connected decoded `KaiBind` ability 159 to active-status enforcement for Punch, Kick, and JumpKick at combat admission and round execution, preserving its marker semantics and opponent activity.

- Connected spell-side decoded `ReqToHit` ability 158 for globe of invulnerability as active defensive strike code 9, sharing the established exact-code semantics and blocking unmatched physical attacks.

- Connected decoded `OpenDoor` ability 161 for `wizard knock` to authoritative paired living-world door state, including ambiguity rejection, unlock/open persistence, and transactional two-sided rollback.

- Connected decoded `GiveTempSpell` ability 160 to temporary, effect-lifetime spell ownership with exact target resolution, reconnect reconciliation, expiration removal, and permanent-spell preservation.

- Connected decoded ability 164 (`CastOnEnd%`) to authoritative EndCast execution, including exact inclusive probability gates, immediate duration-zero follow-ups, timed expiration follow-ups, inert zero sentinels, and transactional rollback.

- CodeUpdate0139 NPC ability pass 146-147: activated exact MonsGuards same-room assistance for 112 NPC definitions and projected nine ResistWater profiles while preserving the missing water-attack evidence boundary.

- CodeUpdate0139 maintenance-visibility pass: connected all 16 Visible@Maint fixtures to remain visible during Classic cleanup; preserved Remove@Maint as a separate unresolved evidence boundary instead of duplicating Del@Maint deletion semantics.

- CodeUpdate0139 TextBlock/Rune pass: typed ability 148 selectors and ability 152 markers, regression-locked their full distributions, and quarantined 70 contextual control nodes instead of misrendering them as plain text or inventing rune placement.

- CodeUpdate0139 EndCast pass: activated all 77 positive expiration-triggered spell links with resource-free automatic execution, authored duration retention, immediate chain handling, transactional rollback, and reconnect processing; preserved 11 zero sentinels.

- CodeUpdate0139 KillSpell pass: activated all 3 owner spells and 5 exact effect-removal links, including drowning/holding-breath cleanup and pyramid timer termination.

- CodeUpdate0139 ability inventory 145-154: regression-locked live ManaRgn/HealMana coverage and exact owner counts for the remaining NPC, cleanup, and spell-control families without inventing generic modifier behavior.

- CodeUpdate0139 ability pass 142-144: connected timed HitMagic spell status to magical-creature combat admission, verified zero ClearItem owners, and regression-locked all 318 NonMagicalSpell general-dispel exclusions.

- CodeUpdate0139 fixed-teleport pass: activated all 12 complete TeleportRoom 140 / TeleportMap 141 spell pairs with exact generated-room validation, transactional rollback, character-location persistence, and instant-effect cleanup.

- CodeUpdate0139 ability pass 137-141: connected all 387 NPC SpellImmu thresholds to living NPC spell resolution and verified existing Shock damage-shield/native-message consumption. Preserved fail-closed boundaries for RoomVisible and incomplete teleport pairs.

- CodeUpdate0139 item ability pass 135-136: fixed decoded equipment level-range consumption. Compact generation and live equip now enforce both MinLevel and MaxLevel, including the multicoloured staff's exact level-65 gate.

- CodeUpdate0139 grouped quest-progression ability pass 125-134: classified the complete consecutive family—IceSorcQuest, GoodQuest, NeutralQuest, EvilQuest, DarkDruidQuest, BloodChampQuest, SheDragonQuest, WereratQuest, PhoenixQuest, and DaoLordQuest—as persistent decoded character progression state. All ten retain exact numeric values and provenance through save/load and are explicitly excluded from player power restoration. Character Bundle A proves every identity, persistence path, and non-power boundary; the full 83-quest/182-script narrative smoke remains green.

- CodeUpdate0139 grouped ability pass 123-124: verified HPRegen across 25 item and 16 spell owners against the existing authoritative regeneration cadence. Connected five spell-side and two NPC-side NegateAbility records: positive values suppress only their named decoded ability (Hold Person 74, Fear 60, or Confusion 71), including both Tyrannosaur Confusion immunities. The lone zero-valued `hold immune` record remains inert and fail-closed rather than inventing a target ability. Regression proves immunity does not cancel unrelated spell payloads.

- CodeUpdate0139 RemovesSpell pass: audited all 107 owners and 334 ability-122 relationships, proving 331 resolvable positive spell targets plus three inert zero sentinels. Corrected removal-helper lifetime so duration-zero cures and cancellation spells never leave permanent empty effects when no matching status exists. Timed replacement spells now preserve their newly committed effect after removing the exact authored conflicts from the target.

- CodeUpdate0139 Recharge pass: projected ability 121 for all 78 decoded item owners and connected its documented cleanup behavior. Seventy-seven finite-capacity items now regain the exact Recharge value at cleanup, capped by decoded maximum uses and guarded against duplicate application when the same cleanup event replays. The rule applies to all surviving ownership domains without inventing an independent schedule. Shadowy ankh remains fail-closed because its decoded maximum is -1 and no cast relationship supplies a usable charge contract.

- CodeUpdate0139 StartMsg pass: projected ability 120 for all 229 decoded spell owners from verified native WCCMSG2 payloads. Together with the existing BCNS correction, 230 runtime contracts preserve 164 caster, 208 target, and 189 observer messages. Successful committed player casts now route each native slot to its correct audience with ordered placeholder substitution, weapon procs use decoded caster output, empty slots remain silent, and failed/rolled-back casts do not publish start text.

- CodeUpdate0139 Del@Maint pass: verified ability 119 on all 460 generated item definitions and locked its existing room-ground-only Classic maintenance semantics. Marked visible or hidden instances are deleted while player inventory/equipment and other ownership domains remain untouched. Cleanup deletion now uses the item engine's cycle-safe subtree traversal so a deleted container and every nested child are removed in one atomic operation without orphaned item state.

- CodeUpdate0139 backstab-damage range pass: connected spell abilities BsMinDmg 117 and BsMaxDmg 118 to the authoritative player combat profile. Mantle of the woodsman now supplies its decoded timed +25/+25 range; natural prowess and way of the cat remain zero/zero. Regression locks the independent item evidence surface at 47 minimum relationships (4 negative/43 positive) and 35 maximum relationships (2 negative/33 positive), without manufacturing missing pairs. Status, equipment, derived, and quest bonuses combine once before stock backstab scaling.

- CodeUpdate0139 BSAccu pass: closed the missing spell-side consumer for ability 116. Camouflage spell 1314 now projects its exact +10 timed backstab-accuracy modifier and removes it on expiration. It combines with the existing 239 item relationships—114 negative, 68 zero, and 57 positive—before the stock `Fix(BSAccu / 2)` formula term. Regression locks the complete signed item distribution and proves the modifier is isolated from normal attack accuracy.

- CodeUpdate0139 DescMsg presentation pass: connected ability 115 from 539 spell owners to 540 effective native message selectors. Runtime no longer consumes only the wear-off text: the player WCCMSG2 slot supplies 432 nonempty expiration messages, the secondary slot supplies 18 observer descriptions during LOOK at an affected player/NPC, and the tertiary slot supplies 435 active-effect descriptions on SCORE/STAT. Repeated selectors are preserved, native empty output remains empty, selector zero remains inactive, and BCNS retains the corrected message 2531 in place of unresolved source value 3498.

- CodeUpdate0139 `%Spell` ordered-pair pass: corrected ability 114 parsing from immediate-adjacency matching to the decoded "next CastsSp" rule. Intervening item attributes are skipped without changing their order, raising executable on-hit spell pairs from 109 to 127. Every recovered spell resolves and is a generated single-hostile-target effect; normal and critical weapon hits retain exact 1-100 inclusive chance checks. One identical Caladbolg duplicate is deduplicated. AngelCrusher, skull flail, and tonfa remain fail-closed because their `%Spell` rows have no later `CastsSp` relationship.

- CodeUpdate0139 spell-alignment exclusion pass: connected NotGood 110, NotEvil 111, Neutral 112, and NotNeutral 113 to live caster admission. Generated ownership is exactly 1/3/10/0 spells. Lawful/Saint/Good, Neutral/Seedy, and Outlaw/Criminal/Villain/Fiend use the decoded allow/exclude matrix; missing and unknown alignment fail closed. The gate runs before target selection, resources, cooldowns, effects, or world mutation and now matches the already-live equipment restriction family.

- CodeUpdate0139 AffectsLiving/NonLiving pass: connected all 64 AffectsLiving-108 spells to the 184 NPC definitions carrying NonLiving 109. The compact flag now survives both ordinary spawn materialization and runtime summons. Direct casts and weapon procs reject nonliving NPCs before resources, cooldowns, damage, effects, or world mutation; character targets remain living. Regression inspection exposed and repaired a separate boolean-loader disconnect that had classified all 106 generated undead definitions as living. The catalog proves 83 NonLiving definitions are not undead, so the two concepts remain independent. Race/Class stealth 102/103 was audited but remains fail-closed because the generated race/class data does not prove ownership.

- CodeUpdate0139 LoyalItem death-retention pass: connected all 101 decoded ability-100 item relationships to the authoritative character death/corpse transition. LoyalItem and CURSED-83 now share one truthful `RetainedOnDeath` definition property; with one overlap and two CURSED-only records, 103 exact item definitions remain owner-held through death while ordinary items continue into the corpse. Corpse item counts, owner-limit reservations, resurrection, and normal drop behavior remain intact.

- CodeUpdate0139 spell-alignment admission pass: connected all 19 Good-97 and 20 Evil-98 spell relationships to the authoritative character reputation boundary already used by equipment. Good spells admit Lawful/Saint/Good casters; Evil spells admit Outlaw/Criminal/Villain/Fiend plus the compatible legacy Evil label. Invalid casters fail before target resolution, mana/energy cost, cooldown, effects, or world mutation. Shatter 85 remains quarantined because its sole retained Quality threshold description explicitly labels the behavior theoretical.

- CodeUpdate0139 curse lifecycle pass: connected item abilities Curse 82 and CURSED 83 plus both Rcrs 84 spells. All 15 Curse items and three CURSED items now resist ordinary unequip from their decoded catalog definition rather than depending on optional instance metadata. CURSED items remain owner-held/equipped across corpse creation, while ordinary Curse items follow normal death transfer. Both remove-curse spells atomically unequip every cursed item on the target, recompute equipment/inventory/derived state, persist the result, and discard their zero-duration helper effect.

- Parity evidence policy changed by user direction: imported and decoded data will never be deleted. After full gameplay, board/Sysop/MajorMUD Plus, and MegaMUD emulation parity, files not required for live play will be placed in a local server evidence ZIP and excluded from Git. No archive move occurs before that complete parity gate.

- CodeUpdate0139 AffectsAnimals target pass: connected the single decoded ability-80 spell, charm animal, to the exact 155 NPC definitions carrying decoded Animal ability 78. Living NPC metadata now preserves that classification compactly. Player casting admits only an animal NPC target and rejects non-animal NPCs or characters before mana, energy, cooldown, effect, or world-state mutation; no name-based species inference is used.

- CodeUpdate0139 Percep search-consumer pass: connected all 14 decoded ability-77 spell relationships to the authoritative hidden-search Perception value. Trained Perception, equipped ability-77 modifiers, and active timed spell modifiers now combine once; fixed signed parameters remain authoritative, zero parameters use the persisted cast roll, and the final search boundary safely clamps negative totals to zero. Alertness and Feeblemind regressions prove both positive rolled and negative fixed effects without changing unrelated NPC AI perception.

- CodeUpdate0139 HoldPerson separation pass: corrected all 41 decoded ability-74 spell relationships from synthetic paralysis to the decoded room-hold contract. Hold Person, Entangle, web, net, knockdown, and related effects now project `immobilized`: movement is rejected before combat-flee mutation, active Dodge is suppressed by the established immobilization rule, but physical attacks, spellcasting, and other conscious actions remain available. Freedom continues to remove the full family through the shared effect ledger.

- CodeUpdate0139 DispellMagic scope pass: separated all 16 decoded ability-73 records by their retained value. Eight value-zero spells remain general magical dispels, four value-107 spells remove only BlindUser effects, and four value-19 spells remove only poison effects. Targeted cures no longer erase unrelated buffs, `NonMagicalSpell` remains protected from general dispels, and zero-duration dispel helper effects are always discarded even when no matching status is present.

- CodeUpdate0139 SpellImmu defensive pass: connected both decoded SpellImmu (139) sources as numeric inclusive spell-level thresholds: globe of invulnerability at 9999 and ray of petrification at 999. Direct player, weapon-proc, item, and exit-trap casts now spend normal resources but atomically discard damage/status when the incoming spell level is at or below the target threshold. NPC hit spells apply the same gate to primary targets and each room-area bystander before resistance, damage, or duration delivery. Higher-level spells remain eligible, and expiration/removal clears immunity through the shared status lifecycle.

- CodeUpdate0139 BlindUser presentation pass: connected all 20 decoded BlindUser (107) duration spells to the live character vision boundary. While active, room rendering, directional look, examine, and search return ANSI blindness output without revealing rooms, exits, entities, items, or hidden discoveries. Remembered movement remains usable, hostile room admission still occurs, and combat/spellcasting are not generically blocked; each spell's separate decoded Accuracy, AC, Crits, and damage modifiers remain the sole combat penalties.

- CodeUpdate0139 Mute/MageBind separation pass: split four decoded Mute (76) spells from three MageBind (79) evidence records. Mute now blocks player `say`, `tell`, and NPC `talk`, and prevents a muted live NPC from producing dialogue. It no longer blocks spellcasting. MageBind remains projected as explicit retained evidence but has no gameplay suppression because its own decoded description says the former spellcasting behavior may no longer work; runtime does not turn that uncertainty into a false Classic rule.

- CodeUpdate0139 Fear-control pass: connected all 14 decoded Fear (60) duration spells to live combat disengagement. A feared persistent character executes the existing authoritative player-flee release before either side attacks; a feared live NPC releases its matching encounter and threat/engagement ownership before another round. Fear remains distinct from paralysis: it does not blanket-block movement, attacks, or spellcasting outside an active encounter, and it does not invent a destination, bypass an exit, or teleport either participant.

- CodeUpdate0139 confusion-control pass: connected all 84 decoded Confusion (71) spell relationships to their exact per-round fumble percentage instead of treating every confusion effect as a blanket stun. Zero-valued ability parameters now use the persisted per-cast effect roll, 77 paired ConfuseMsg (101) selectors render their exact native WCCMSG2 player payload (including verified no-output message 66), and the seven effects without a selector do not invent a native binding. Physical attacks and spellcasting remain available on successful rolls; paralysis and silence keep their separate decoded blocking behavior.

- CodeUpdate0139 rolled-status/AlterDRpercent pass: player and NPC duration effects now persist their actual decoded effect roll instead of reducing min/max ranges to a midpoint. Status-only spells emit non-damaging modifier events rather than accidental HP damage. Both frail spells' AlterDRpercent (99) values now modify the assembled additive DR by the retained signed percentage, clamp effective DR at zero, and expire through the shared status lifecycle.

- CodeUpdate0139 Freedom spell pass: connected all eight Freedom (81) spells to atomic status removal. Freedom now removes generated holding effects (HoldPerson 74) and every active spell that alters speed (Slowness 68 or Speed 87), resynchronizes persisted character status, and removes its own helper effect. Poison, unrelated buffs/debuffs, and unsupported paralysis semantics remain untouched.

- CodeUpdate0139 timed resource-stat pass: connected four MaxMana (69) spells, one Alter HP/max-HP (88) spell, and nine Encumbrance (96) spells through the shared status lifecycle. Maximum-resource modifiers are reversible and non-cumulative, preserve equipment/base maxima, and clamp current HP/mana on reductions or expiration. Spell encumbrance now joins equipment and persistent encumbrance in live weapon cadence/quick-critical calculations.

- CodeUpdate0139 HealMana resource pass: connected all 26 generated HealMana (150) spell relationships. Nonzero ability values apply as fixed signed mana changes; zero values use the decoded/scaled spell effect roll. Character mana persists with min/max clamping and self-cast resource costs remain correctly ordered. NPC-facing effects modify the same bounded encounter energy pool consumed by decoded NPC spell attacks and used by resisted-spell refunds, closing three `mana dead` weapon procs without introducing a duplicate NPC mana field.

- CodeUpdate0139 spell mana-regeneration pass: connected all 26 generated ManaRgn (145) spell relationships to the decoded character regeneration authority. Timed spell percentages combine with equipment ManaRgn, affect both 30-second passive ticks and 10-second post-focus meditation ticks, support positive and negative values, clamp final restoration at zero, and disappear through the existing status expiration/removal lifecycle.

- CodeUpdate0139 native damage-shield presentation pass: replaced the generic retaliation line with each equipped item or active spell's decoded Shock (137) WCCMSG2 selector. All eight damage-shield items and all six damage-shield spells retain independent damage identity and execute only after positive physical damage. Five spells resolve exact native player/observer payloads; chaos shield (`spell.405`) correctly executes its decoded DamageShield 10 but emits no invented retaliation line because that spell has no Shock selector. Shatter remains fail-closed because the only retained threshold description explicitly marks its Quality rule as theoretical.

- CodeUpdate0139 decoded weapon on-hit spell pass: connected all 109 exact adjacent `%Spell` (114) / `CastsSp` (43) item relationships to successful normal-weapon hits and criticals. Each proc retains its decoded 1-100 percentage, resolves through the generated Classic spell catalog, applies through the shared living-world NPC spell/damage path, and emits ANSI effect output. Misses, dodges, glances, martial attacks, and 22 `%Spell` rows without the required following `CastsSp` remain fail-closed. Shock (137) remains a damage-shield message selector, while the single Shatter (85) spell remains blocked because its retained description labels the Quality threshold semantics theoretical rather than exact.

- CodeUpdate0139 magical-creature weapon admission: promoted item/NPC abilities Magical (28), HitMagic (142), and ReqToHit (158) into live combat profiles and fail-closed attack admission. Class plus all equipped HitMagic reduces the target requirement first; the wielded weapon must meet only the remainder. ReqToHit requires an exact nonzero weapon code match. Martial/unarmed attacks receive no wielded-weapon credit, while a complete HitMagic bypass remains valid. Checks occur before engagement, threat, concealment, or combat-state mutation.

- CodeUpdate0139 martial equipment pass: registered and routed Punch, Kick, and Jumpkick; enforced decoded Mystic abilities 29/30/35; connected equipment/status abilities 89-94 only to their matching martial accuracy and damage paths; and retained the native 0%/+33%/+66% attack-type scaling, AC-first/DR/dodge ordering, and unarmed weapon independence. F11 passes with 100 registrations and 46 aliases.

- CodeUpdate0139 Borin directed-command passthrough repair: Borin's supervised terminal path now sends any user-directed one-line game command to the connected Reforged/Omniverse terminal without the old starter-command allow-list, HP/combat refusal, or movement exit pre-check. Autonomous play remains restricted to conservative self-selected action families; the Host/BBS remains authoritative for command validity and consequences.

- CodeUpdate0139 alignment-defense equipment pass: connected Damage Shield (72) as hit-only physical retaliation; connected Protection from Evil/Good (24/25) as the retained native armor-class adjustment against matching attackers; and enforced Good, Evil, Neutral, NotGood, NotEvil, and NotNeutral item restrictions from decoded reputation groups. Equipment and timed spells share the same consumers; misses, dodges, zero damage, spell rows, and neutral attackers do not trigger retaliation/protection incorrectly.

- CodeUpdate0139 REST/MEDITATE closure: registered both Classic commands; implemented 20-second uninterrupted activation, 20-second REST HP ticks, 10-second MEDITATE mana ticks, mutual exclusion, combat rejection, universal action cancellation, decoded formulas, and command/runtime regression coverage.

- CodeUpdate0139 decoded regeneration cadence: added authoritative 30-second passive HP/mana ticks, decoded rate calculations by level/stats/magic family, equipment abilities 123/145 percentage application, active-session scheduling, resource caps, and regression coverage. Passive ticks continue during combat; REST/MEDITATE remains a separate 20-second uninterrupted-state feature.

- CodeUpdate0139 equipment resource pass: connected abilities 69 Maximum Mana, 88 Alter HP/maximum HP, and 96 Encumbrance to reversible persisted character/runtime consumers. Added removal clamping and regression coverage; retained 123/145 regeneration percentages without inventing an unsupported timer.

- CodeUpdate0139 defensive equipment pass: connected equipment abilities 36/51/21 to live Magic Resistance, Anti-Magic eligibility, and poison immunity consumers across weapons, wearables, and cloaks. Preserved zero-valued Anti-Magic marker semantics and added universal projection regression coverage.

- CodeUpdate0139 universal equipment pass: connected decoded Maximum Damage (4), Stealth (27), Dodge (34), Perception (77), and Backstab damage (117/118) modifiers from all equipped weapons/items/cloaks to shared combat and search consumers. Added regression coverage proving modifier application without item-name special cases.

- CodeUpdate0139 Blacknight closure: connected level-40 Thief reward item 1316 to the live `use` route, generated spell 1220, five-use charge persistence, retained-item behavior, and combined passive/timed Picklocks gate values. Added decoded max-use initialization and retained Stealth/Find Traps/Picklocks equipment projections.

- CodeUpdate0139 NPC self-status closure: added compact living-world timed NPC status persistence and connected hooded man spell 1220 to Picklocks +200 for 15 combat rounds, including refresh, expiration, death cleanup, event recording, and modifier lookup. No unproven NPC door behavior was added.

- CodeUpdate0139 plague duration closure: type-0 NPC spells with decoded combat modifiers can now apply their timed status payload in addition to immediate damage. Spell 446 applies its exact 30-round Dodge, AC, and Max Damage penalties room-wide, with shared persistence/refresh/expiration behavior and regression coverage.

- CodeUpdate0139 sphinx runtime execution: connected generated spell 686 Textblock 2510/2511 to live combat, including exact weighted branches, native room-aware messages, two-stage teleport, spells 689/688 damage, final state persistence, engagement release, fail-closed validation, and rollback coverage.

- CodeUpdate0139 sphinx textblock recovery: promoted the structured MDB decode of spell `686`'s full textblock chain into compact Librarium authority and generated runtime data. Root `2510` selects table `2511`; thresholds 25/50/75/100 route to branches `2512-2515`, preserving exact WCCMSG2 `1960-1963`, two map-qualified Map 12 teleports per branch, and casts `689` then `688`. This corrects the earlier false interpretation of a WCCTEXT2 physical-page fragment as a complete payload. Compiler regression, clean generation, validation, Release server build, and live-combat smoke pass.

- CodeUpdate0139 NPC self-target correction: restored target type `1` to the original `Self` meaning and stopped its six generated NPC casts from incorrectly evaluating player resistance or attempting player damage/status/script execution. Regression inventories five stone sphinx spell-`686` rows and hooded man NPC `1000` spell `1220`. NPC-side effects remain fail-closed at explicit evidence boundaries: malformed/truncated textblock `2510` and the absence of a proven NPC Picklocks action consumer. Release build and focused combat gates pass.

- CodeUpdate0139 NPC full-area spell connection: original MMUD Explorer source history proves target type `11` is `Full Area` and is treated as an area attack. The only generated NPC use, plague-crafter NPC `361` casting spell `446` (`plague`), now executes one authoritative room-wide cast. Persistent characters independently receive the decoded `4-10` untyped, never-resisted damage, persistence, and lethal death/corpse handling without per-session cast multiplication. The decoded duration modifiers remain explicitly unimplemented pending an exact type-0 lifecycle consumer. Release build and focused combat gates pass.

- CodeUpdate0139 NPC area-hostile spell closure: target type `12` now applies one authoritative cast to all persistent characters in the caster's room, with per-target resistance, immunity, elemental damage, status delivery, persistence, and death/corpse handling. Selected-target ownership prevents per-session duplicate casts; area resistance no longer incorrectly refunds a cast that continues against other targets. Release build and focused combat gates pass.

- CodeUpdate0139 native NPC argument-boundary audit: exhaustively traced WCCMSG2 `8480` across generated data, Librarium, retained WCC public decode, raw imports, and native payloads. Five NPC `1006` Nahr attacks lack both caller-owned miss arguments in every available source, so safe fallback wording remains and no Classic value is inferred. The audit also records decoded target type `12` area-hostile NPC spell delivery as the next real combat disconnect because current execution affects only the engaged character.

- CodeUpdate0139 reachable native body-part closure: the final audit separates 37 deterministic type-2 spell rows from physical miss-message coverage; their physical body-part miss templates are unreachable by design and empty weapon fields are not disconnects. Among 785 reachable physical rows, 780 render exact decoded arguments. IDs 2939 and 8310 are now fully connected, raising fully exact native formats to 735/736. Only ID 8480 remains partial: five Nahr physical attacks lack an explicit decoded miss verb/body-part pair and retain safe fallback wording. Release build and live-round regression pass.

- CodeUpdate0139 decoded type-2 NPC spell execution: type-2 attack rows now expose an effective spell ID from their decoded `accuracy` slot when no separate hit-spell field exists. They no longer feed spell ID/cast-control fields into the physical weapon resolver or inflict duplicate physical damage. Runtime executes the canonical spell resistance/effect/status path, applies only resolved spell damage, and renders that damage through the exact native cast message. Regression proves dark-cleric spell 16 resolves as spiritual hammer; Release build and all combat gates pass.

- CodeUpdate0139 duplicate-definition combat argument recovery: live runtime builds a fail-closed consensus from generated NPC definitions keyed by normalized NPC name, attack slot, and native hit-message ID. Missing miss verbs or weapon names are inherited only when every matching populated definition agrees; conflicting groups are rejected. This recovers eight additional executable body-part rows (786/822 exact), including Nahr, necromancer, and inquisitor variants. Thirty-six rows remain without unique evidence across partial message IDs 2939, 8310, and 8480. Fully exact format count remains 733/736. Release build and live-round regression pass.

- CodeUpdate0139 native type-2 spell/song message closure: decoded type-2 NPC attack rows use their `accuracy` slot as the canonical spell ID, proven against generated spell names and retained printable evidence (including spiritual hammer, agony, unholy force, holy force, sphere of implosion, divine fury, and cloud of spores). Live rendering now selects the authoritative target-facing secondary WCCMSG2 slot for actor-facing `You cast`/`You sing` records and fills attacker, spell name, and damage exactly. IDs 17, 18, 20, 8441, and 8502 are closed, raising fully exact native behavior to 733/736 formats. Only partially populated body-part IDs 2939, 8310, and 8480 remain. Release build and live-round regression pass.

- CodeUpdate0139 decoded NPC miss-argument connection: runtime NPC attack definitions now consume retained generated `atk_verb3`, `miss_verb3`, and `weapon_name` fields from each attack's public-reference evidence. Five-token native dodge messages render attacker, exact miss action, defender, exact weapon/body part, and defender without hard-coded substitutions. Coverage is 778/822 executable body-part rows; IDs 806, 2490, and 8372 are fully bound, raising fully exact native behavior to 728/736 IDs. The 44 remaining rows across IDs 2939, 8310, and 8480 lack verb/weapon fields in retained evidence. Five spell/song formats remain separately caller-blocked. Release build and live-round regression pass.

- CodeUpdate0139 verified native no-output combat closure: WCCMSG2 IDs 1 and 66 are authoritative empty player payloads used by decoded NPC attack rows. Live combat now treats them as successful intentional silence, suppresses the generic fallback and avoids emitting empty ANSI lines. Combined with the six paired-hit contextual formats, exact native outcome behavior reaches 725/736 IDs. Eleven formats remain evidence-blocked: six body-part dodge templates and five spell/song templates whose required original caller arguments are absent from decoded attack rows. Regression locks no-output behavior and direct 719/736 rendering; Release build and all combat gates pass.

- CodeUpdate0139 paired native combat-argument recovery: decoded NPC attack rows now supply their authoritative paired hit template when a generic armor-deflection or attack-dodged outcome requires the attack action phrase. This connects six additional WCCMSG2 formats contextually without hard-coded verbs, raising exact native outcome coverage from 717 to 723 of 736 IDs. Thirteen IDs remain evidence-blocked: two verified empty/no-output payloads, six body-part dodge formats lacking the original body-part argument, and five spell/song formats lacking original spell-name/caller ownership. Release build and combat admission, live-round, and completion gates pass.

- CodeUpdate0139 native NPC combat-message connection: all 736 generated hit/miss/dodge message IDs resolve in the authoritative 3,869-record WCCMSG2 catalog; the 735 IDs reachable from executable attack rows are likewise complete. Live NPC attacks now render the exact target-facing native payload for 717 templates whose argument grammar is proven, including decoded attacker/damage forms and safe multi-token attacker/defender/color-affix forms, then apply the shared ANSI outcome color. The remaining 19 IDs comprise two verified empty/no-output payloads plus generic attack-verb/body-part and spell/song formats whose original call-site arguments are not carried by the generated outcome record; they deliberately retain safe semantic fallbacks instead of guessed substitutions. Regression locks exact hit and complex dodge rendering plus exact 717/736 coverage.

- CodeUpdate0139 live-combat presentation/flow repair: automatic six-second combat rounds now wake an idle Telnet session and render immediately instead of remaining queued until the player presses Enter. Telnet writes are serialized so asynchronous combat output cannot interleave with server echo. Added a shared Classic ANSI combat presentation for engagement, player/NPC attacks, criticals, warnings, effects, defeat/victory, round HP summaries, and the live HP prompt. Release build plus combat admission, complete live-round, and combat-completion gates pass.

- CodeUpdate0139 description-attachment closure: recovered the 13 exact native WCC race-help payloads from `WCCMMHLP.mcv` evidence and attached them to canonical race numbers/names through a compact Librarium binding family. Clean generation now emits `race_descriptions.json`; compiler regression verifies every race-to-symbol binding (`HL1B34` through `HL1B44`, `HL1B102`, and `HL1B103`). Bundle F F2A static passes with 13 race, 1,100 NPC, 1,950 item, and 26,668 room descriptions and zero disconnects; F2A dynamic and F1 authority audits also pass. Decoded evidence remains preserved and source retirement remains blocked by the master ledger.

- CodeUpdate0139 NPC marker closure: removed 45 exact `Unknown` sentinel tokens from MMExplorer raw printable-segment arrays during generated runtime projection while preserving all meaningful segments and original Librarium evidence. No NPC identity, combat, spell, loot, dialogue, or authoritative description field was replaced or invented. Compiler regression covers all 1,100 NPCs; clean generation, Release build, NPC smoke with 962/962 resolved spawns, and Bundle E 13-subsystem zero-disconnect audit pass. Decode exhaustion reaches 37 connected families / 0 pending-unresolved families / 0 disconnected families / 0 disconnected fields. Source retirement remains blocked by the master parity/archive ledger. The broad F2A static-description audit separately identified missing generated Human race-description authority.

- CodeUpdate0139 economy-rule marker closure: reconciled 22 evidence-backed shop-type/service relationship indexes with their existing runtime consumers, replacing 44 stale pending/unscored labels with `runtime_authoritative_relationship_index` and `decoded_service_relationship_evidence`. The separate Bard 10% pricing rule remains unchanged. Compiler regression verifies all 23 records and non-empty relationship targets. Clean generation, Release build, economy/service coverage, and live services/economy smoke pass. Exhaustion improves to 36 connected families / 1 pending family / 0 disconnected families / 0 disconnected fields; NPCs are the sole remaining marked family.

- CodeUpdate0139 bank runtime compaction: excluded the literal `bank.2` / `Leave this blank` sentinel from generated `/Data` while preserving it in Librarium evidence. Generated Classic now contains exactly 26 decoded, room-bound bank/vault services and no non-service placeholder. Updated compiler regression and economy coverage accordingly. Clean generation, Release build, full economy/service audit, and live services/economy smoke pass. Exhaustion improves to 35 connected families / 2 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 loot-rule data connection: fixed compiler family selection that generated only the old loot evidence catalog and omitted the verified Classic kill-drop rule. Generated `loot_rules.json` now contains both records, and live combat loads/validates `roll_range = 1..100 inclusive` plus `success_condition = roll <= drop_percent` before resolving NPC drops. Librarium-only implementation notes and example labels are stripped from runtime. Compiler regression protects exact 1% and 3% boundaries; clean generation, Release build, Bundle C loot audit, loot materialization, and combat completion smoke pass. Exhaustion improves to 34 connected families / 3 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 character-foundation marker closure: removed the non-executable, unresolved lawful-target penalty evidence note from generated runtime projection while retaining it in Librarium. Runtime continues to use the decoded eight-reputation attack penalty table; regression fixes Saint/Good at 2 and Neutral/Fiend at 0 and prevents the unverified alternative from entering `/Data`. Clean generation, Release build, and live character foundation smoke pass. Exhaustion improves to 33 connected families / 4 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 learned-skill identity closure: reconciled all 13 generated skill records with their live role as the authoritative saved-character identity registry. Replaced stale candidate/pending projection labels with explicit runtime identity-contract classifications while preserving empty acquisition relationships, so unsupported teaching/training remains fail-closed. Added compiler regression for record count, classifications, and the no-invented-relationships boundary. Clean generation, Release build, and the complete Bundle A character audit pass; exhaustion improves to 32 connected families / 5 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 ability-marker closure: classified 13 exact `Unknown` OmniVerse public-reference descriptions as absent reference text rather than executable Classic values. Compiler runtime projection omits only those placeholders while preserving source evidence and every meaningful description. Added regression across all 188 abilities; clean Classic generation contains zero placeholder descriptions, Release server build passes with zero warnings/errors, and the 14-check actors/abilities smoke passes. Fresh exhaustion improves to 31 connected families / 6 pending families / 0 disconnected families / 0 disconnected fields; retirement remains blocked.

- CodeUpdate0139 Map 1 candle-route runtime repair: correlated the user-confirmed source room with exact decoded data for `exit.map1.2252.n`, destination `room.map1.2254`, and aliases `turn right candle left`, `twist right candle left`, and `turn right candlestick left`. Fixed text-command traversal being rejected by a redundant second gate evaluation that lacked the entered phrase. Added exact teleporter assertions and end-to-end living-world traversal checks for all three aliases. Release server build, teleporter audit, and living-world smoke pass with zero failures.

- CodeUpdate0139 world-contract disconnect closure: verified the 25 contextual room-action records end to end on a clean Classic regeneration; F11 passes with 95 command registrations, 44 aliases, 25 generated contextual verbs, and no failures. Reconciled the three generated Classic/custom/AI world definitions with the existing authoritative server world projection, replacing stale pending/unscored metadata with runtime-authoritative world-configuration and canonical-Librarium-contract classifications. Added compiler regression for all three scopes and output contracts. Fresh exhaustion improves to 30 connected families / 7 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 local Borin command-gateway repair: Borin/Wanderer user-directed terminal dispatch now accepts any ordinary single-line gameplay command and lets the connected Host/BBS decide validity. The app still blocks account/admin/script/destructive control commands, keeps combat/danger guards, and leaves autonomous "play by himself" on its conservative command family list.

- Cumulative packaging: combined the Classic Administration Editor, Classic Command Runtime Closure, and Classic Character LOOK Repair payloads into one checksum-validated update with one top-level folder and one installer.

- CodeUpdate0256A cumulative compiler repair: fixed CS0136 in `RuntimeCommandSystem.RenderHelp` by renaming the contextual command collection to `contextualCommands` and the help-topic Boolean to `isContextual`. No command behavior changed. The cumulative installer now asserts both corrected source contracts before building.
- CodeUpdate0256 Classic character LOOK repair: character inspection no longer throws when a race-description record is missing or uses a different separator/ID form. The runtime now consumes 13 verified native race descriptions, normalizes names such as `Half Ogre`/`Half-Ogre` and `race.10`, reads the actual Classic stat keys (`str`, `agl`, `int`, `wil`, `hea`, `cha`), emits a stat-based race/class headline such as `Shisenshi is a heroically proportioned Half-Ogre Paladin.`, and always continues to the equipped-item listing. `look <character>` accepts every unique leading name abbreviation, including one-letter abbreviations that overlap directions when another visible character matches; ordinary directional LOOK remains intact otherwise. Bundle F Phase F2A static and dynamic audits cover the repair.

- CodeUpdate0255 Classic command runtime closure: audited all 95 built-in commands and 44 aliases, added a 95-entry runtime route authority, connected 18 additional generated contextual verbs for 113 unique active commands, repaired parser-driven quit/logout/exit handling, connected `tat <stat>` to live service runtime, completed help usage for every command, and restored exact `g` → `get` while preserving contextual `go`. The stale 83-command/missing-sidecar audit was replaced with runtime-derived F11 coverage.

- Classic administration editor update: added Classic controls for native item-limit enforcement, per-character full SYSOP authority, and global SYS GOTO-only authority; added a live logged-in character/session table to the Server tab; and retained unlimited items as the default until the host changes the policy.

- CodeUpdate0139 user-confirmed contextual room-action closure: all 25 clue-harvest words are confirmed as verbs used by room-specific action phrases, not direct textblock commands. Compiler generation attaches the user-confirmation evidence, emits authoritative contextual-room-action diagnostics, and activates the records through the existing evidence predicate. This closes all 75 command markers; F11 passes with 95 registrations, 42 aliases, and 25 active contextual verbs, and exhaustion improves to 29 connected families / 8 pending families / 0 disconnected families / 0 disconnected fields. The supplied `turn right candle left` trainer-access example is retained as recovery evidence, but its exact room and action chain are absent from current interaction data and remain unimplemented rather than guessed.

- CodeUpdate0139 bank marker repair: 26 evidence-backed bank/vault records with positive room bindings now generate truthful runtime-authoritative/decoded-service labels, closing 52 stale markers. The one remaining record, `bank.2` / `Leave this blank`, has no room number and remains pending/unscored instead of being falsely promoted. Compiler regression locks the 26/1 boundary; clean generation and the complete economy/service audit pass across all 27 records.

- CodeUpdate0139 command candidate fail-closed repair: `commands.json` contains 25 clue-harvest candidates explicitly marked parser-confirmation-required. The loader no longer contradicts that evidence by activating every record; its existing authority predicate now quarantines all 25 until exact parser proof is recovered. The 95 authoritative registrations / 42 aliases remain active, contextual aliases pass, and the full interaction-narrative smoke passes across 83 quests and 182 scripts. The F11 report now names its 25-record value `GeneratedEvidenceCount` rather than inaccurately calling them textblock invocations. The 75 evidence markers remain intentionally open.

- CodeUpdate0139 shop marker closure: all 178 generated shops now carry decoded-service evidence confidence instead of stale unscored authoring metadata. Every record retains evidence references and a ready generator mapping; compiler regression proves 178 shops and 1,204 inventory links. Clean generation plus economy/service coverage pass for 178 shops, 27 banks, 44 trainers, 1,950 priced items, 23 rules, and zero unclassified services. Exhaustion improves to 28 connected families / 9 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 structural-family/status-effect marker closure: required `client_projection.json` is now audited as a structural contract family rather than a disconnected gameplay family. The compiler emits accurate `runtime_authoritative` and `decoded_relationship_evidence` diagnostics for all 111 decoded status effects, closing 222 stale pending/unscored markers without changing their evidence relationships. Compiler regression, clean Classic generation, Release server build, and the 1,350-spell / 111-effect coverage audit pass. Exhaustion is now 27 connected families / 10 pending families / 0 disconnected families / 0 disconnected fields.

- CodeUpdate0139 evidence-lineage audit closure: the repository-wide decode audit now includes authoritative Librarium objects and decoded source imports, recognizes five exact compiler-source renames, scans short JSON field names such as NPC `hp`, and classifies compiler-only runtime projection labels as structural. The prior 234 evidence-name flags are closed without using runtime consumers as evidence. Fresh verification covers 104,176 records / 1,974 scalar paths with zero disconnected fields; server Release build passes with zero warnings/errors. Source retirement remains blocked by pending/unresolved records and the outstanding parity/archive gates.

- CodeUpdate0139 generated-field consumption closure: all remaining duplicate formula output labels, WCCMSG2 physical arrays/derived empty flags, room decoded-slot copies, room-item search booleans, and Sysop prose/path notes were removed from generated runtime while immutable Librarium evidence remains. Exact audit proof now recognizes all 1,100 live NPC HP fields despite the generic scanner's two-character cutoff. Native-message, living-world, Sysop/invasion, compiler, server, and core gates pass. Generated-unconsumed paths reach 0; runtime shrinks to 83,969,251 bytes. The remaining 234 flags are consumed-without-field-evidence audit gaps, so parity/source retirement remains blocked.

- CodeUpdate0139 local character/command/manifest compaction: removed race/class `printableSegments` that duplicated canonical names, command `rooms_seen_in` indexes that duplicated retained evidence rows, and diagnostic `value_kinds` arrays from the runtime compile manifest. All 13 races, 15 classes, 25 decoded room-command verbs, and compact family field inventories remain. The stale F11 sidecar audit now validates the consolidated live authority: 95 registrations, 42 aliases, and all 25 generated verbs pass. Generated-unconsumed paths fall from 14 to 10 and runtime output shrinks to 84,662,936 bytes.

- CodeUpdate0139 local Classic correction-family retirement: the eight correction contracts remain immutable Librarium compiler input but no longer emit a duplicate runtime family. Compiler tests prove the 10% Bard shop floor, NPC 1086's 10,000 XP, four FACE mask projections, and item/spell corrections survive. Clean Classic output drops to 32 families / 37 files / 103,119 primary objects; equipment, economy, NPC, and server gates pass. Generated-unconsumed paths fall from 17 to 14.

- CodeUpdate0139 local NPC/spell-message closure: spell 1055 now resolves required WCCMSG2 hooks 2520/2531 and emits the exact native cast and effect-expiration messages. Duplicate per-record Classic-fix annotations and derived NPC spell counts no longer enter generated runtime data; NPC HP remains authoritative. Clean generation exposed and repaired stale Bundle E checks for retired NPC sidecars by validating the compact spawn-derived catalog instead. Live powers, NPC smoke, and Bundle E zero-disconnect audit pass; generated-unconsumed paths fall from 24 to 17.

- CodeUpdate0139 complete weapon equip projection: all 383 generated weapons now receive legal equipment relationships, with decoded weapon types 1/3 marked two-handed and the exact mask/weapon FACE override preserved. `eq <weapon>` and `equip <weapon>` route through the authoritative item engine; bare `eq` remains equipment display. Compiler, live gameplay, and 914-record equipment runtime audits pass.

- CodeUpdate0139 local trigger/script compaction: generated triggers now contain 83 live NPC quest-entry records instead of 367 mixed live/analysis rows, and generated scripts contain 182 executable stages instead of including a tooling catalog. Duplicate script policy/reference twins were removed while complete clauses and operations remain. Full narrative smoke passes; runtime objects fall by 285 to 103,127 and the ledger improves from 284 to 260 flags.

- CodeUpdate0139 local room/exit projection closure: retained canonical toll debit, removed duplicate toll-display and unbound offset-message policy runtime blocks, removed duplicate interaction provenance, and connected decoded repeatability/reset timing to live activation and room-area reset selection. Living-world smoke passes; generated-unconsumed paths fall from 65 to 47 and the conservative ledger falls from 303 to 284.

- CodeUpdate0139 local quest projection compaction: removed duplicate generated quest string references while retaining authoritative numeric IDs, decoded scripts, ordered stages, arguments, relationships, and execution graphs. Compiler regression and 83/83 interaction/narrative execution pass; the conservative ledger improves from 317 to 303 flags and top-level runtime output falls another 261,222 bytes to 86,549,414 bytes.

- CodeUpdate0139 local runtime metadata compaction: recursively removed proven non-runtime enrichment, generator-mapping, validation/test, assumption, override-policy, and version-history blocks from generated gameplay records while preserving all Librarium evidence and metadata with live readers. Classic remains 33 gameplay families / 103,412 objects / 38 files and shrank by 8,637,399 bytes to 86,810,636 bytes. Fresh decode exhaustion reports 317 flags (79 generated-unconsumed, 238 consumed-without-field-evidence); focused builds and runtime smokes pass.

- CodeUpdate0139 local boulder failure closure: textblock 4235 executes message 3309 and spell 1324; generated spell growth fields drive the decoded +5/+7 per-level damage range.

- CodeUpdate0139 local native-message closure: promoted all 3,869 logical WCCMSG2 records, separated `message` from WCCTEXT2 `text` resolution, bound ordered pedestal output, and resolved boulder failure script 4235 through message 3309.

## CodeUpdate0139 — Native presentation reproducibility closure

- Promoted 3,467 verified WCCTEXT2 payload records into the canonical Librarium.
- Added a deterministic, count-gated promotion tool that reads but never modifies the retained WCC default-install evidence.
- Clean Classic compilation now regenerates `native_textblock_payloads.json` as one consolidated runtime file.
- Verified F10 native payload coverage, complete dialogue presentation coverage, and interaction/narrative regression with zero disconnects.

- CodeUpdate0139 Classic economy/service closure: exhaustively classified 178 shops, 27 banks, 44 trainers, 1,204 inventory links, 1,950 item economy records, and 23 rules; connected decoded markup and regeneration percentage/timing to live persistent commerce.

- CodeUpdate0139 Classic spell/status closure: classified 1,350/1,350 generated spells, 105/105 decoded ability IDs, and 111/111 status-effect definitions with zero unclassified records; live damage/heal selection now prioritizes decoded ability vectors.

- CodeUpdate0139 full decoded remoteaction closure: correlated all 139 decoded invocations and 85 distinct signatures to exact WCCMP002 action slots, with zero unresolved or evidence-gated signatures.

## Guardian Hall Engineering Department

- Added the ten-phase Borin engineering capability surface: foundation, tools, workspace, build, validation, runtime, repository analysis, candidate packaging, diagnosis, and autonomous orchestration.
- Added persistent sessions, task queue, dependencies, memory, reports, statistics, events, status, configuration, and deterministic authority enforcement.
- Added separate operator switches for autonomous engineering and live-repository edits; both default off and autonomy never implies live-edit authority.
- Expanded Borin controls into independent switches for autonomous planning, engineering, build, validation, runtime testing, gameplay testing, optimization, documentation, candidate ZIP generation, repository updates, and continuous overnight mode.
- Added a dedicated Emergency Stop that cancels running work and pauses pending work without changing saved capability preferences.
- Added a persisted Engineering Objective and a one-click **Fix Runtime Disconnects** assignment that queues capability-gated reproduction, diagnosis, repair, build, validation, regression, documentation, and candidate ZIP tasks.
- Corrected Guardian Hall engineering counter bindings to explicit one-way mode so read-only memory/report counts cannot trigger a WPF two-way binding startup exception.
- Connected Autonomous Engineering to Borin's configured Forge/LM Studio reasoning engine. Runtime-disconnect evidence now drives a required unified diff that is checked and applied only in the isolated worktree before enabled build, validation, runtime/gameplay regression, documentation, and cumulative ZIP stages.
- Added the latest cumulative candidate ZIP path to Guardian Hall engineering status.
- Corrected Guardian Hall engineering workspace creation to use The Forge's verified repository discovery instead of deriving a path from Guardian Hall's AppData persistence folder.
- Added isolated Git worktree execution and cumulative candidate ZIP output containing modified/new payload files, patch, manifest, validation log, README, and combined install-and-validate batch file.
- Full solution Release compilation passes with warnings-as-errors after connecting generated shop markup to the existing native economy markup authority and resolving strict nullable diagnostics.

## Guardian Hall Phase 3-0164 — Final Hardening and Self-Test

- Added atomic Guardian Hall state writes through a temporary file and replace operation.
- Added a recoverable `.backup` state snapshot before every replacement.
- Added automatic fallback to the backup when the primary state JSON is malformed.
- Advanced Guardian Hall persistence to schema version 7.
- Added a persisted diagnostics workspace and an in-application **Run Hall Self-Test** control.
- Added five-role smoke validation, Wanderer character retention, Worldsmith Live Realm isolation, Archivist preview-only enforcement, scheduler safety, connector URL/timeout bounds, and atomic save/reload verification.
- Added persisted pass/fail results to the Guardian Activity Center.
- Added Phase 3-0164 static validation and updated the Phase 3-0161 validator to accept later compatible schema versions.
- Release compilation remains pending in environments without the .NET 8 SDK.


## CodeUpdate0140 - Repository Cleanup and Imported Evidence Archive

- Retired loose `ImportedDatabases/` and `Evidence/` content from the Git worktree after approved cleanup, leaving README placeholders and preserving the local-only archive `Archive/ImportedDatabasesAndEvidence.LocalOnly.rar`.
- Reaffirmed Librarium as committed gameplay authority and generated `/Data` as runtime output that clean validation regenerates rather than source-controls.
- Preserved required promoted proof anchors in Librarium before archive cleanup, including room environment anchors and gang-house service-location authority.
- Converted runtime save paths from update-numbered folders to realm-scoped roots: `Saves/Classic`, `Saves/Custom/<realm>`, and host account state under `Saves/Host`.
- Enforced the current account/character direction that one login may keep up to three characters per world.
- Local verification after cleanup: `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore` PASS with 0 warnings/errors; `python Tools\validator.py` PASS 15/15.
- GitHub Actions remains pending until CodeUpdate0140 is pushed and confirmed green.

## CodeUpdate0139 - Telnet Runtime Cohesion

- Closed the generated quest-stage remoteaction corpus: its sole signature, `1423.66.0.3`, is executable with zero evidence-gated signatures. Corrected the native argument map so the fourth value is action slot 3 and the second is optional message record 66; WCCMP002 proves reciprocal type-7/state-2 slots in rooms `9/1423` and `9/1425`. Added Librarium-backed paired-door mutation, host execution, persistence, exact result-state journaling, malformed-intent checks, and `--audit-native-remoteactions`.

### Decoded Exit-Action Closure

- Promoted the exact Great Pyramid stone-sphinx answer chains for rooms `12/1920` and `12/2001` into Librarium-backed runtime interactions.
- Bound `ask sphinx fire|flame` and `ask sphinx sun|the sun` only to their decoded map-qualified upward exits.
- Reduced action-gated exits lacking complete executable bindings from 15 to 13; unresolved conditions remain fail-closed.
- Kept the room `9/1012` rubble exit closed because its decoded `testskill strength` prerequisite is not yet executable and must not be bypassed.
- Classified 12 of the 13 remaining closed action gates as their decoded `native_or_passive_room_effect` ownership instead of reporting them as ordinary missing command bindings.
- Added a distinct `native-passive-event-pending` traversal result; these gates remain closed until a verified native event hook changes their shared edge state.
- Implemented the decoded `testskill strength 0 1423` rubble action for `exit.map9.1012.n` with all nine exact rubble/mound/rock command aliases and retryable 1..100 skill rolls.
- Successful rubble tests activate the exact `remoteaction 1012 1840 0 0` edge latch; failed tests preserve the closed state and retain failure textblock `1423` provenance.
- Completed hidden-exit action latches now make their bound hidden exit traversable without requiring an unrelated search-discovery flag.
- Promoted ten exact decoded Map 17 boulder actions with `move|push boulder`, five-second delay metadata, `testskill strength 60 4235`, and their map-qualified remote actions.
- Promoted four exact Map 17 pedestal-item scripts with their room fixtures, required/consumed gems, native message IDs, and remote-action steps.
- Bound the final `exit.map12.2051.u` through NPC `550`'s exact decoded `stars`/`star`/`the stars` command chain and `remoteaction 2051 0 0 8`. The living-world gate audit now binds 206/206 action gates with zero native/passive placeholders.
- Promoted 363 exact map-qualified static room-item placements into one generated family: 102 visible and 261 hidden-searchable across 287 rooms. Rejected seven BBS-only Sysop-room rows and one invalid item-zero row.
- Connected generated static room items to shared living-world state, item-catalog validation, visible room rendering, pickup, visible re-drop, hidden Search discovery, snapshot persistence, and runtime audit counts.
- Added idempotent static-placement maintenance reset with exact restoration counts: ordinary items return even when another copy is character-owned, existing ground placements never duplicate, and decoded positive item `game_limit` values cap world-plus-owned quantities. Retained external scheduling because neither a universal native reset interval nor placement-specific timer linkage is proven.

### GitHub Actions Generated Data Bootstrap Repair

- Kept generated `Data/Classic` out of Git tracking; Librarium remains the committed gameplay authority.
- Updated the GitHub Actions server workflow to restore/build the Librarium compiler and regenerate Classic runtime data before server bootstrap diagnostics.
- Added compiler/runtime chunked-record support for oversized Librarium source catalogs while keeping generated `/Data` local.
- Added the missing Librarium `textblock_command_ability_contracts` source family and allowed NPC AI relationship derivation from generated spawns when the optional derivative `npc_ai_runtime.json` is absent.

### Bundle H Final Gate Repair

- Repaired runtime item ownership validation so bank vault location IDs remain valid while owner identity is still enforced.
- Isolated owner-limited item smoke state so earlier ordinary item checks do not contaminate limited-item proof.
- Added exact `ba` alias routing to `bash` while preserving `bs` for `backstab`.
- Updated living-world smoke to validate against generated data counts and report actual gate kinds / validation signals.
- Split player combat profile smoke checks between stored character fields and derived backstab/quest/dodge formula consumers.
- Hardened the desktop app logger so a temporarily locked `application.log` cannot crash startup or shutdown.
- Added startup failure logging for the WPF startup path before the user-facing error dialog is shown.

Verification:

- `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore -warnaserror` PASS.
- `dotnet test Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj --configuration Release --no-build` PASS.
- `dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build` PASS.
- Bundle H final audit sweep PASS 63/63.
- Final smoke sweep PASS across all resumed segments.
- Published `Reforged.Server.exe` PASS for Classic data audit, item runtime, live gameplay, living-world, player combat profile, and telnet listener startup on `127.0.0.1:4000`.
- Published `MajorMudReforged.exe` PASS for shell/settings/cleanup smoke routes and brief GUI launch/startup check.

Known limitation:

- GitHub Actions remains pending until CodeUpdate0140 is pushed and confirmed green.

### Imported Reference Archive Cleanup

- Created local-only archive `Archive/ImportedDatabasesAndEvidence.LocalOnly.rar` for imported/reference databases and raw evidence.
- Removed loose `ImportedDatabases/` and `Evidence/` contents from the Git worktree, leaving README placeholders only.
- Updated `.gitignore` so the loose imported/evidence folders remain local-only and the archive is not committed.
- Promoted the two required imported WCC room anchors, `room.map1.288` and `room.map1.2306`, into Librarium so clean Classic generation no longer depends on loose imported files.
- Promoted gang-house location authority into Librarium so archived local WCC `.HSE` files are not required during normal compile.
- Updated active continuity/manifests to state that Librarium is committed gameplay authority, `/Data` is generated runtime output, and imported/raw evidence is local-only archive material.

Verification:

- RAR integrity test PASS with WinRAR.
- `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore` PASS with 0 warnings and 0 errors.
- `python Tools\validator.py` PASS 15/15.

- Summarized Bundle H baseline runtime consolidation in active continuity documentation; historical raw logs are retired during repository cleanup.
- Added `search` to the supported runtime command catalog; dispatch was already routed to the live Classic search handler.
- Updated Bundle D search audit semantics to verify Classic transient search reroll/replace behavior instead of the older persisted-discovery expectation.
- Scoped Bundle D door audit validation to door/world-object failures so unrelated living-world categories do not fail the door gate.
- Verified server build PASS with warnings-as-errors after the Bundle H audit repairs.
- Verified Bundle D search, door, and corrective audits PASS after the repairs.
- Verified Bundle A character audit PASS after separating true generated-unconsumed fields from repository-wide evidence-name-only warnings.
- Promoted imported WCC room-anchor environment records for `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` into generated Classic environment data; both were absent only from the MMExplorer-derived environment catalog.
- Updated Bundle D environment audit to require one decoded map-qualified environment row per runtime room and to require gang-house runtime consumption to match the decoded catalog instead of inventing nonzero gang-house rows.
- Added explicit D9 regression checks for the two imported WCC environment anchors and their decoded values.
- Verified Bundle D environment audit PASS with 26,668 runtime rooms, 26,668 decoded environment records, and 0 decoded gang-house environment rows.
- Verified Bundle D final PASS with `AuditPassed=true` and `BundleDComplete=true`.
- Verified Bundle E focused closure audits PASS for companion lifecycle, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service.
- Verified `--audit-bundle-e-zero-disconnects` PASS with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths.
- Recorded Bundle H current gate summary in active continuity documentation; historical raw logs are retired during repository cleanup. Compiler build, server build, the seven focused Bundle H runtime audits, and the gang-house service-location audit pass locally.
- Removed the stale Bundle D corrective/final audit limitation that claimed decoded room environment fields did not reach generated Classic records.
- Added `--audit-bundle-h-gang-houses` to verify native/default WCC gang-house source files, ten MudInfo gang-house maps, ten generated deed items, 22 generated gang-house/deed shop records, and the runtime deed/upkeep protection service.
- Promoted a WCC shop API reference-backed gang-house service-location catalog into `Librarium/Objects/gang_houses/gang_house_records.json` and generated `Data/Classic/gang_houses.json`.
- Added runtime catalog loading through `RuntimeClassicGangHouseCatalog`, marked `gang_houses` as a required Classic generated family, and wired the generated catalog into `RuntimeClassicGangHouseProtectionService`.
- Verified `--audit-bundle-h-gang-houses` PASS with `RuntimeGameplayReady=true` for 10 Classic houses, 20 service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted service locations resolve to runtime rooms and current/expired/non-gang-room cleanup protection checks pass.
- Recorded the remaining gang-house limits: `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, ordinary interior house ownership coverage and live deed acquisition/upkeep mutation remain evidence-blocked, MudInfo maps remain reference-only, and the listed `LibrariumLocal` handoff graph/index files are absent from the repo.
- Connected live combat targeting to shared `RuntimeLivingWorldService` NPC instances instead of static spawn definitions.
- Added live NPC instance identity to active combat encounters.
- Made combat rounds refresh target HP from living-world state and apply NPC damage back to living-world state.
- Made spell/ability NPC targeting resolve visible live NPC instances and synchronize NPC damage/healing with living-world state.
- Added the combat movement guard to dispatcher movement handling so connected sessions cannot walk away from active combat.
- Added CodeUpdate0139 Bundle 3 Classic runtime audit tooling:
  - `--audit-classic-data`
  - `--audit-classic-runtime`
  - `--audit-room "<room>"`
  - `--audit-spawns`
  - Host command: `audit room <room>`
- Added startup runtime audit output for `--run-local` and `--run-network`.
- Recorded audit evidence showing generated room descriptions are missing before runtime/Telnet.
- Recorded audit evidence showing live NPC/monster instantiation is active globally.
- Added compiler-side native map DAT room-text recovery diagnostics through `audit-room-text` and `export-room-text`.
- Promoted 2,417 binary-backed room descriptions into `Librarium/Objects/room_descriptions/native_map_room_description_records.json`.
- Added OmniVerse map-qualified room-description recovery for 800 canonical rooms not already covered by native binary evidence.
- Added exhaustive WCCMP002 default-install binary room-description recovery for 23,309 additional canonical rooms, including the user-approved repeated-room rule that same-title duplicate rooms share one description.
- Added WCCMP002 placeholder room-description recovery for the final 140 placeholder-named rooms. WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this pass, and WBBMP002 corroborates all 140 recovered titles and first description lines.
- Updated compiler room-description loading to merge all Librarium room-description files while keeping native/binary records ahead of reference gap fills.
- Consolidated the read-only WCC Librarium exhaustion audit into `Reports/DataDecode/decoded_data_evidence_summary.md`.
- Reconciled MMExplorer SQL/MDB and OmniVerse public codex data into Librarium as reference-backed gap fills.
- Added `public_reference` and `mmexplorer_sql_reference` blocks to matched item, spell, NPC, and shop records.
- Added unmatched MMExplorer supplement files for item, spell, and NPC rows that do not map to active canonical records.
- Added OmniVerse-backed ability records under `Librarium/Objects/abilities/ability_records.json`.
- Verified required room-description probes for `room.map1.2141`, `room.map1.2140`, `room.map1.488`, and `room.map1.348`; regenerated live `Data/Classic` now audits with 26,668 rooms with descriptions and 0 rooms without descriptions.
- Added centralized command abbreviation resolution with exact aliases, unique-prefix matching, and ambiguity rejection for unsafe prefixes.
- Repaired live spawn materialization so grouped spawn candidates no longer explode into every room occupant; live NPC instances now require exact map-qualified spawn room resolution.
- Removed legacy room-number fallback from spawn placement and added hard placement validation for unresolved spawns.
- Expanded `audit room` and `audit spawns` output to prove each displayed entity's definition ID, spawn ID, source room, resolved runtime room, live `RoomId`, alignment, disposition, and combat hostility state.
- Updated room rendering to classify visible entities by runtime disposition instead of treating every high-HP NPC as hostile.
- Added a living-world regression check for `room.map1.21` proving the Guild/River room does not leak storm giants, woodelf entries, bosses, or unrelated group candidates.
- Current generated evidence for `room.map1.21` resolves to `guardsman` (`npc.14`) with neutral disposition; the user-expected `elite guardsman` requires a separate evidence-backed Librarium/data correction if the generated room source is wrong.
- Added cumulative MMExplorer room source, room interaction, exit semantic, edge state, latch binding, room reset/dependency, and trigger/script/quest evidence under `Librarium/`, `Data/Classic/`, `Evidence/`, `Reports/DataDecode/`, and `Tools/`.
- Resolved the remaining 7 distinct native/passive room-effect linkage gaps across 12 exit rows as evidence-bounded native/passive effects without inventing player commands.
- Added MegaMUD `/mp` route replay to the room native effect resolver; replay found 0 command hits for the affected map-qualified exits, so all repaired rows remain `resolved_without_player_command`.
- Added `Librarium/Objects/room_messages/room_message_records_catalog.json` and generated `Data/Classic/room_messages.json` with 117 unique native WCCMSG offset-backed room message records.
- Decoded room message categories for room action aliases/success text, doors, hidden exits, locks/keys, searches, and generic failures.
- Consolidated room native effect/message decode reporting into `Reports/DataDecode/decoded_data_evidence_summary.md`.
- Updated the Librarium compiler catalog reader to recognize plain `*_records.json` catalog files and avoid treating source-only `room_descriptions` files as a compiled family.
- Loaded `MajorMUD_Reforged_Cumulative_Update_0206.zip` into Librarium as current quest dialogue dispatch source, without copying package cache files or treating package `/Data` as canonical authority.
- Added 183 script records to `Librarium/Objects/scripts/mmexplorer_textblock_stage_records.json`, including 182 decoded MMExplorer textblock stage scripts.
- Added merged trigger source `Librarium/Objects/triggers/mmexplorer_room_interaction_records.json` with 284 room/exit interactions plus 83 NPC quest-entry triggers.
- Added consolidated runtime quest dialogue execution policy at `Librarium/Policies/runtime_quest_dialogue_execution_policy.json`.
- Connected runtime `talk <npc>` to generated NPC quest-entry triggers and decoded stage scripts through room-scoped live NPC lookup.
- Added atomic decoded quest clause execution for conditions, text/message branches, quest flags, item give/take, experience, alignment, and host world intents for cast/summon/teleport/remoteaction.
- Bound generated random textblock tables into runtime quest dialogue execution using the decoded independent 1..100 threshold semantics.
- Added generated world-intent target validation for quest `cast`, `summon`, `teleport`, and `remoteaction` operations.
- Added host-side quest intent commits for teleporting the active character/session and materializing summoned generated NPCs into the living world.
- Added host-side quest remoteaction event recording in the living-world journal, preserving exact decoded metadata while marking unknown native mutation as evidence-gated.
- Added durable living-world event recording for committed room/object interactions.
- Imported the user-provided final native closure/WCC decode archive as repo evidence and Librarium/reference material.
- Added new native decode evidence, reports, tools, manifests, and Librarium evidence/reference files from the archive.
- Added `Native*` server registry source files from the archive; they compile with the current server runtime.
- Merged the 241 user-confirmed final native closure server implementation slices into active runtime code and consolidated duplicate contracts instead of retaining parallel reference copies.
- Recorded archive intake details at `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Activated verified/native and strongly-correlated Librarium sidecar data in executable generated output where evidence supports it.
- Updated the Librarium compiler to merge active `room_interactions` sidecars and to record multi-source family provenance in generated manifests.
- Added compiler cross-family validation that blocks unresolved exit targets instead of allowing stale `active_runtime` traversal.
- Added compiler smoke assertions for 367 generated triggers, strongly-correlated room-interaction sidecars, and blocked unresolved exits.
- Replaced the read-only Librarium dashboard with a navigable Classic data browser in the Reforged app. The Librarium tab now has Weapons, Armor, Classes, Races, Items, Monsters, and Rooms tabs, selected-tab search, name-only record lists, and curated game-facing stat details without evidence/source metadata.
- Changed parchment tab/list/grid text to dark readable text so the Librarium tab is legible.
- Reworked the Client play surface toward the MegaMUD-style flow: the Play tab now selects server/session profiles, `Open Terminal` launches a dedicated black terminal window, and client settings live in a separate tabbed options window behind menu/actions instead of dominating the main play screen.
- Added required Male/Female selection during Telnet character creation and persisted the selected sex on `RuntimeCharacterRecord` while keeping existing saves readable.
- Fixed the compiler room-description source matcher so plain `*_records.json` Librarium room-description catalogs, including the WCCMP002 1.11p corpus, are included in generated `Data/Classic`.
- Regenerated Classic data and verified compiler/runtime audits now report 26,668 rooms with descriptions and 0 rooms without descriptions.
- Fixed generated numeric-string handling in runtime quest dialogue arguments and item catalog numeric fields, removing the server smoke crash that reported a JSON `String` where a `Number` was expected.
- Added `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md` to track the remaining native decode work after the current server/runtime audit.
- Fixed `GuardianServices.cs` missing `System.IO` import so the full solution build passes.
- Hardened generated quest reset semantics so Classic completed quests cannot be reset or repeated on the same character.
- Expanded interaction/narrative smoke coverage for random table execution, cast/summon events, remoteaction preservation/event recording, room/object event recording, world-intent target validation, edge opcode counts, and exhaustive dispatch validation across all generated NPC quest-entry triggers.
- Added generated quest state command handling for `journal`, `quest`, `accept`, `abandon`, `complete`, and `quest reset`.
- Added interaction/narrative smoke coverage for 182 decoded stage scripts, 83 NPC quest-entry triggers, 182 linked stage script references, branch dispatch, item consumption/reward transition, and failure/no-mutation behavior.
- Added `server_interaction_narrative` to `Tools/validator.py`.
- Installed Decode Exhaustion Bundle F Revision 0033 into the local dirty CodeUpdate0139 repository without commit or push.
- Fixed Bundle F runtime/audit integration issues found after overlay: nested decoded-description projection, exact NPC corpse id/gold transfer, non-stackable smoke samples, presentation-only graph traversal, variable operand role projection, Witchhunter ability mapping, optional world reward activation terms, nullable NPC dialogue names, and current NPC dialogue execution counts.
- Installed Decode Exhaustion Bundle G Revision 0002 into the local dirty CodeUpdate0139 repository without commit or push.
- Closed the F10 native presentation payload gap with evidence-backed mappings for verified no-output decoded text nodes and exact WCCMSG message payloads for `message 2044` and `message 3290`; unknown message IDs still fail closed.
- Verified Bundle F through F15 locally after Bundle G: F10 native payload audit reports 0 disconnects, and F10 complete, F11 commands, F12 random tables, F13 events/missions, F14 rewards, and F15 persistence all pass.
- Preserved `Saves/` and generated `/Data` authority boundaries. Imported/reference databases and raw evidence are retained as the local-only archive `Archive/ImportedDatabasesAndEvidence.LocalOnly.rar`.

Verification:

- `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore`
- `dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore`
- `dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-classic-data`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-classic-runtime`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- audit-room-text`
- `python Tools\Importer\audit_wcc_librarium_gaps.py --repo .`
- `python Tools\Importer\promote_reference_sources_to_librarium.py --repo .`
- `python Tools\Importer\recover_reference_room_descriptions.py --repo .`
- `python Tools\Importer\recover_wccmp002_room_descriptions.py --repo .`
- `python Tools\Importer\recover_wccmp002_placeholder_room_descriptions.py`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicReferenceReconcile0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data/CompilerSmokeTest/ClassicRoomText0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicRoomDescriptionReference0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicWccmp002DuplicateRule0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicRoomDescriptionComplete0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release -- build --world Classic --clean --output Data\Classic`
- `python Tools\Importer\resolve_room_native_effect_messages.py --repo .`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-interaction-narrative`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-live-gameplay`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-living-world`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-room "room.map1.21"`
- `dotnet run --project Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj --configuration Release`
- `dotnet build Apps/Source/Reforged.App/Reforged.App.csproj --configuration Release`
- `python Tools\validator.py`
- `python _codex_staging\Decode_Exhaustion_Bundle_F_Revision_0033\Decode_Exhaustion_Bundle_F_Revision_0033\Validation\validate_bundle_f_revision_0033.py _codex_staging\Decode_Exhaustion_Bundle_F_Revision_0033\Decode_Exhaustion_Bundle_F_Revision_0033`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-e-zero-disconnects`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f10-npc-dialogue-execution`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f10-native-payloads`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f10-complete`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f11-commands`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f12-random-tables`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f13-events-missions`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f14-rewards`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f15-persistence`
- Single validator PASS 15/15 at `Reports/RuntimeVerification/validator_latest.md`
- Latest validator PASS includes `librarium_catalog`, `generation_readiness`, solution restore/build, compiler tests, core smoke, Classic data audit, server generated data, server room exits, server NPCs, and server live gameplay checks.
- Latest validator PASS also includes `server_interaction_narrative`.
- Latest `server_interaction_narrative` PASS includes generated random textblock execution, world-intent target validation, cast/summon/remoteaction coverage, Classic non-repeat quest guard coverage, and exhaustive NPC quest-entry dispatch validation.
- Latest living-world smoke PASS includes quest remoteaction event recording and generated room/object interaction event recording.
- Final native closure archive intake verification: server Release build PASS, test Release build PASS, Classic compiler/runtime audits PASS with 26,668/26,668 room descriptions, interaction/narrative smoke PASS with 83 executable quest entries, and `python Tools\validator.py` PASS 15/15 after import, runtime audit repair, and imported/evidence archival cleanup.

Known limitation:

- Room-description coverage is 26,668/26,668 after WCCMP002 placeholder recovery and compiler regeneration.
- WBBMP002 is retained as secondary comparison evidence only and does not override WCCMP002 room/map source records.
- Spawn placement validation now skips and reports unresolved map-qualified spawns instead of attaching them to fallback rooms. Current unresolved placement records are `spawn.288.43` (`room.map1.288`) and `spawn.2306.213` (`room.map1.2306`).
- The 7 repaired room native/passive effects are closed as linkage classifications, not as discovered player commands. MegaMUD `/mp` evidence did not prove command bindings for those exact exits.
- `remoteaction` is recorded as a durable host event with exact decoded metadata; its native remote mutation is not guessed without additional evidence.
- Native closure implementation slices are now active runtime code after merge/replace cleanup; remaining limitations are compile warnings and GitHub Actions pending after push.

GitHub Actions is pending until CodeUpdate0140 is pushed and confirmed green.

## CodeUpdate0138 - Server Runtime and Host Enablement

- Closed the local CodeUpdate0138 baseline.
- Integrated server runtime and host enablement surfaces.
- Added/updated host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit coverage.
- Integrated live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world-system smoke surfaces.
- Promoted map-qualified MMExplorer movement records through Librarium and compiler-generated `/Data`.
- Applied MMExplorer textblock quest-flow fix 0142.
- Updated Librarium quests to 2,183 total quest records, including 83 `npc_textblock_quest_flow` records.
- Added the 0142 textblock quest validator to the complete runtime verification gate.
- Consolidated active handoff docs to the compact root/docs surface.
- Removed copied update package folders, generated Python caches, compiler smoke output, and smoke-save artifacts from the working tree.

Verification:

- `python Tools\validator.py`
- PASS 68/68

GitHub Actions is pending until pushed and confirmed green.

One-time validation proofs are recorded in ValidationManifest.json.

## GuardianHallPhase2-0149 - Dedicated Guardian Workspaces

- Replaced the shared Guardian assignment panel with five role-specific fantasy command-center workspaces.
- Made Realm Keeper global across connected hosts and world categories with no world or character assignment.
- Retained Wanderer server/world/realm/character assignment and player-like mission policies.
- Removed character control from Analyst and Worldsmith and added analysis/report and proposal/simulation workflows.
- Converted Archivist into a Librarium evidence workbench with multi-file/folder/drag-drop intake, decode queue, confidence and relationship records, preview-only knowledge output, Markdown/HTML/JSON/PDF reports, Codex package export, and an editable local-tool registry.
- Replaced generic assignment persistence with versionable role-specific Guardian Hall state persistence.
- Preserved Forge/Borin as a separate software-engineering surface.

## GuardianHallPhase3-0150 - Realm Keeper Runtime Telemetry

- Added an evidence-based local telemetry service for the global Realm Keeper workspace.
- Added initial, manual, and 30-second periodic telemetry refresh.
- Wired generated world/record counts, save activity, validator state, build reports, runtime logs, and crash/error evidence into Realm Keeper health and activity panels.
- Added observable telemetry metrics and a bounded persisted realm timeline.
- Kept LAN and Internet host scopes configuration-only until authenticated host telemetry endpoints exist.
- Preserved read-only client behavior and made no Librarium or server-state mutations.

## Guardian Hall Phase 3-0151
- Added Wanderer local character/session telemetry over repository Saves and generated room records.
- Added session refresh, evidence-backed route/inventory/progress summaries, and dynamic mission queue updates.
- Preserved Wanderer character assignment and avoided false live-session claims.

## Guardian Hall Phase 3-0152 — Analyst Runtime Data Wiring

- Added a character-free Analyst telemetry service that scans generated runtime data by selected analysis target.
- Added target mappings for combat, economy, drops, NPC balance, quest balance, experience curve, item balance, and progression.
- Added measured source-file, source-record, and existing-report counts.
- Added read-only graph readiness, historical baseline, data provenance, refresh status, and refresh time.
- Added manual Analyst data refresh and initial refresh when Guardian Hall loads.
- Expanded Analyst Markdown exports with measured counts and runtime status.
- Preserved Librarium authority and prohibited Analyst runtime or character mutation.

## Guardian Hall Phase 3-0153
- Wired Worldsmith to generated-data baselines by content family.
- Added safe test-realm inspection, persisted proposal previews, and non-mutating simulation reports.
- Blocked live-realm proposal and simulation targets; no runtime apply path exists.

## Guardian Hall Phase 3-0154 — Archivist Evidence Decode Pipeline

- Added individual and queue-wide Archivist decode commands.
- Added SHA-256 evidence identity, file-size capture, bounded inspection, and persisted findings.
- Added structured JSON, XML, CSV, text, WCC/WBB, DLL, MDB, and BIN inspection paths.
- Added preview-only evidence artifacts, queue relationships, Librarium preview summaries, and saved evidence snapshots.
- Preserved Librarium authority: no decode or save action overwrites Librarium content.

## Guardian Hall Phase 3-0155 — Archivist Reports and Local Tools

- Expanded Archivist reports into complete Markdown, JSON, HTML, and PDF investigation outputs with decoded findings, evidence, confidence, relationships, suggested-runtime cautions, and open questions.
- Expanded Codex exports into a multi-file handoff package with investigation summary, decoded structures, evidence, relationships, suggested runtime, open questions, and a package manifest.
- Added persisted editable local-tool working directories, enable/disable state, validation status, last launch time, and launch errors.
- Added executable and working-directory validation before launching local tools.
- Added `{file}`, `{folder}`, and `{root}` argument tokens without hardcoded paths.
- Preserved preview-only Librarium behavior and introduced no runtime promotion or silent overwrite path.

## Guardian Hall Phase 3-0156 — Worldsmith Approval-Controlled Staging

- Added a generated approval code and separate entered-code confirmation gate.
- Bound approvals to the exact proposal SHA-256 and safe target realm.
- Added persisted approval artifacts, isolated test-realm staging, apply receipts, and rollback backups.
- Blocked Live Realm, disabled approval bypass, and rejected proposals that differ from the approved source.
- Declared and validated that staging does not mutate runtime data or Librarium content.

## GuardianHallPhase3-0157 — Phase 1 Persistence Migration
- Added schema-versioned Guardian Hall state.
- Added one-time migration from legacy `GuardianHall/assignments.json`.
- Preserved the legacy file as `.phase1-backup` and wrote a migration receipt.
- Mapped generic Phase 1 fields into role-specific workspaces without introducing character control for Analyst, Worldsmith, or Archivist.
- Preserved Wanderer character assignment and protected Worldsmith from migrated Live Realm targets.

## Guardian Hall Phase 3-0158
- Added persisted Guardian scheduling, job history, restart recovery, cancellation, pause/resume, and role-safe execution.
- Added Guardian Activity Center UI.
- Kept Worldsmith apply operations manual and approval-gated.

## Guardian Hall Phase 3-0159
- Added persisted configurable LAN and Internet Realm Keeper telemetry endpoints.
- Added bounded authenticated HTTP telemetry using bearer tokens read only from named environment variables.
- Added endpoint status, last-contact, and error tracking with unavailable hosts excluded from connected totals.
- Merged verified remote world, population, event, and health data with local repository telemetry.

## Guardian Hall Phase 3-0160
- Added authenticated live Wanderer session telemetry and guarded one-step mission dispatch.
- Added environment-only bearer authentication, bounded timeout, endpoint configuration, and local-evidence fallback.
- Advanced Guardian Hall persistence schema to version 5.

## Guardian Hall Phase 3-0161
- Added persisted Analyst historical snapshots and prior-baseline comparisons.
- Added rendered family distribution series and Markdown/JSON/CSV export packages.
- Added validated configurable Analyst output locations.

## Guardian Hall Phase 3-0162
- Added indexed Archivist evidence search across decoded names, paths, hashes, findings, confidence, and relationships.
- Added selectable evidence results with direct and reverse cross-reference inspection.
- Added bounded multi-term search and clear-search controls.
- Preserved preview-only Librarium behavior and read-only evidence browsing.

## Guardian Hall Phase 3-0163
- Added distinct visual identity styles and purpose badges for all five Guardian chambers.
- Preserved all role-specific controls and kept Forge/Borin separate.

## CodeUpdate0139 — MegaMUD Classic Terminal Presentation
- Rebuilt the dedicated Client terminal window around the verified MegaMUD workflow: File/Edit/View/Action/Options/Help menu bar, compact one-click auto-option toolbar, conversation selector, black primary terminal, right-side map/items/character tools, classic command line, and blue status bar.
- Preserved all existing Reforged session, automation, route, parsing, coordination, map, and Librarium services; the change is Client presentation and command wiring only.
- Added command-history navigation, quick gameplay commands, reconnect, route controls, optional modern side panels, and channel synchronization between the conversation list and output tabs.
### CodeUpdate0139 — WCCMMUD SYSOP command closure

- Added a 20-root generated SYSOP/GAMEOP catalog from decoded native documentation.
- Added authenticated `SYSOP` dispatch with Moderator GAMEOP scope and full Sysop/Administrator scope.
- Connected safe diagnostics, room spawn reset, realm broadcast, active-session save, and arena-mode controls.
- Preserved the configured legacy unlock as evidence without accepting it as a plaintext command.
- Added a 13-check focused smoke gate and explicit fail-closed reporting for incomplete native behavior.
- Recovered and connected persistent `GOTO NewHaven` and `GOTO Support` destinations.
- Recovered all six native support-room invasion phrases and complete textblock 2454 scripts; retained live execution as an explicit projection blocker.
- Projected the six support-room invasion commands into generated room interactions and live recognition, with an explicit non-mutating error for the 25 absent random child textblocks.
- Recovered those 25 placement children plus six random NPC-mixture tables from the exact WCCUPDA2 database-update pages, promoted them reproducibly into Librarium, and activated authenticated Support Chamber invasion execution with authoritative summons, one-use gate consumption, and cleanup-cycle removal/reset.
- Closed every named WCCMMUD GOTO destination using exact map-qualified mappings, including user-confirmed SilverMere `1/2189`, Rhudaur `2/2519`, Khazarad `6/1249`, and Lost City `16/426`.
- Removed duplicated engine-side WCCMMUD GOTO and invasion gameplay tables. Production now executes generated SYSOP targets and generated support-room interaction definitions directly; focused closure passes 17/17 and the conservative decoded-field disconnect count falls from 345 to 339.
- Reconciled all 35 unresolved spawn labels: resolved 28 exact no-spawn NPC names from generated NPC IDs and classified seven decoded monster-group-9 population references without expanding grouped candidates into live rooms. Living-world counts remain 962 generated spawns and 1,015 live NPC instances.
- Compacted every generated runtime world by retaining pending quest candidates and formula probes in Librarium only. Classic runtime quests drop from 2,183 to 83 executable flows and formulas from 36 to seven, removing exactly 2,129 non-runtime records while quest and combat smokes remain green.
- Retired the unconsumed 117-record room-message offset catalog from runtime generation while preserving it in Librarium. Exact comparison found 59/62 unique strings already represented by executable interactions/native payloads; three unbound offset strings remain evidence-only. Classic now emits 33 gameplay families, 103,412 objects, and 38 files; native-message coverage is green.

## Update 0309 — Pass 38 Classic scheduler boundary
- Promoted `native.clock-scheduling` UNKNOWN -> STRONG by separating independently recovered owner clocks from unsupported global-phase assumptions.
- Centralized existing 5s combat, 3s spell/system, and 30/20/10s regeneration cadence constants in `RuntimeClassicTimingAuthority`.
- Did not promote a universal 10-second tick, player-first NPC phase, cleanup/spawn order, or same-cycle cross-owner ordering.
