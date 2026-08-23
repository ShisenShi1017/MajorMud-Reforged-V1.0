# Code Update 0256 — Borin Verified Perception, Server Identity, Assignment Window, and Autonomous Play

## Purpose

This cumulative repair prevents Borin from inventing a parallel version of MajorMUD when the live terminal evidence is missing or stale. It also separates the MajorMUD Reforged application from the game server on which Borin's character is actually playing.

## Verified perception

The canonical Client session now tracks, with independent timestamps:

- the latest MajorMUD HP prompt,
- the current room title,
- the exact visible exit list,
- the latest server activity,
- the latest terminal command and its owner.

Room exits are parsed from `Obvious exits:` and `Exits:` lines and normalized without inventing destinations. A room change clears the previous room's exits until a new exit line is observed. Partial HP prompts without CR/LF update the same live state used by the Forge and Borin's player controller.

## Server identity

Borin now distinguishes:

- **MajorMUD Reforged application** — the client and Forge workspace,
- **Omniverse BBS** — an external game server,
- **Reforged server** — the local or configured Reforged game server,
- **other configured BBS hosts** — external game servers.

A connected terminal session is authoritative for the active host identity. Opening or saving the Forge form cannot relabel a connected Omniverse session as Reforged, or a connected Reforged-server session as Omniverse.

## Deterministic live-state answers

Questions in the Forge about the current HP prompt, room, visible exits, connected server, incoming telepath, or last command owner are answered directly from the Client snapshot rather than left to model imagination. If the information is not verified, Borin says so. He never assigns an exit destination merely because a direction is visible.

## Command ownership

Every normal terminal command is attributed as one of:

- `Human` — William typed it,
- `Borin` — Borin's player controller sent it,
- `RouteAutomation` — a Reforged route sent it,
- `SystemLogin` — the login controller sent it.

Borin must not claim a Human command as his own action. Human terminal input immediately stops autonomous play.

## Assignment window

The broken hard-coded club-assignment control is removed from the Forge. A dedicated Assignment window provides:

- the exact current server environment,
- verified perception status,
- last parsed incoming telepath,
- assignment text,
- Save Assignment,
- Play by Himself,
- Stop Playing.

## Play by Himself safety gate

Autonomous play cannot start until the exact active session has:

- login state `Ready`,
- a fresh HP prompt,
- a fresh room title,
- a fresh exit list observed after that room title,
- health above the safety threshold,
- no active combat,
- no unresolved movement.

Movement is limited to directions in the current verified exit list. A visible exit does not establish its destination. The autonomous loop stops on stale perception, missing server response, repeated-command loops, rate limits, unsafe state, model uncertainty, or Human input.

## Regression coverage

The deterministic no-model harness now includes twenty scenarios, including:

- exact visible exit parsing,
- Omniverse versus Reforged-server identity,
- autonomous perception gating,
- Human/Borin command attribution,
- no-invented-exit-destination prompt rules.

## Preserved boundaries

This update does not include or replace the AI folder, GGUF model, Borin configuration, credentials, existing memories, knowledge corpus, runtime binaries, or telnet logs. Direct LLamaSharp/llama.cpp inference and the hard 270-character in-game reply limit remain intact.

## Scalar HP safety and post-command proof

- Omniverse prompts such as `[HP=51]:` are parsed as **51 current HP**, not as 51% and not as proof of maximum HP.
- When a previously verified maximum is available, the current percentage is recalculated. When maximum HP is unknown, Borin says so and autonomous play remains locked.
- After every autonomous command, a newer HP prompt must arrive. After movement, a newer room title and exit list must also arrive before Borin may choose another command. Ordinary server chatter is not enough to prove movement succeeded.
