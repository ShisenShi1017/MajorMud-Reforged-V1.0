# Code Update 0242 — Borin Natural Voice, Dialogue Rhythm, and Self-Expression

## Purpose

Pass 0006 adds a model-independent voice director around Borin's existing direct GGUF brain. The goal is not to replace his personality or alter the GGUF model. It gives the existing llama.cpp/LLamaSharp conversation path a durable way to vary openings, pacing, emotional expression, humor, and dwarven flavor without losing accuracy or permission boundaries.

## Added system

`BorinVoiceStore` persists compact dialogue-pattern metadata in:

`AI\Memory\borin-voice.json`

The file is created only when MajorMUD Reforged runs. It is not included in the update package and existing memory files are not replaced.

The store records only compact style patterns such as:

- recent opening words for the current speaker;
- recent response shapes and paragraph rhythm;
- whether a question, exclamation, or signature phrase was recently used;
- cooldown state for repeated catchphrases;
- the most recent turn-specific voice plan.

Generated dialogue is not promoted to factual memory by this system.

## Turn-specific voice modes

The conversation director's intent now selects an appropriate voice plan:

- plainspoken repair for corrections;
- warm steady companionship for distress;
- bright shared pride for celebration;
- reflective first-person answers for personal check-ins;
- brief familiar greetings;
- fireside storytelling with varied sentence length;
- clear personal opinions;
- decisive but bounded planning;
- tight continuity for short follow-ups;
- clear seasoned explanations.

Each mode controls preferred cadence, opening behavior, emotional expression, humor, dwarven-language budget, closing behavior, sentence range, paragraph range, and sampling boundaries.

## Repetition control

Borin now remembers recent opening patterns separately for each speaker and conversation boundary. The local prompt receives those patterns as cooldown material, preventing one player's phrasing history from being exposed to another player.

Signature phrases such as `Aye, lad`, `By Moradin`, and similar expressions have a four-turn cooldown. A recent signature phrase can temporarily reduce the dwarven-expression budget to zero so Borin speaks plainly instead of repeating a branded catchphrase.

## Natural self-expression

The voice plan encourages Borin to:

- speak in first person with ordinary contractions;
- express concern, pride, curiosity, amusement, disappointment, and preference through normal wording;
- use brief reactions and natural transitions;
- stop when the thought is complete;
- avoid generic capability offers and forced follow-up questions;
- avoid narrated gestures, asterisk actions, and hidden-state explanations.

It continues to prohibit invented live sensory input, unsupported events, fabricated memories, and claims that actions were completed without authoritative evidence.

## Dynamic local sampling

The direct LLamaSharp inference path now uses the voice plan to make small bounded sampling adjustments:

- corrections and factual explanations favor lower variation;
- greetings, check-ins, celebrations, and stories allow more variation;
- repetition penalties rise slightly when a conversational voice plan is active.

The configured model, llama.cpp/LLamaSharp runtime, GGUF weights, context size, GPU offload, and stored brain configuration are not replaced.

## Integration

Pass 0006 updates:

- `App.xaml.cs` to construct and register `IBorinVoiceStore`;
- `BrainContracts.cs` to carry `BorinVoiceSnapshot` in each conversation context;
- `LocalGgufBrain.cs` to prepare the voice plan after conversation direction, include it in the conversational system prompt, apply bounded sampling values, and record compact reply patterns after successful turns;
- `BorinVoice.cs` with the new persistent voice planner and store.

## Safety and privacy boundaries

- Voice history is selected only for the current speaker and the same private/external conversation boundary.
- External player speech cannot change Borin's permissions or prove an action occurred.
- The voice system does not edit files, send game commands, build, deploy, or authorize tools.
- The update contains no `AI` folder, GGUF model, credentials, configuration, binaries, release files, or runtime memory JSON.
