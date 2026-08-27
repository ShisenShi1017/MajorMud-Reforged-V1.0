# Code Update 0289 — Commands / Communication / Party Coordination

## Scope

Pass 19 continues MajorMUD 1.11p parity without promoting guessed native behavior. It closes the Reforged command-prompt fallback, narrows communication and party behavior to recovered WCCMMHLP/support evidence, corrects the transportation model to immediate paid room transfers, and records unresolved native mechanics as evidence gates.

Worldgroup-only behavior is not counted as MajorMUD parity. Classic remains the baseline for all worlds; Custom and AI may add extension behavior without changing Classic claims.

## Global command prompt rule

The dispatcher now applies the project-wide rule to Classic, Custom, and AI:

1. Preserve the complete original prompt input.
2. Resolve registered commands and valid abbreviations.
3. Resolve explicit native speech-prefix actions and evidence-backed data-driven actions.
4. Execute only a uniquely valid resolution.
5. Otherwise send the entire original prompt as SAY text.

Ambiguous abbreviations therefore fall through to SAY instead of producing a generic unknown/ambiguity response.

Native speech prefixes are resolved before fallback:

- `.` — room talk/SAY behavior.
- `"` — YELL.
- `/Username <TEXT>` — one-recipient telepath.

No native `TELL` command is claimed. `WHISPER` retains native identity evidence but its syntax/range/rendering remain UNKNOWN.

## Communication recovery

Recovered WCCMMHLP/support evidence establishes:

- SAY is same-room communication.
- YELL reaches the current room and adjacent rooms.
- GOSSIP is a same-world global channel filtered by the recipient's gossip setting.
- AUCTION uses the same global-channel model for auction/trade communication.
- FORGET toggles blocking telepaths from a named sender.
- `SET GOSSIP ON|OFF` and `SET AUCTION ON|OFF` are native.
- `SET FOLLOW BLIND|NORMAL` is native; BLIND suppresses room descriptions while moving with the party and NORMAL restores them.
- `SET MESSAGES LOW|MEDIUM|HIGH` and `SET ENTRANCES ON|OFF` are native identities/settings, but their complete defaults/persistence/filter tables remain unresolved.
- `SET TELEPATH ON|OFF` exists, but its exact polarity is not promoted because retained wording is not sufficiently unambiguous.

Exact native communication prose, color/formatting, WHISPER behavior, channel defaults, and related filtering details remain evidence-gated.

## Command table and HELP corrections

WCCMMHLP directly proves these explicit aliases:

- `BR` = BROADCAST.
- `BS` = BACKSTAB.
- `BG` = BROADGANG.

The previous BRIEF ownership of `BR` was removed. Commands without exact recovered minimum-prefix evidence are registered at their full-name length rather than receiving invented abbreviations.

Classic HELP now fails closed against the retained native help catalog. If a native topic cannot be uniquely resolved, Classic reports that native topic resolution is not yet recovered instead of substituting Reforged synthetic help. Custom/AI may still use synthetic extension help after native resolution fails.

This is important for titles such as FOLLOW, which occur in more than one retained native help record. Native fuzzy/prefix precedence remains UNKNOWN rather than guessed.

## Party corrections

Recovered native help establishes the following party behavior:

- INVITE invites a player to the adventuring group.
- UNINVITE removes/kicks a player from the group.
- `JOIN <username>` accepts an invitation and immediately begins following the leader.
- `FOLLOW <username>` also accepts/uses the invitation and begins the same party-follow behavior.
- `TRACK <username>` is a separate tracking-skill command and is not the party-follow command.
- LEAVE stops an ordinary follower from following/leaves the party.
- A follower moves where the leader moves.
- FRONTRANK and BACKRANK are native party-position commands.
- Backrank may not outnumber Frontrank.
- `SET FOLLOW BLIND|NORMAL` controls whether room descriptions are shown during party-follow movement.
- Explicit follower exit from the game stops following; unexpected socket loss/linkdead behavior is not inferred from that statement.

The Classic party shell no longer invents:

- a two-minute invite timeout;
- a six-member party cap;
- automatic leader succession;
- a rule that the leader must remain Frontrank;
- unexpected disconnect/linkdead removal or retention policy;
- equal-split party XP or loot behavior.

Multi-member leader LEAVE, leader rank changes, exact initial/default rank assignment, the exact Frontrank capacity formula beyond the proven Front>=Back invariant, linkdead behavior, party XP/loot sharing, and party spell/teleport/resurrection coordination remain UNKNOWN unless previously owned by another evidence-backed subsystem.

The generic multiplayer completion/smoke model is explicitly non-authoritative for Classic parity. Its compatibility assumptions do not earn parity credit and are not used as the native party specification.

## SHARE

Native WCCMMHLP message 227 establishes:

`SHARE <amount> <currency> [WITH <ROOM/PARTY>]`

Implemented core behavior:

- PARTY is the default scope.
- `WITH ROOM` and `WITH PARTY` are accepted selectors.
- The sender is excluded from the recipient count.
- Hidden recipients are excluded.
- Recipients whose RECEIVE setting blocks the transfer are excluded.
- Whole-unit integer division is used among eligible recipients.
- Any remainder stays with the sender.
- The named denomination is preserved; SHARE does not silently convert currency denominations.
- Distribution is committed through an atomic multi-character character transaction so a failed recipient write does not intentionally leave a partial distribution.

Exact native validation order and user-facing SHARE messages remain unresolved. A retained-help conflict also remains around the polarity wording for SET RECEIVE; Pass 19 does not use that conflict to reverse the Pass 18 runtime interpretation without stronger native proof.

## Broadcast groups

Native help proves `JOIN <n>` broadcast-group selection and LEAVE returning a non-party user to group 0. It also proves BROADCAST/BR and BROADGANG/BG identities. However, retained help conflicts on the maximum group number (one record says 126, another says 127), and complete broadcast-group state/global command-character ownership is not recovered. The runtime therefore keeps these details evidence-gated.

## Transportation correction

The prior transport-service concept was too broad. MajorMUD parity now models transportation as an immediate paid room transfer:

`source room + recognized command + payment validation + immediate destination move`

The native parity model explicitly excludes:

- vehicle state;
- boarding state;
- departure schedules;
- travel timers;
- arrival waits;
- transport-specific persistence;
- transport-specific disconnect state.

Still UNKNOWN: exact source room, recognized command, destination, cost/currency, validation/payment order, insufficient-funds text, and success/movement text for each native transfer.

## Parity result

Pass 18 baseline:

- PASS 71
- STRONG 68
- UNKNOWN 45
- Strict parity 38.6%
- Evidence-backed parity 75.5%

Pass 19:

- PASS 71
- STRONG 69
- UNKNOWN 44
- Strict parity 38.6%
- Evidence-backed parity 76.1%

`native.communications.classic-range-rendering` moves from UNKNOWN to STRONG because retained native help/support evidence establishes the core channel/range semantics. It is not promoted to PASS because exact native rendering and remaining communication details are still unresolved.

## Validation

The dedicated Pass 19 static validator passes 151/151 checks. Economy native-markup and both combat regression validators also pass.

Historical Pass 17/18 validators report only their old hard-coded active parity-ledger pointer checks (`0287` / `0288`) because Pass 19 correctly advances the active ledger to `0289`; those pointer-only results are not runtime regressions and the historical validators are intentionally not rewritten.

A .NET compilation was not performed in this environment because `dotnet`, `csc`, and `mcs` are unavailable.
