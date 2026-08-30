# MegaMUD Server Compatibility / Borin Player Runtime

## Scope

The current cumulative integration promotes player-visible lifecycle and profile behaviors while preserving MegaMUD as an unchanged external Telnet client and Borin as a normal Reforged player/tester.

## CodeUpdate0345 communication compatibility

The server communication stream now follows recovered Classic parser-visible forms instead of Reforged-only channel wrappers:

- room SAY: `Name says "text"`;
- current-room YELL: `From Name (Yelling): "text"`;
- adjacent YELL: `Someone yells from above/below/the <direction> "text"`;
- telepath: `Name telepaths: text`;
- directed speech: `Name says (to you) "text"`;
- world channels: `Name gossips: text` and `Name auctions: text`.

The imported MegaMUD runtime log independently contains the telepath, directed-message, and gossip forms, so these lines are now regression invariants in `RuntimeMegaMudConnectionSmokeReport`. `GOS` and `AUC` are accepted as legacy channel aliases. The private Reforged client-data channel remains separate and is still disabled when a legacy client rejects GMCP.

Borin was updated in the same pass: he emits native slash telepaths, uses full GOSSIP/AUCTION commands, translates legacy autonomous TELEPATH/GOS at the client boundary, and parses the exact native/MegaMUD lines above.

## REROLL + SET KEEP

Native HELP REROLL establishes that REROLL kills the current character and enters replacement-character creation. Native final-death evidence establishes the retained-experience calculation and its gates. The user clarified the gameplay contract: the old character is discarded; a newly created character replaces it; only the KEEP-controlled retained experience may cross that boundary.

Reforged now implements that lifecycle:

- REROLL is owned by the interactive Telnet/session lifecycle, not the generic command dispatcher.
- The replacement is a fresh level-1 character built by the normal character-creation authority.
- The account character slot is replaced atomically. A new character name/identity may replace the old identity.
- The old active character save and backup are removed after the replacement commits.
- Inventory, equipment, item/bank state, carried wealth, spells, skills/abilities beyond fresh creation grants, quests/flags/conditions, alignment/Evil Points, appearance, stats, lives, and other old-character state are not copied.
- Active party membership is removed; if the departed character was the party leader, the transient party is dissolved to prevent an orphaned old-character owner.
- Active gang membership is removed; a gang led by the departing character is disbanded rather than leaving an invalid leader identity.
- Active combat ownership is broken, companion ownership is cleaned up, stale runtime character tracking is removed, and persisted/live spell, ability, resource, effect, and cooldown state keyed to the departed character is purged.
- SET KEEP ON causes the fresh replacement to receive the already-recovered native retained-XP result. SET KEEP OFF produces zero retained XP.
- REROLL does not add a Reforged-only Y/N prompt. Native MajorMUD only requires an extra credential when SET SUICIDE has configured a suicide password; Reforged now exposes that setting and uses the same prompt for SUICIDE and REROLL.

The native retained-XP calculation remains:

`max(floor(old total XP / 10), required XP for floor(old level / 2) on the old race/class chart + 1)`

when global KEEPEXP is enabled, the player's KEEP option is enabled, and the old level is greater than 1; otherwise the retained amount is zero.

## SET WARNING

WARNING now has an executable consumer at authoritative positive-Evil-Point boundaries:

- WARNING ON blocks a player action before it executes when the runtime can prove the action would add positive Evil Points.
- WARNING OFF permits the deed.
- The live PvP attack path checks WARNING before registering the PvP attack, so a blocked action creates no combat side effect.
- Decoded NPC/quest dialogue mutations that are proven to add Evil Points are also blocked before character/world commit when WARNING is on.
- Practice-arena attacks are not blocked by WARNING because that path explicitly awards no Evil Points.
- Unresolved crime classes are not guessed. Future proven Evil-Point action owners should use `RuntimeClassicWarningAuthority` rather than creating independent WARNING logic.

## MegaMUD connection impact

- MegaMUD remains on normal Telnet/game port 4000 only.
- The private Reforged client-data channel remains separate.
- REROLL is processed on the same interactive Telnet connection and enters replacement-character creation without a non-native confirmation prompt.
- All previous CP437, status-prompt, `/go majormud`, selector, path, room-action, and startup-SET compatibility work remains cumulative.

## Borin impact

Borin's player contract is promoted to `majorMud-player-2026-pass09`.

Borin now knows:

- REROLL is destructive and must never be issued autonomously during training.
- KEEP controls only the retained experience transferred to the replacement character.
- WARNING ON blocks proven positive-Evil-Point deeds; WARNING OFF permits an intentionally chosen evil deed.
- Nothing else from the rerolled character may be assumed to survive.

## Regression coverage

- `RuntimeClassicDeathExperienceSmokeReport` verifies the native retention formula and the connected final-reroll consumer flag.
- `RuntimeRerollWarningSmokeReport` verifies WARNING ON/OFF gating, zero-EP non-blocking behavior, account-slot replacement, deletion of the old character save, replacement persistence, non-copying of old character state, and purging of old-character live power persistence.
- `RuntimeClassicParityClosureSmokeReport` now includes the reroll/WARNING regression report.

## Remaining boundary

- SET INTERRUPT still requires an authoritative editable input-buffer interruption consumer.
- SET SUICIDE / suicide-password handling is now connected to the Telnet lifecycle: the configured credential gates SUICIDE and REROLL, and `SUICIDE NOW` follows the native immediate-reroll lifecycle form. Tournament/no-suicide-room admission flags remain a separate native projection boundary.
- Live Windows/.NET + actual MegaMUD binary certification remains required before declaring 100% MegaMUD compatibility.


## CodeUpdate0347 TALK / RESPONSE / PAGE compatibility

- Classic `TALK FAST` preserves native fallback speech: otherwise-unrecognized non-command text is sent to the room.
- Classic `TALK SLOW` no longer relies on that fallback. Room speech must use an explicit `SAY` or the stock `.` speech prefix; otherwise-unrecognized input returns `Command not recognized.`
- `SET RESPONSE BRIEF` / `VERBOSE` follows the native player-visible self-response contract; BRIEF returns `Well Spoken...`.
- Bare `PAGE` toggles the live-session page flag; `SET PAGE ON`, `SET PAGE OK`, and `SET PAGE OFF` use the native visible state strings.
- These are ordinary Telnet/game-stream behaviors. No MegaMUD-only private channel or parser shim is introduced.
- Actual MegaMUD-binary certification remains a Windows/.NET runtime test boundary.


## Reforged host scope (CodeUpdate0348)

Reforged does not emulate Worldgroup PAGE/telegram host transport, mail storage, presentation queues, or host logging. Those are host implementation details, not MegaMUD protocol requirements. MegaMUD compatibility is judged on MajorMUD-visible Telnet/game-stream behavior; the existing PAGE setting text/state and communication commands remain part of that contract.

### SET MESSAGES filtering (CodeUpdate0349)

Legacy MegaMUD compatibility uses the same recipient-side MajorMUD room-message threshold as the stock server. No private protocol is required: LOW receives only high-priority room text, MEDIUM receives high+medium, and HIGH receives all room-message classes. Native threshold bytes are 20/10/0 respectively. Reforged's MegaMUD smoke locks this contract so a compatibility client does not receive a separate or bypassed observer stream.

## CodeUpdate0350 recovery-state compatibility

The original WCCMMUD command branch and imported MegaMUD executable inventories now jointly close the REST/MEDITATE terminal surface. Reforged emits the stock success, sickness, full-mana, and observer text on the normal Telnet stream. REST remains legal at full HP; MEDITATE rejects at current mana >= maximum. No private Reforged client-data dependency is introduced.

The server MegaMUD smoke now locks the exact recovery strings. Final end-to-end certification with the actual MegaMUD binary remains a compiled Windows runtime boundary, not a protocol-design gap.

## CodeUpdate0351 BREAK compatibility

Imported MegaMUD evidence contains `*Combat Off*`, ` breaks off combat`, a stock-only note that the client sends `break`, and help stating that MegaMUD can issue `break` before running from PvP/monsters. Reforged now preserves the native active-BREAK player/observer terminal presentation and High-priority room delivery on the same ordinary Telnet stream. No MegaMUD-only shim was added.
