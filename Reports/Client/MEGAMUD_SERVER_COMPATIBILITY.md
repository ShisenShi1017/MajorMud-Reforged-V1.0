# MegaMUD Server Compatibility / Borin Player Runtime

## Scope

The current cumulative integration promotes player-visible lifecycle and profile behaviors while preserving MegaMUD as an unchanged external Telnet client and Borin as a normal Reforged player/tester.

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
- REROLL does not add a Reforged-only Y/N prompt. Native MajorMUD only requires an extra credential when SET SUICIDE has configured a suicide password; that setting is not yet exposed by Reforged.

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
- SET SUICIDE / suicide-password handling and SUICIDE NOW equivalence are separate lifecycle work and are not invented by this pass.
- Live Windows/.NET + actual MegaMUD binary certification remains required before declaring 100% MegaMUD compatibility.
