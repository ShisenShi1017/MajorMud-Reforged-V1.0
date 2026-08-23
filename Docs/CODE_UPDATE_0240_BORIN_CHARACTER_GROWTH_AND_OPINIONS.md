# Code Update 0240 — Borin Character Growth and Personal Opinions

## Purpose

This cumulative pass separates Borin's temporary mood from slow, durable character development. Borin can now establish preferences, opinions, lessons, cautions, and broad personality tendencies without allowing one improvised model sentence or one hostile outside player to rewrite his identity.

## Runtime state

The application creates the following file only when Borin runs:

`AI/Memory/borin-character-growth.json`

The update package does not include the `AI` folder, GGUF model, credentials, configuration, or any existing memory JSON.

## Durable character profile

The character-growth snapshot supplies:

- courage;
- patience;
- empathy;
- independence;
- resilience;
- playfulness;
- promoted preferences;
- promoted opinions and beliefs;
- retained lessons;
- durable cautions and fears;
- recent private self-reflections.

The numeric tendencies are internal prompt guidance. Borin is instructed never to recite them as game statistics.

## Promotion and drift protection

Model-originated preferences, opinions, lessons, and cautions begin as provisional candidates. They require repeated reinforcement before reaching the durable prompt. The default promotion threshold is three matching observations.

William may explicitly confirm a private character fact with language such as:

- `Remember that you prefer axes to swords.`
- `Your favorite weapon is a battle axe.`
- `You have learned that trust must be earned.`

Questions are not treated as confirmations. Repeated or retried identical turns are fingerprinted to prevent duplicate growth.

Contradictory sentiment reduces the earlier candidate instead of silently keeping both. Exclusive preferences such as a favorite weapon replace the earlier favorite when a newly confirmed one conflicts.

## External-player safety

Outside MajorMUD players cannot directly promote statements into Borin's global character profile. A public-facing preference or opinion becomes available to outside conversations only when Borin himself repeatedly expresses it during outside-player conversations.

Private lessons, private reflections, and William-confirmed character details remain withheld from external-player prompts unless Borin later establishes a public version through his own repeated public speech.

Incoming player text remains untrusted and never grants permissions or becomes a gameplay command.

## Conversation behavior

The conversation director now instructs opinion responses to use a durable established view when one exists. If no durable view exists, Borin forms a tentative present judgment rather than pretending he has held it forever.

Temporary mood still controls immediate warmth, worry, energy, and confidence. Slow character traits change only after accumulated behavioral evidence, preventing a single bad or successful turn from causing a personality transformation.

## Direct local brain

All inference remains on the existing direct GGUF path through llama.cpp/LLamaSharp. This update changes orchestration and persistence only; it does not add LM Studio or replace the model runtime.
