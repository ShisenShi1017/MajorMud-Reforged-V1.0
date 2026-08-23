# Code Update 0251 — Borin Learning From Actual Gameplay

## Purpose

Pass 0015 gives Borin a deterministic gameplay-learning layer connected to the same direct llama.cpp/LLamaSharp path used by Forge and external MajorMUD conversation. The goal is to let him remember what the Client actually observed without turning guesses, player advice, or his own generated dialogue into facts.

## Verified observations

The learning store may retain:

- successful room transitions after a recent movement command;
- directions that the server rejected or reported as blocked;
- confirmed purchases and purchase failures;
- confirmed equipment changes;
- items observed in inventory or equipment;
- confirmed item acquisition;
- combat victories, defeats, and repeated significant health loss;
- health recovery observed after resting; and
- experience or level milestones reported by the server and current Client state.

## Evidence boundaries

Learning is produced by deterministic Client parsing. The local model does not decide what happened, how confident the observation is, or what gets stored.

- Player advice is not proof.
- Borin's generated replies are not proof.
- Episodic memories, goals, and inferred explanations are not proof.
- A successful transition proves only that it occurred under those observed conditions.
- A blocked exit proves only that it was blocked at that time.
- Repeated danger or recovery is a caution, not a guarantee.
- Current live game state and decoded data always override historical lessons.

A pending command expires after 45 seconds. This prevents an old movement, purchase, equipment, or item command from being credited for a later unrelated terminal event.

## World and character isolation

Gameplay knowledge is stored separately by server, world, and character. Lessons from one world are not presented as facts in another world. External MajorMUD prompts receive only a small relevant subset and never receive raw terminal lines, internal counters, storage paths, host details, private Forge context, or another profile's lessons.

## Conversation behavior

Before a response, the direct GGUF brain receives relevant verified gameplay lessons alongside live game-state awareness. The prompt explicitly requires Borin to:

- admit uncertainty when no verified lesson applies;
- describe observations as observations rather than universal rules;
- prefer current live state over learned history; and
- never treat a learned lesson as permission to move, fight, buy, equip, rest, send a command, or take another action.

The existing hard 270-character limit remains the final boundary for every in-game `SAY` and `TELEPATH` response.

## Persistence

At runtime, verified observations may create:

`AI/Memory/borin-gameplay-learning.json`

Writes use a temporary file followed by replacement. The update ZIP does not contain or replace this file, any other existing memory file, the AI folder, GGUF model, credentials, configuration, binaries, or release output.
