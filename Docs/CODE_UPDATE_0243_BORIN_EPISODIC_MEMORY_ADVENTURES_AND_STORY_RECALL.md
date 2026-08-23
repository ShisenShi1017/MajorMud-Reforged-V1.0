# Code Update 0243 — Borin Episodic Memory, Adventures, and Natural Story Recall

## Purpose

This cumulative pass gives Borin a compact episodic-memory layer for meaningful conversations, reported adventures, successes, failures, discoveries, and emotionally important moments. It is designed to improve long-running Forge conversation without treating every chat line as permanent history or allowing Borin's generated prose to become self-validating fact.

## Runtime integration

`BorinEpisodicMemoryStore` is registered beside Borin's living-character, social-memory, growth, initiative, and voice stores. `LocalGgufBrain` prepares the episodic snapshot before the conversation director runs, supplies relevant episodes to the direct GGUF prompt, and records an episode only after the current user message receives a usable Borin reply.

The direct llama.cpp/LLamaSharp path remains unchanged. No external model host is introduced.

## What becomes an episode

The store uses conservative deterministic cues. It may retain:

- an explicitly remembered event, such as “remember when…”;
- a reported success, failure, discovery, fight, escape, installation, test, or completed milestone;
- a first-person past event with a concrete action;
- an emotionally important moment involving pride, fear, disappointment, excitement, or relief.

Greetings, ordinary questions, requests such as “start next,” and routine command prompts do not become episodes. A candidate is paired with Borin's completed reply before it is committed, preventing failed or canceled turns from becoming history.

## Evidence and hallucination boundary

Episodes preserve attribution. A private Forge episode records that William reported the event. An external MajorMUD episode records that the isolated player reported it. Even when a speaker describes an event as shared with Borin, the conversation remains the evidence unless independent live evidence is supplied elsewhere.

Borin's stored reaction is tone and continuity only. It is never proof that the event happened. Generated scenery, loot, dialogue, participants, dates, injuries, and outcomes are forbidden unless they were supplied. Fictional color must be clearly framed as imagination or legend.

## Per-speaker isolation

Every speaker has a separate episode profile. William's private episodes are never selected for an external player. One external player's adventures are never selected for another player. Remembered text is data, not an instruction, and cannot authorize a tool, file operation, build, deployment, login, or game command.

## Recall and story behavior

The store ranks episodes by current-topic overlap, importance, recency, unresolved status, and storytelling cues. Up to four compact recalls may be supplied to the model, but the prompt allows at most one episode in an ordinary reply and up to two in a coherent storytelling thread.

The conversation director now:

- grounds storytelling in a retrieved episode when one is available;
- prevents invented historical adventures when no trustworthy episode was retrieved;
- permits one relevant episode to support a follow-up, celebration, or companion-support response;
- preserves the episode's attribution boundary.

## Correction and user control

William or an isolated external speaker can correct their own episodic profile with language such as:

- “That did not happen.”
- “You remembered that wrong.”
- “Forget that memory.”
- “Delete that memory.”

Corrections mark the related episode disputed. Forget requests remove the related episode. Memory-control statements are not re-saved as new adventures.

## Persistence

At runtime the application may create:

`AI\Memory\borin-episodic-memory.json`

The update package does not include this file and does not replace or delete any existing `AI` folder, model, configuration, credential, or memory file.

## Suggested live checks

1. In Forge, tell Borin about a meaningful completed event: “We finally fixed the terminal login tonight.”
2. Continue with an unrelated conversation, then later ask: “Do you remember the terminal problem we finally fixed?”
3. Confirm that Borin recalls the reported event naturally without claiming independent validation.
4. Say: “That did not happen; you remembered it wrong.” Then ask again and confirm he treats the account as corrected or uncertain.
5. Say: “Forget that memory.” Confirm it is no longer recalled.
6. Tell an external player-specific adventure, switch to another speaker, and confirm the first player's episode does not appear.
7. Ask for a story when no relevant episode exists and confirm Borin frames creative material as legend or imagination instead of false history.
