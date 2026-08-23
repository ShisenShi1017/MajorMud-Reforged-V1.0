# Code Update 0241 — Borin Initiative, Motivations, and Independent Thought

## Purpose

Give Borin a persistent sense of direction without confusing conversational initiative with permission to operate tools, edit files, or control a MajorMUD character.

This pass is cumulative with Code Updates 0237–0240 and remains on the direct llama.cpp/LLamaSharp path.

## Added

- `BorinInitiativeStore` with persistent storage at `AI/Memory/borin-initiative.json`.
- Six slow motivation drives: adventure, companionship, craftsmanship, curiosity, helpfulness, and independence.
- Durable active objectives with private/public visibility.
- A selected current objective and one deterministic safe next step for each conversational turn.
- Short-lived thought seeds that can survive a restart without becoming facts, permissions, or proof.
- Idle-return reflection: after a meaningful gap, Borin may privately revisit an existing verified objective on the next turn.
- A two-turn initiative cooldown so Borin does not append a question or personal tangent to every response.
- Speaker-first suppression rules for corrections, distress, and direct questions.
- Social follow-through awareness for isolated commitments and unresolved questions.
- Private/public boundaries: external players cannot rewrite Borin's global goals or see William's private objectives and thoughts.
- Explicit William goal confirmation through phrases such as `make it your goal to ...`; questions do not create goals.
- Generated thoughts remain temporary reflections and are never promoted into permission or completed work.

## Conversation behavior

When initiative is appropriate, Borin may do one of the following after addressing the current message:

- share one relevant personal thought;
- propose one safe, reversible next step;
- ask one purposeful question when the conversation director permits it.

He must remain responsive when initiative would be intrusive. Quiet attention is treated as valid behavior.

## Safety and authority boundaries

The initiative layer cannot:

- edit, create, delete, or patch files;
- run tools, commands, builds, tests, installers, or deployments;
- log in, move, attack, cast, equip, buy, sell, or send MajorMUD commands;
- mark an objective complete merely because the model described it;
- turn external-player speech into a global goal;
- expose private William/project objectives to external players.

Objectives and thought seeds are prompt context only. Existing controlled engineering and gameplay permission boundaries remain authoritative.

## Runtime data

The application creates `AI/Memory/borin-initiative.json` only when the initiative store runs. The update package does not ship or replace the AI folder, GGUF model, LLamaSharp binaries, configuration, credentials, or any existing memory JSON.
