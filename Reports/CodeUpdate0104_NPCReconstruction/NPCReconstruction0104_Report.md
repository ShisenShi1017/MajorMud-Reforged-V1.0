# CodeUpdate 0104 — NPC Reconstruction Branch

Status: **PASS_WITH_LIMITATIONS**  
Canonical rule: **Librarium remains source of truth.** Runtime NPC behavior, AI, spawn, dialogue, and service logic were not replaced.

## Scope completed

Full NPC branch pass: inventory, relationship mapping, evidence reconciliation, AI/behavior review, spawn/respawn review, merchant/trainer/dialogue/quest/loot mapping, probe creation, runtime traceability review, promotion review, documentation, validation, and handoff.

## Evidence inventory

- Raw NPC-related evidence hits: **90448**
- Prioritized evidence hits: **1600**
- Source files with NPC hits: **1630**
- Relationship edges generated: **7769**

## Hits by source
- mmud_explorer: 5
- repo: 197
- repo_strings: 1398

## Hits by category
- ai_behavior: 1361
- combat_caps: 374
- dialogue_interaction: 78
- faction_alignment: 295
- loot_drops: 713
- merchant_trainer: 1519
- npc_entities: 1123
- quest_links: 219
- room_world_links: 1416
- spawn_respawn: 671

## Runtime review

Runtime/module discovery found **3** NPC-named source candidates. These are indexed for follow-up, but they do not by themselves prove Classic NPC behavior.

## Probe status

Eight NPC probes were defined. They become deterministic simulator/regression tests once generated runtime exposes authoritative NPC hooks.

## Promotion decision

- NPC mechanics promoted to Verified: **0**
- Runtime formulas/behaviors replaced: **0**

## Remaining NPC gaps
- **NPC-GAP-0104-ENTITY-FIELD-PROVENANCE** (critical): Complete provenance for NPC stats, flags, inventory, spells, location, services, and special behavior.
- **NPC-GAP-0104-AI-TARGETING-ORDER** (critical): Exact Classic target selection, aggro, assist, flee, target switch, and threat ordering.
- **NPC-GAP-0104-SPAWN-RESPAWN-TIMING** (critical): Exact spawn/respawn timers, zone reset rules, and host scheduler interaction.
- **NPC-GAP-0104-SPELL-SPECIAL-WEIGHTING** (high): NPC spell/special selection priority, cooldowns, resources, and combat timing order.
- **NPC-GAP-0104-MERCHANT-TRAINER-SERVICES** (high): Exact merchant/trainer flags, prices, inventory refresh, repairs, training gates.
- **NPC-GAP-0104-DIALOGUE-QUEST-BRANCHING** (high): Dialogue keyword ordering, quest checks, reward order, textblock branches, repeat/failure states.
- **NPC-GAP-0104-LOOT-DROP-PROBABILITY** (high): Loot table probability, gold formula, rare drops, corpse timing, equipped item behavior.
- **NPC-GAP-0104-MOVEMENT-PATHING-CADENCE** (medium): Wander/path cadence, tracking, blocked exits/doors, combat/aggro interaction.

## Next recommended branch

Continue to **World / Movement Reconstruction**.
