# Code Update 0245 — Borin Situational Awareness and Unified Mental State

## Purpose

This cumulative pass adds a model-independent integration layer between Borin's existing character systems and his direct llama.cpp/LLamaSharp inference path.

Earlier passes intentionally kept mood, social memory, episodic memory, time continuity, growth, initiative, conversation direction, and voice separate. That separation prevents one subsystem from becoming false proof for another, but it can also leave a local model deciding between several competing prompt sections. Code Update 0245 resolves that problem before inference by creating one compact, evidence-bounded description of what matters in the current turn.

## New source

`Apps/Source/Reforged.App/Brains/BorinSituationalAwareness.cs`

The new store produces a `BorinSituationalAwarenessSnapshot` for every private Forge conversation and every external MajorMUD player conversation. It combines only state already supplied by the earlier systems. It does not invent game state, actions, memories, feelings, or repository evidence.

## Unified priority order

For each conversational turn Borin now resolves competing context in this order:

1. The current speaker's direct meaning and emotional need.
2. Privacy, evidence, permission, and no-invention boundaries.
3. That speaker's isolated completed conversation history.
4. Relationship, mood, and durable character tendencies.
5. A relevant objective, episode, or proactive thought only when permitted.
6. Voice and dwarven style after meaning and accuracy are settled.

The per-speaker social topic is authoritative when an older broad living-character topic disagrees with it. A follow-up question is allowed only when both the conversation director and voice plan permit it. The current request always outranks Borin's desire to tell a story, pursue a personal objective, or keep the conversation going.

## Situational fields

The local GGUF prompt receives a compact synthesis containing:

- the current scene and isolated speaker;
- the primary conversational focus;
- the immediate response priority;
- Borin's social stance and emotional posture;
- the completed-turn continuity anchor;
- one relevant objective, when allowed;
- one optional grounded episode, when available;
- known and unknown current facts;
- the strict knowledge and permission boundary;
- the intended response posture;
- explicit resolution of subsystem disagreements.

The synthesis is marked as authoritative when lower-level character sections appear to compete. Borin is instructed never to expose the mental-state machinery or its priority list in spoken dialogue.

## Completed-turn persistence

The application may create:

`AI/Memory/borin-situational-awareness.json`

The file stores only compact per-speaker completed-turn continuity:

- last scene;
- last conversational focus;
- last response priority;
- last response posture;
- last continuity anchor;
- last user message and Borin reply for thread resumption.

It does not store invented world state. Empty, canceled, failed, and `[NO_REPLY]` turns are not committed.

## Privacy and evidence boundaries

External MajorMUD players receive only their own isolated situational state. They do not receive:

- William's private Forge conversation;
- private project rules;
- private objectives or reflections;
- exact host date, clock, or timezone;
- another player's memories, focus, or return history.

Conversation and memory remain evidence of what was said, not proof that a game, repository, build, login, deployment, or off-screen event occurred. Situational awareness grants no permission to edit files, run tools, issue game commands, or claim an action completed.

## Prompt efficiency

The conversational prompt budgets for the older independent sections were reduced while the unified synthesis was placed first. This gives the local model a clearer decision hierarchy without removing the detailed grounded state it may need. Direct LLamaSharp model loading, settings, sampling, model files, and the existing AI folder remain unchanged.

## Cumulative scope

This package includes all source and documentation from Code Updates 0237 through 0245. It does not include or replace the AI folder, GGUF model, llama.cpp/LLamaSharp binaries, credentials, configuration, existing memory JSON, or release binaries.
