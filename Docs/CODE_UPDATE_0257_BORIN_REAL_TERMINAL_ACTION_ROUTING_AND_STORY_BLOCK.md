# Code Update 0257 — Borin Real Terminal Action Routing and Story Block

## Purpose

Forge conversation must never role-play a MajorMUD command or invent a server result. When William gives Borin an explicit gameplay instruction, Reforged now converts a narrow, deterministic set of natural-language actions into real supervised terminal commands. The local GGUF model is not used to fabricate the command sequence or its results.

## Real action routing

Examples such as `go north and buy a club and equip it` compile to:

1. `NORTH`
2. `LIST`
3. `BUY CLUB`
4. `INVENTORY`
5. `EQUIP CLUB`
6. `EQUIPMENT`

When perception is stale, Reforged first sends `LOOK` and requires a newer HP prompt, room title, and exit list. Each later command waits for matching parsed server evidence. Movement is allowed only through an exit currently visible in the exact active session. Purchase and equipment sequences stop if the parsed inventory or equipment does not contain the requested item.

A direct sequence explicitly authorized by William may run when current HP is verified but maximum HP has not yet been parsed. That exception applies only to this supervised sequence; autonomous **Play by Himself** remains locked until both current and maximum HP are verified.

## No invented transcripts

Active-player model replies are rejected when they contain simulated command lines or imagined outcomes such as:

- `> go north`
- `You find yourself at ...`
- `You purchase ...`
- `Your character now wields ...`

The replacement reply states that no terminal command was sent. Hallucinated gameplay text is not saved as a successful action.

## Structured-decision repair

JSON boolean values such as `"question": true` are no longer converted into the visible message `True`. Player-decision text fields accept strings only.

## Room-title evidence repair

Wrapped room-description fragments such as `Surrounds the village, to protect against creatures of the night and random raids` are rejected as prose. They can no longer replace the actual verified room title such as `Newhaven, Village Entrance`.

The follow-up room-title repair no longer blanket-rejects names beginning with `A`, `An`, or `The`. Legitimate titles such as `The Southern Edge of Newhaven` and `A Hall of Heroes` are accepted, while article-led prose such as `The dusty path winds into darkness` remains rejected through descriptive-verb checks.

## Safety boundaries

- Reforged remains the application.
- Omniverse BBS, the Reforged server, and other BBS endpoints remain separate game-server identities.
- William's explicit Forge instructions use the supervised command gateway.
- Destructive, account, administrative, multi-command, and unapproved command families remain blocked.
- No command after a failed verification step is sent.
- Existing AI configuration, GGUF model, knowledge, credentials, and memory files are not packaged or replaced.
