# MegaMUD Connection to MajorMUD Reforged

## Connection boundary

MegaMUD connects as an unchanged legacy Telnet client to the normal Reforged server port. With the default local configuration:

- address: `127.0.0.1`
- Telnet/game port: `4000`
- Reforged private client-data port `4001` is **not** used by MegaMUD.

For a MegaMUD installation on another machine, change `TelnetAddr` in its BBS profile to the Reforged server's reachable host/IP and configure the server/firewall accordingly. Do not point MegaMUD at the private client-data port.

## Ready-made profiles

### Reforged Local — resume the most recent character

`Tools/MegaMUD/Reforged Local/BBS.INI` is the simplest profile. It uses MegaMUD's normal `{userid}` and `{pswd}` substitutions as the Reforged account login and sends `/go majormud` at the recovered `your selection` launch prompt.

The server then:

1. authenticates the Reforged account;
2. selects the configured default world;
3. resumes the most recently updated existing character in that world;
4. enters normal MajorMUD gameplay and emits the classic HP completion prompt.

### Reforged Multi-Character — deterministic character selection

`Tools/MegaMUD/Reforged Multi-Character/BBS.INI` follows the shared-account/character-slot pattern recovered from MegaMUD's own BBS help. It deliberately repurposes MegaMUD's **User ID field as the Reforged character selector** while the account name is stored as a static login reply in the profile.

Before using this profile, replace every `YOUR_REFORGED_ACCOUNT` entry with the Reforged account login. In MegaMUD:

- **User ID** = either the 1-based character slot (`1`, `2`, `3`, ...) or the exact Reforged character display name;
- **Password** = that Reforged account's password.

At the launch prompt the profile sends `/go majormud {userid}`. Reforged accepts either:

- `/go majormud 2` — select character slot 2; or
- `/go majormud Character Name` — select an exact character display name, case-insensitively.

An invalid slot or name is rejected instead of silently choosing a different character. Bare `/go majormud` remains supported and resumes the most recently updated character.

Character slots use the same order as Reforged's normal character-selection list for that account and world. If characters are added or removed, confirm the slot before relying on a numeric selector; an exact character name is the most stable selector.

## Existing-character requirement

MegaMUD direct login intentionally does not enter Reforged character creation. Create/claim the account, set its password, and create at least one character once through the Reforged client or a normal Telnet session before using automatic MegaMUD login.

## Telnet compatibility contract

The server keeps the legacy connection path separate from Reforged-only UI transport:

- CP437 application bytes;
- CRLF network lines;
- server-side Telnet echo;
- SUPPRESS-GO-AHEAD negotiation;
- MegaMUD may reject the optional GMCP offer and continue normally;
- a private Reforged client-data token is never issued to a client that does not opt into GMCP;
- default/full command completion is the ANSI-free `[HP=...]:` MajorMUD statline;
- no generic `> ` prompt is appended after gameplay output.

## Automated regression check

The server supports `--smoke-megamud-connection`. The smoke opens a real loopback TCP/Telnet pair and verifies legacy negotiation, GMCP rejection, normal command input, server echo, CP437 output, CRLF framing, launch-token parsing, and deterministic character-selector resolution. The repository's server GitHub Actions workflow runs this smoke after the Release build.

## Remaining certification boundary

The connection profile and server launch contract are implemented, but final certification still requires running a compiled Reforged server with the actual MegaMUD binary. `SCAN` remains evidence-gated because the recovered MegaMUD automation/path corpus does not depend on MajorMUD's SCAN command and its exact native behavior is still unresolved. Reforged does not invent SCAN behavior merely to clear a parity counter.

## REROLL / KEEP compatibility

REROLL is handled on the normal MegaMUD Telnet connection. It enters replacement-character creation and does not use the private Reforged client-data channel. `SET KEEP ON` transfers only the native retained-experience amount to the replacement character; `SET KEEP OFF` starts the replacement at zero experience. Reforged does not insert an extra Y/N confirmation, because native MajorMUD only adds a credential gate when a suicide password has been configured. `SET SUICIDE <password>` is now supported. When configured, both `SUICIDE` and `REROLL` receive the native `Enter your suicide password:` credential prompt; no Reforged-only Y/N confirmation is inserted.


## TALK / RESPONSE / PAGE compatibility (CodeUpdate0347)

- Classic `TALK FAST` preserves native fallback speech: otherwise-unrecognized non-command text is sent to the room.
- Classic `TALK SLOW` no longer relies on that fallback. Room speech must use an explicit `SAY` or the stock `.` speech prefix; otherwise-unrecognized input returns `Command not recognized.`
- `SET RESPONSE BRIEF` / `VERBOSE` follows the native player-visible self-response contract; BRIEF returns `Well Spoken...`.
- Bare `PAGE` toggles the live-session page flag; `SET PAGE ON`, `SET PAGE OK`, and `SET PAGE OFF` use the native visible state strings.
- These are ordinary Telnet/game-stream behaviors. No MegaMUD-only private channel or parser shim is introduced.
- Actual MegaMUD-binary certification remains a Windows/.NET runtime test boundary.


## Reforged host scope (CodeUpdate0348)

Reforged does not emulate Worldgroup PAGE/telegram host transport, mail storage, presentation queues, or host logging. Those are host implementation details, not MegaMUD protocol requirements. MegaMUD compatibility is judged on MajorMUD-visible Telnet/game-stream behavior; the existing PAGE setting text/state and communication commands remain part of that contract.

### SET MESSAGES filtering (CodeUpdate0349)

MegaMUD receives the ordinary MajorMUD terminal stream after the connected character's native `SET MESSAGES` threshold is applied. Reforged does not add a MegaMUD-only channel for this feature. The recovered native threshold byte values are High=0, Medium=10, and Low=20: LOW receives high-priority room events only; MEDIUM receives high+medium; HIGH receives high+medium+low. Pickup/drop and ordinary physical-combat observer chatter are LOW-priority, while spell cast/resist/effect observer chatter is MEDIUM-priority. Movement, room speech, engagement, and important room actions remain HIGH-priority.

## REST / MEDITATE compatibility (CodeUpdate0350)

- REST and MEDITATE remain ordinary MajorMUD commands on the Telnet stream.
- Active native sickness/poison state rejects them with the stock visible strings.
- REST is still accepted at full HP; there is no invented full-HP rejection.
- MEDITATE at full mana returns `Meditation will not help at this time.` and does not enter meditation.
- Successful entry returns `You are now resting.` or `You are now meditating.` and emits the stock room observer social.
- Imported MegaMUD executable string inventories independently recognize the recovery success/sickness/observer forms, so Reforged does not require a MegaMUD-specific parser shim.

## BREAK compatibility (CodeUpdate0351)

MegaMUD uses stock `break` on the ordinary MajorMUD command stream, including its documented "Break Combat Before Running" behavior. Reforged now emits the original DLL's active-manual-BREAK presentation: `*Combat Off*` to the breaking player and `<name> breaks off combat.` as a High-priority room observer line before combat teardown. No MegaMUD-specific protocol or private client-data signal is required. Exact opponent-side PvP autocombat ownership remains a separate evidence-gated server parity boundary.
