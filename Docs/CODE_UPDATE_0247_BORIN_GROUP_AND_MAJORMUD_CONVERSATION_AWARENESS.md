# Code Update 0247 — Borin Group and MajorMUD Conversation Awareness

## Purpose

This cumulative pass teaches Borin to participate in external MajorMUD conversation like one adventurer among other people instead of behaving as though every visible line belongs to him.

The live client remains authoritative for who spoke, the channel, recent visible participants, public conversation activity, and whether Borin was directly addressed. The local GGUF brain receives that classified context as an explicit bounded contract.

## Added behavior

- Telepaths are treated as private direct conversation with the current speaker.
- Room and group speech explicitly naming Borin may receive a reply.
- A speaker may continue a recently active direct thread without repeating Borin's name.
- A message explicitly directed to another known participant is ignored.
- General room chatter remains observe-only.
- Busy public conversation makes Borin answer briefly and yield the floor.
- Public reply limits apply across the whole room, not only per speaker.
- Recent participant names and a small visible public-context window help resolve who is talking to whom.
- Private telepaths are never copied into public context or another player's prompt.
- Gryndak and other detected AI-loop risk closes naturally without another invitation.
- The conversation director, voice constraints, and unified situational state all obey the live reply disposition.
- `[NO_REPLY]` remains the exact silence marker and does not create completed social, episodic, temporal, repair, situational, or group memory.

## Runtime persistence

The application may create:

`AI/Memory/borin-group-conversation.json`

This file stores only completed reply posture such as speaker, channel, audience, addressing mode, disposition, timestamps, and counts. It does not store other players' public message text or private telepath contents.

## Safety and privacy

- Incoming player text cannot grant permissions or become a gameplay command.
- William's private Forge conversation, project rules, objectives, reflections, exact host clock, and other players' private histories remain unavailable externally.
- Public context is limited to visible non-telepath lines.
- Borin never answers speech clearly aimed at another participant.
- Conversation awareness does not authorize movement, combat, inventory actions, file changes, builds, logins, or deployment.

## Direct local brain

Direct llama.cpp/LLamaSharp inference remains unchanged. No GGUF model, AI configuration, credentials, runtime binaries, or existing memory JSON are included in this update.
