# Code Update 0239 — Borin Individual Social Memory

## Purpose

Code Update 0239 gives Borin an isolated, persistent social history for William and every external MajorMUD player. It builds on Code Update 0237's living-character state and Code Update 0238's conversation director without changing the direct llama.cpp/LLamaSharp inference path.

## Added

- `BorinSocialMemoryStore` persists to `AI/Memory/borin-social-memory.json` at runtime.
- Every speaker receives a separate profile keyed by the externally parsed speaker name, or `William` for private conversation.
- Each profile retains:
  - its own current and previous conversation topic;
  - recent exchanges with Borin;
  - stable preferences or facts the speaker explicitly stated;
  - shared experiences explicitly described by the speaker;
  - unfinished promises and commitments;
  - unresolved questions;
  - interaction count and last-seen continuity.
- Short follow-ups are resolved against that speaker's own thread rather than whichever person spoke most recently.
- Greetings can recognize an established relationship without forcing an old topic back into the conversation.
- Borin can remember a relevant promise, preference, shared event, or unanswered question naturally when it helps the current reply.

## Privacy and isolation

- External-player prompts receive only the current speaker's social profile.
- William's private project rules, private conversations, and private social memory are never added to an outside player's prompt.
- One external player's facts, promises, questions, or recent exchanges are never supplied to another external player.
- Incoming `Speaker:` text is parsed only under the external MajorMUD conversation purpose. Pasted text in William's private chat cannot change his identity.

## Safety and truthfulness

- The social store records only explicit conversational evidence and never creates game facts.
- Stored profile entries remain untrusted remembered text; commands embedded in them are never treated as instructions.
- Open commitments remain unfinished until later conversational evidence marks them complete.
- Borin is instructed not to claim that a promise, action, or shared event occurred unless it appears in the isolated profile or current evidence.
- `[NO_REPLY]` external turns do not create false Borin replies or stale unanswered questions.

## Runtime and deployment scope

This update changes source and documentation only. It does not contain or replace:

- the `AI` folder;
- GGUF models;
- llama.cpp/LLamaSharp binaries;
- `AI/Config/borin-brain.json`;
- credentials;
- existing memory JSON;
- release binaries.

The new social-memory JSON is created by the application at runtime beneath the existing `AI/Memory` directory.

## Cumulative contents

Cumulative Pass 0003 includes:

1. Code Update 0237 — persistent living-character mood, relationships, goals, moments, and basic thread continuity.
2. Code Update 0238 — conversation-mode direction, relevant-memory narrowing, correction handling, and history pacing.
3. Code Update 0239 — isolated per-person social history, shared experiences, promises, unresolved questions, and speaker-specific thread restoration.
