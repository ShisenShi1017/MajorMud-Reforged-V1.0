# MajorMUD Parity Scorecard

- PASS: **144**
- STRONG: **56**
- UNKNOWN: **0**
- Scored assertions: **200**
- Strict parity: **72.0%**
- Evidence-backed parity: **100.0%**

| Category | PASS | STRONG | UNKNOWN | Total | Evidence-backed |
|---|---:|---:|---:|---:|---:|
| Administration & Host | 1 | 1 | 0 | 2 | 100.0% |
| Character & Status | 39 | 7 | 0 | 46 | 100.0% |
| Combat | 16 | 2 | 0 | 18 | 100.0% |
| Commands & Help | 10 | 3 | 0 | 13 | 100.0% |
| Data Reconstruction | 0 | 3 | 0 | 3 | 100.0% |
| Economy & Services | 11 | 5 | 0 | 16 | 100.0% |
| Host / Persistence / Worldgroup | 4 | 5 | 0 | 9 | 100.0% |
| Import / Export / Limits / Recovery | 2 | 2 | 0 | 4 | 100.0% |
| Items & Inventory | 4 | 2 | 0 | 6 | 100.0% |
| NPC & AI | 7 | 6 | 0 | 13 | 100.0% |
| Optional / Rankings / Extensions | 0 | 3 | 0 | 3 | 100.0% |
| Parties / Communication / Organizations | 15 | 0 | 0 | 15 | 100.0% |
| Protocol & Legacy Clients | 11 | 0 | 0 | 11 | 100.0% |
| Quests / Scripts / Triggers | 0 | 2 | 0 | 2 | 100.0% |
| Rooms & World | 14 | 7 | 0 | 21 | 100.0% |
| Spells & Effects | 1 | 2 | 0 | 3 | 100.0% |
| Timers / Resets / World Rules | 9 | 6 | 0 | 15 | 100.0% |

## Remaining UNKNOWN assertions

None.

## CodeUpdate0358 native LVLDIFF / working-install promotion

- Before: **143 PASS / 57 STRONG / 0 UNKNOWN** (**71.5% strict**).
- After: **144 PASS / 56 STRONG / 0 UNKNOWN** (**72.0% strict**).
- Promoted: `pvp.realm-level-range-configuration`.
- The retained default 1.11p install and the user-supplied working 1.11p Worldgroup/MajorMUD installation use the **byte-identical WCCMMUD.DLL** (`5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077`).
- Default configuration stores `LVLDIFF=100`; the working installation stores `LVLDIFF=10`, proving site-configurable persistence without changing the executable.
- Native setup constrains `LVLDIFF` to **-1..100**: `-1` disables PvP completely, `0` permits same-level PvP only, and a lower-level FIEND does not receive the normal level-difference protection.
- Reforged now uses the native sentinel/default instead of a separate Classic disabled-by-default assumption and avoids reapplying generic Reforged same-party/guild/range policy after native admission.
- The user-supplied Worldgroup installation is **evidence only**; Reforged does not depend on Worldgroup.
- Direct working-install/native verification: **14/14 PASS**.
- MegaMUD and Borin remain ordinary-stream/server-authoritative consumers; no private PvP admission shim was added.

## CodeUpdate0356 native raw-Dodge promotion

- Before: **141 PASS / 59 STRONG / 0 UNKNOWN** (**70.5% strict**).
- After: **143 PASS / 57 STRONG / 0 UNKNOWN** (**71.0% strict**).
- Promoted: `character.raw-dodge-formula`.
- Original `_move_player_to_fighter` calls `_get_user_ability_value(...,34,...)`, stores that result in fighter Dodge, then adds `trunc(Level/5)`, `trunc((Charm-50)/5)`, and `trunc((Agility-50)/3)`.
- For living characters with encumbrance below 33%, native adds `10-trunc(encumbrancePercent/10)` to that same fighter field; `_calculate_attack` later consumes it for the already-PASS stock Dodge resolver.
- This direct callsite supersedes the older 0334 limitation that had not yet located the matching raw-Dodge base-stat/encumbrance path.
- MegaMUD/Borin remain ordinary-stream, server-authoritative consumers; no private raw-Dodge formula is added.
- Direct original-DLL/MegaMUD verification: **40/40 PASS**.

## CodeUpdate0355 native physical combat promotions

- Before: **139 PASS / 61 STRONG / 0 UNKNOWN** (**69.5% strict**).
- After: **141 PASS / 59 STRONG / 0 UNKNOWN** (**70.5% strict**).
- Promoted: `combat.backstab-damage-formula` and `combat.physical-damage-dr-order`.
- Original `_move_player_to_fighter` directly closes Backstab min/max endpoint construction, ability 117/118 bonuses, the race-only 75% penalty, and `(Level+100)%` Classic scaling.
- Original `_calculate_attack` directly closes attack-type scaling, hit/critical ordering, damage roll, DR subtraction, Bash/Smash post-DR multiplication, Backstab Dodge /5, Dodge resolution, and final non-positive clamp.
- Reforged moved the Bash/Smash post-DR multiplier before Dodge to match the exact native branch order.
- Raw player Dodge construction, full class/base accuracy construction, and non-default Speed integration remain separate STRONG assertions.
- MegaMUD/Borin remain consumers of the ordinary server-authoritative combat stream; no private combat formula shim was added.
- Direct original-DLL/MegaMUD verification: **45/45 PASS**.

## CodeUpdate0354 native BUY/SELL Charm pricing promotions

- Before: **137 PASS / 63 STRONG / 0 UNKNOWN** (**68.5% strict**).
- After: **139 PASS / 61 STRONG / 0 UNKNOWN** (**69.5% strict**).
- Promoted: `economy.buy-charm-adjustment` and `economy.sell-charm-adjustment`.
- Original `_buy_item` directly loads effective Charm from character `+0xAC`, divides by 5, and applies `110-floor(Charm/5)` after the `Markup+100` integer stage; Charm 0 therefore uses a 110% factor.
- Original `_sell_item` loads the same Charm field, shifts right by one, adds 25, and computes `floor(baseCopper*(floor(Charm/2)+25)/100)` with no shop markup; Charm 0 therefore sells at 25%.
- These pricing callsites are now independent of the still-separate universal character modifier-order question.
- Imported MegaMUD contains ordinary `buy %s` / `sell %s` command templates; Borin remains server-authoritative for committed price/currency outcomes.
- Direct original-DLL/MegaMUD verification: **29/29 PASS**.

## CodeUpdate0353 native GIVE capacity/presentation promotion

- Before: **136 PASS / 64 STRONG / 0 UNKNOWN** (**68.0% strict**).
- After: **137 PASS / 63 STRONG / 0 UNKNOWN** (**68.5% strict**).
- Promoted: `transactions.classic-give-capacity-restrictions`.
- Original `_cmd_give` plus `_add_item_to_inventory` close item-level restriction ordering, `SET RECEIVE OFF`, native 100 carried-root slots, encumbrance admission, exact capacity rejection, and exact three-perspective success presentation.
- Successful room-observer GIVE text is **High priority** (priority 0), matching the retained 0349 `_tell_room` census.
- MegaMUD independently contains `You cannot carry that much` and `You just gave ` tokens; Borin remains on authoritative inventory/client-data state and requires no private GIVE shim.
- Direct imported-native/MegaMUD verification: **24/24 PASS**.

## CodeUpdate0352 character-only BREAK promotion

- Before: **135 PASS / 65 STRONG / 0 UNKNOWN** (**67.5% strict**).
- After: **136 PASS / 64 STRONG / 0 UNKNOWN** (**68.0% strict**).
- Promoted: `commands.break-autocombat`.
- Native `_kill_autocombat(user,1)` clears only owner slots belonging to that user; `_kill_autocombat_against` is a separate routine and BREAK does not call it.
- Reforged PvE now leaves NPC threat active after BREAK, and PvP tracks independent autocombat state for both characters.
- The shared client/Borin parser distinguishes local autocombat-off from continued incoming combat, preventing automatic offense from silently undoing BREAK.
- Direct imported-native/MegaMUD verification: **21/21 PASS**.

## CodeUpdate0351 native BREAK presentation + stock extension-scope promotions

- Before: **133 PASS / 67 STRONG / 0 UNKNOWN** (**66.5% strict**).
- After: **135 PASS / 65 STRONG / 0 UNKNOWN** (**67.5% strict**).
- Promoted: `transactions.no-native-trade-escrow` and `economy.no-native-repair-service`.
- Original `WCCMMUD.DLL` exposes 78 native `_cmd_` handlers including `_cmd_give` and `_cmd_auction`, with no `_cmd_trade` or `_cmd_repair`; the fully classified Classic service corpus contains 204 services and zero repair owners.
- BREAK presentation is tightened but intentionally remains STRONG: active manual BREAK now emits `*Combat Off*` plus `<name> breaks off combat.` at High priority before teardown, matching the native display-before-`_kill_autocombat` order. Exact opponent-side PvP autocombat ownership remains unresolved.
- MegaMUD independently contains `*Combat Off*`, ` breaks off combat`, and help saying it issues `break` before running; Borin's shared Classic parser already treats `*Combat Off*` as authoritative disengagement.
- 0351 direct imported-native/MegaMUD evidence verification: **27/27 PASS**.

## CodeUpdate0350 native REST/MEDITATE promotion

- Before: **132 PASS / 68 STRONG / 0 UNKNOWN** (**66.0% strict**).
- After: **133 PASS / 67 STRONG / 0 UNKNOWN** (**66.5% strict**).
- Promoted: `native.rest-inn-recovery-policy`.
- Original `WCCMMUD.DLL` closes the remaining sickness/full-resource/state-entry presentation: `+0xBE` sickness rejection, no full-HP REST rejection, full/over-full mana MEDITATE rejection/clamp, and exact player/observer text.
- The sickness field is cross-checked by native slow-update `You feel ill.` damage and Cure Poison ability 20 reducing the same field; Reforged maps the gate to decoded active poison rather than unrelated status effects.
- MegaMUD executable inventories independently recognize the native REST/MEDITATE strings.
- The shared Reforged client/Borin parser now recognizes both `You are now resting.` and `You are now meditating.`.
- 0350 direct native evidence verification: **24/24 PASS**; repository static integration verifier: **96/96 PASS**.

## CodeUpdate0349 native SET MESSAGES promotion

- Before: **131 PASS / 69 STRONG / 0 UNKNOWN** (**65.5% strict**).
- After: **132 PASS / 68 STRONG / 0 UNKNOWN** (**66.0% strict**).
- Promoted: `native.communications.persistence-logging`.
- Native `_cmd_set` stores High=0, Medium=10, Low=20; `_tell_room` filters each recipient against that saved threshold.
- LOW pickup/drop and ordinary physical-combat observer detail, MEDIUM spell observer detail, and HIGH important room events are now connected to the recipient-side gate.
- MegaMUD receives the same ordinary filtered terminal stream; Borin respects the player setting and does not force HIGH.
- Worldgroup-only transport/mail/presentation/logging remains outside Reforged parity scope.

## CodeUpdate0348 Reforged host-scope correction + native builder promotion

- Before: **130 PASS / 70 STRONG / 0 UNKNOWN** (**65.0% strict**).
- After: **131 PASS / 69 STRONG / 0 UNKNOWN** (**65.5% strict**).
- Promoted: `native.builder-content-editing`.
- Original WCCMMUD.DLL exposes 78 unique `_cmd_` handlers with no world-content builder; the retained 20-root SYSOP catalog also contains no room/item/NPC/world editor.
- Worldgroup PAGE/telegram transport, host presentation queues, mail storage, and host logging are explicitly outside Reforged parity because Reforged is not hosted by Worldgroup.
- `native.communications.persistence-logging` remains STRONG only for exact player-visible `SET MESSAGES` per-message priority/filter integration.
- MegaMUD remains bound to ordinary MajorMUD Telnet/game-stream behavior; Borin's authority rules now carry the same host-scope distinction.

## CodeUpdate0347 native TALK/RESPONSE/PAGE + MegaMUD/Borin promotion

- Before: **129 PASS / 71 STRONG / 0 UNKNOWN** (**64.5% strict**).
- After: **130 PASS / 70 STRONG / 0 UNKNOWN** (**65.0% strict**).
- Promoted: `commands.talk-fast-slow-mode`.
- Native TALK defaults to FAST and persists at record offset `0x45`; TALK SLOW now rejects otherwise-unrecognized lines instead of silently turning them into room speech.
- Native talking-response state persists in the communication flags word at record offset `0x3C`, bit `0x0100`; BRIEF returns `Well Spoken...`, VERBOSE preserves the normal SAY/YELL self response.
- PAGE remains live-session state; bare PAGE toggles and SET PAGE ON/OK/OFF now use the retained native text.
- MegaMUD receives the native parser distinction on the ordinary Telnet stream; Borin remains compatible because generated room dialogue uses explicit `SAY`.
- `native.communications.persistence-logging` remains STRONG for exact player-visible SET MESSAGES per-message priority/filter integration; Worldgroup host PAGE/telegram plumbing is out of Reforged parity scope.

## CodeUpdate0345 native communication + MegaMUD/Borin promotion

- Before: **126 PASS / 74 STRONG / 0 UNKNOWN**.
- After: **127 PASS / 73 STRONG / 0 UNKNOWN** (**63.5% strict**).
- Promoted: `native.communications.classic-range-rendering`.
- Retained WCCMMUD.DLL directly closes SAY/YELL, slash telepath, directed-message, GOSSIP and AUCTION visible rendering/branch behavior.
- Imported MegaMUD runtime evidence independently corroborates 23 telepath, 41 directed-to-you, and 308 gossip lines in the recovered native forms.
- Borin now emits/parses the same forms in the same pass; legacy autonomous TELEPATH/GOS output is translated at the client boundary.
- `native.communications.persistence-logging` remains STRONG for player-visible SET MESSAGES priority/filter integration; Worldgroup host PAGE/telegram plumbing is out of Reforged parity scope.
- CodeUpdate0345 native/static verification: **59/59 PASS**, 0 failures. The analysis container had no .NET SDK, so no new local source-build claim is made.
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

## CodeUpdate0346 suicide + native trap execution promotion

- Before: **127 PASS / 73 STRONG / 0 UNKNOWN** (**63.5% strict**).
- After: **129 PASS / 71 STRONG / 0 UNKNOWN** (**64.5% strict**).
- `traps.live-movement-order` and `traps.native-activation-state-schema` are promoted to PASS from original WCCMMUD.DLL movement/DISARM disassembly.
- Broad trap closure remains STRONG for exact SEARCH probability, Para3/Para4 presentation ownership, and reset/rearm timing.
- SET SUICIDE password gating is connected for both SUICIDE and REROLL; Borin blocks autonomous suicide credential operations.
## CodeUpdate0357 native stock Accuracy promotion

- Before: **142 PASS / 58 STRONG / 0 UNKNOWN** (**71.0% strict**).
- After: **143 PASS / 57 STRONG / 0 UNKNOWN** (**71.5% strict**).
- Promoted `combat.class-accuracy` from STRONG to PASS using direct `_move_player_to_fighter` disassembly.
- Replaced the old `combatCode*Level`/generic attack-value approximation with the native class/Level/STR/AGI/equipped-Accuracy/encumbrance builder.
- Normal/PvP combat no longer double-adds wielded weapon Accuracy after the native equipped-Accuracy fold.
- MegaMUD and Borin remain server-authoritative consumers; no private Accuracy protocol/formula is added.
- Non-default Speed source/integration remains STRONG.
