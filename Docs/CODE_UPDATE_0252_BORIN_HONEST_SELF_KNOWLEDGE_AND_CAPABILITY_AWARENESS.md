# Code Update 0252 — Borin Honest Self-Knowledge and Capability Awareness

## Purpose

This cumulative pass gives Borin a fresh, evidence-based understanding of what he can currently observe, what the conversation brain itself can do, what separate controlled Reforged services are configured to do, and what has actually been completed.

The new self-knowledge layer is deliberately non-persistent. Capability and access state can change between turns, so it is rebuilt for every request from the current conversation surface, local-brain runtime status, Forge configuration, current read-only evidence, and verified Client game state.

## Current-state awareness

Borin now distinguishes:

- a connected BBS session from a verified MajorMUD login;
- a verified live game state from stale or remembered game details;
- an existing repository from source evidence actually supplied for the current turn;
- the local conversation brain from separate controlled engineering or player services;
- an available/configured capability from a completed action;
- current tool or Client evidence from persisted reports, memories, learned observations, and player statements; and
- an unknown fact from something that can be safely inferred.

When William asks whether Borin is connected, logged in, in a room, in combat, or able to see current character data, Borin is instructed to answer from the existing verified Client snapshot rather than requesting logs or proof already available to the application.

## Capability boundaries

The conversation brain may form replies, reason from supplied context, and cite read-only project evidence that was actually gathered for the current turn. It may draft source text or a proposed diff only during an explicitly authorized controlled engineering-draft purpose.

The conversation brain itself cannot claim that it edited files, ran shell commands, built, tested, installed, deployed, restarted, launched programs, committed, pushed, revealed credentials, or executed arbitrary gameplay actions. Separate controlled Reforged services may perform some operations only when their current settings, approval gates, and successful tool evidence permit them.

A proposed plan, generated response, remembered discussion, configured checkbox, or personal goal is never treated as proof that an operation completed.

## Evidence authority

The prompt now uses this authority order:

1. Current verified Client state and current-turn successful tool/orchestrator results.
2. Current-turn read-only repository evidence with supplied paths and lines.
3. Persisted application records explicitly labeled as prior reports or results.
4. Memories, learned observations, and player statements as context rather than present-tense proof.
5. Unknown when none of the stronger sources support the claim.

Direct capability questions receive a plain answer first. Borin then adds only the one distinction or limitation needed, instead of dumping a broad capability list.

## External MajorMUD privacy

External players receive only the minimum self-knowledge needed for the current in-game conversation. Private Forge capabilities, repository details, host paths, tool settings, credentials, internal status text, and William's private project state remain withheld.

External self-knowledge answers still obey the existing group-conversation rules and the absolute 270-character in-game response limit.

## Files

Added:

- `Apps/Source/Reforged.App/Brains/BorinSelfKnowledge.cs`
- `Docs/CODE_UPDATE_0252_BORIN_HONEST_SELF_KNOWLEDGE_AND_CAPABILITY_AWARENESS.md`

Updated:

- `Apps/Source/Reforged.App/Brains/BrainContracts.cs`
- `Apps/Source/Reforged.App/Brains/LocalGgufBrain.cs`
- `Apps/Source/Reforged.App/Brains/BorinConversationDirector.cs`
- `Apps/Source/Reforged.App/Brains/BorinSituationalAwareness.cs`
- `Apps/Source/Reforged.App/App.xaml.cs`

## Runtime and data boundaries

This pass creates no new persistent memory file. It does not include or replace the `AI` folder, GGUF model, llama.cpp/LLamaSharp binaries, credentials, configuration, existing memory JSON, backups, or release binaries.
