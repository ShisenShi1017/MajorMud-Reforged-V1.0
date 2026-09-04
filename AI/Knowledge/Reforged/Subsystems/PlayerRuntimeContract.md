# Player Runtime Contract

Borin participates in MajorMUD as a normal player. These rules describe player-visible runtime behavior he may rely on. They are not permission to bypass the game runtime.

## Transport and prompts

- The normal player terminal is the legacy MajorMUD Telnet stream and uses CP437-compatible bytes.
- RealmGate UI-only Librarium, map, boss, stats, inventory, equipment, spell, party, PvP, and room-panel refresh traffic travels on the private out-of-band client-data channel. Borin must not issue `clientdata`, `roomdata`, or other UI polling merely to obtain an advantage as a player.
- The default/full command-completion prompt is ANSI-free and follows the MajorMUD HP form such as `[HP=328/MA=270]:` or `[HP=571/KAI=64]:`.
- MegaMUD is a separate unchanged legacy client and connects only to the normal Telnet/game port. It does not use the RealmGate private client-data port.
- The recovered MegaMUD BBS auto-login launch reply `/go majormud` is accepted at the world-selection boundary. With no selector, RealmGate enters the configured default world and resumes the most recently updated existing character for that account/world. RealmGate also accepts `/go majormud <slot-or-exact-character-name>` as a compatibility extension for deterministic multi-character MegaMUD profiles. Borin is not to use this launch shortcut during headless play; it exists to preserve the external MegaMUD connection contract.

## Player information commands

- `EXITS` is an executable room-presentation command. It renders the same authoritative visible-exit set and exit qualifiers used by the normal room display.
- `PROFILE` is executable. Native help message 37 proves it displays the current profile settings; RealmGate renders the current live/persisted player settings while keeping exact native formatting/default bits evidence-gated.
- `SCAN` is now native-evidence backed: WCCMMUD.DLL parses it as command `0x3A` and calls `display_scan_of_users(0)`. It displays the current adventurer/user scan; it is not room SEARCH and has no scan-range/pathing semantics. Borin should treat it as a read-only player-state query.
- `HEALTH` is an executable player command. It reports current/max Health plus current/max Mana, or Kai for a Kai/Mystic class. It is not an unrecovered-command placeholder.
- Borin may use `HEALTH` as a normal survival/state check.

## Equipment readiness

- `READY <item>` is an executable equipment command and uses the same authoritative equipment engine as the generic equip path. Borin must supply an actual carried item; bare `READY` is syntax-only and should not be used as a training action.

## Timing

- The classic combat-round authority is 5000 milliseconds. Borin may use the observed five-second round as a scheduling fact, but must still react to actual runtime messages/state rather than assuming a command succeeded by elapsed time alone.

## Spells and Kai powers

- `CAST` is the spellcasting path for non-Kai magic users. A Kai class is rejected from `CAST` by the native class-mode gate.
- `INVOKE <power> [target]` is the Kai/Mystic power path. Non-Kai classes are rejected from `INVOKE`.
- `POWERS` can legitimately answer `You have no powers.` That sentence is status output, not a power name. Never construct a command such as `invoke You have no powers`.
- Likewise, an empty `SPELLS`/`POWERS` listing means Borin should query later or choose another action rather than inventing a spell/power.

## World interactions

- Recovered room actions, puzzle commands, doors, item-owned spell actions, and special route behavior are server-owned runtime behavior. Borin should interact through ordinary player commands and learn from their results; he must not mutate room state directly.
- MegaMUD compatibility fallbacks never override an authoritative decoded room action.

## Destructive character lifecycle and evil-action safeguard

- `REROLL` is a destructive character-replacement lifecycle. It creates a fresh character in the same realm and discards the old character's inventory, equipment, money/bank state, spells, abilities, quests/flags, alignment, stats, appearance, lives, and other character-specific state. Old combat, party/gang membership, companion ownership, and persisted live power/effect/cooldown state are also purged. Borin must never issue `REROLL` autonomously during training.
- `SET KEEP ON` controls the only state allowed to cross REROLL: retained experience. The native retained-XP calculator is connected to the replacement character. `SET KEEP OFF` gives the replacement character 0 experience.
- `SET WARNING ON` is an active safeguard at proven positive-Evil-Point action boundaries. If an action is known to award Evil Points, WARNING ON blocks it before execution; WARNING OFF permits it. Borin may turn WARNING OFF only when an explicitly intended player action requires committing that evil deed.
- `SET INTERRUPT` remains accepted with its native meaning, but exact editable Telnet input-buffer interruption remains separately gated.

## Evidence boundary

- When a recognized command still reports that its exact runtime behavior is not recovered, treat it as a parity defect to report, not a behavior to memorize as normal gameplay.
- Historical parity-gap findings for commands promoted by the current player contract (`HEALTH`, `INVOKE`, `EXITS`, `READY`, `PROFILE`) are retired from Borin's active defect queue. Lifetime counts remain recorded separately; fixed historical findings must not dominate current training reports.


## Pass 09 lifecycle promotion

- `SET WARNING ON` uses the native safeguard meaning and is connected wherever RealmGate can prove an action would add positive Evil Points. OFF removes that safeguard.
- `SET INTERRUPT ON` native text: PvP combat interrupts typing/output; OFF displays PvP combat as normal messages. RealmGate persists the option and uses native response text; terminal input-buffer interruption remains gated until the host owns an authoritative editable input buffer.
- `SET KEEP ON` now feeds the executable REROLL replacement lifecycle. The replacement receives the native retained-XP result; OFF gives the replacement 0 XP. Nothing else from the old character is copied.
