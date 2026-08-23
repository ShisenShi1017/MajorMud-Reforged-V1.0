# Code Update 0237 — Borin Living Character Foundation

## Cumulative series

This is **Borin Living Character Cumulative Pass 0001**. Every later cumulative ZIP in this series must include these files and all later Borin living-character changes.

## Goal

Move Borin away from a repository assistant that merely wears a dwarf voice and toward a persistent adventurer who can maintain a natural relationship and conversation across local GGUF turns.

## What changed

- Added a model-independent living-character store with persistent:
  - emotional tendencies and a computed current mood;
  - individual player relationships, trust, familiarity, and recent exchanges;
  - active personal goals;
  - significant character moments;
  - current conversation topic and thread continuity.
- The state is stored locally at `AI/Memory/borin-living-character.json` and is created automatically at runtime.
- Direct GGUF requests load the current character state before inference. Only private or external player conversations can change that state; engineering, auditing, gameplay-decision, and reflection prompts cannot pollute Borin's relationship or conversation thread.
- Ordinary private conversation now uses a compact conversation-specific system prompt rather than the full repository-audit prompt.
- Ordinary conversation no longer performs repository bootstrap, index search, evidence gathering, or Classic tracing before every reply.
- Conversational sampling receives a small creativity increase and stronger repetition control while engineering and repository modes retain their existing deterministic settings.
- External MajorMUD conversations identify the current speaker, pass the existing conversation contract directly to the model, and retain a separate relationship profile without weakening the untrusted-chat contract.
- `Speaker:`, `Message:`, and trust metadata are parsed only for the external conversation path, so pasted logs in William’s private chat cannot overwrite his identity or relationship.

## Expected user-visible effects

- Faster ordinary replies because project scans are skipped when no project research was requested.
- Less repetitive `Aye, lad` phrasing and fewer unnecessary reports or capability disclaimers.
- Better continuity across follow-up messages, including very short replies such as “Really?”, “Trolls?”, or “What happened?” that depend on Borin's previous answer.
- Subtle changes in warmth, concern, confidence, curiosity, pride, and energy based on recent exchanges.
- Borin remembers that William is his trusted builder, mentor, and long-term companion.
- Borin can become familiar with individual MajorMUD players over repeated conversations.

## Files

- `Apps/Source/Reforged.App/Brains/BorinLivingCharacter.cs` — new persistent character-state service.
- `Apps/Source/Reforged.App/Brains/BrainContracts.cs` — carries living-character state in every brain context.
- `Apps/Source/Reforged.App/Brains/LocalGgufBrain.cs` — state integration, compact conversational prompt, and conversational sampling.
- `Apps/Source/Reforged.App/ViewModels/ForgeGuardianViewModel.cs` — bypasses repository retrieval for ordinary conversation.
- `Apps/Source/Reforged.App/App.xaml.cs` — creates and registers the living-character store.
- `Docs/CODE_UPDATE_0237_BORIN_LIVING_CHARACTER_FOUNDATION.md` — this record.

## AI folder requirement

The update ZIP does **not** include the `AI` folder, GGUF model, credentials, memories, or local configuration. The installed code will use the user's existing direct llama.cpp/LLamaSharp model and create the new JSON state file automatically inside the existing local `AI/Memory` folder.

## Validation boundary

The package performs payload-hash verification, source-contract validation, restore, Release build, clean staging publish, release verification, and final mirrored deployment. The installer always pauses and prints an unmistakable `SUCCESS` or `FAILED` result with the full log path.
