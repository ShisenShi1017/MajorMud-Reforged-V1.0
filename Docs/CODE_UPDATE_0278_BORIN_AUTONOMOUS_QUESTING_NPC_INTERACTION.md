# Code Update 0278 — Borin Autonomous Questing & NPC Interaction

## Goal
Let Borin discover, evaluate, and pursue MajorMUD quests and NPC interactions by himself while preserving the project's evidence hierarchy. The Librarium may guide Borin toward decoded NPC/quest relationships, but the live server remains authoritative for NPC presence, dialogue, quest stage, inventory ownership, item transfers, rewards, and completion.

## Runtime behavior
- Adds `BorinQuestNpcPlanner`, a deterministic policy layer used by Borin's autonomous player loop and reflection journal.
- Adds `questNpcPlannerEnabled` to `AI/Config/borin-player.json` and enables it by default.
- Extends the structured Librarium player advisor to expose:
  - NPC spawn references associated with the verified current room.
  - decoded `npc_textblock_quest_flow` records linked to those NPCs.
  - decoded quest flag names and dialogue command keywords when present.
  - possible quest/key/gate clues for items Borin currently owns.
- Quest/NPC planning is fed into the longer-term progression planner so a useful quest or upgrade opportunity may outrank repetitive XP grinding.

## Autonomy rules
Borin is allowed to pursue quests without waiting for William, but the decision policy is deliberately evidence-gated:
1. Verify live room/prompt state first.
2. A Librarium spawn is a location reference, not proof that an NPC is currently present. Use live LOOK/EXAMINE/TALK evidence.
3. A decoded dialogue keyword is a candidate interaction step, not proof of Borin's current quest stage.
4. Execute one grounded dialogue/action step, read the server result, then decide the next step. Do not blindly replay an entire decoded flow or walkthrough.
5. Preserve possible quest/key/limited/upgrade items. Never GIVE, DROP, SELL, PUT, or USE one solely because a reference suggests it.
6. Never invent a quest giver, objective, route, reward, completion, NPC response, or successful hand-in.
7. Combat and survival outrank routine quest dialogue.

## Librarium authority
The Librarium remains read-only. PROVEN records may guide game-world facts; STRONGLY CORRELATED records are cautious planning evidence; other/reference records are clues. None of them override fresh live terminal evidence.

## Tests added/extended
- The deterministic Librarium guidance fixture now includes a catalog-listed quest flow linked to a current-room NPC and an owned possible quest item.
- The fixture verifies decoded NPC quest keywords reach Borin's guidance.
- The fixture verifies possible quest items are surfaced with a preserve/investigate boundary.
- A dedicated quest/NPC planner test checks live NPC verification, one-step dialogue progression, and protection against evidence-free item hand-ins.

## Verification note
This build environment does not include the .NET SDK, so compilation and the Borin character test harness must still be run on the Windows development machine after applying the cumulative update.
