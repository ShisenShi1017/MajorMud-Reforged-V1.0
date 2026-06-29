# Reforged Design Decision Index

This index records accepted or proposed architecture decisions that guide future implementation. Each decision should preserve what was decided, why it was decided, alternatives considered, risks, unknowns, and implementation notes.

## Current Decisions

| ID | Title | Status | Notes |
|---|---|---|---|
| DD-0001 | Canonical Data Reconciliation | Accepted | `Data/` remains runtime authority; references verify and reconcile. |
| DD-0002 | Reference Source Hierarchy | Accepted | Catalog WSGSERVE, WCC, LLT, MegaMUD, Mud Hex, OmniVerse, MudInfo, MudCentral, and community sources. |
| DD-0003 | Knowledge Base Strategy | Accepted | Preserve reasoning, research, design decisions, and meeting notes in repo docs. |
| DD-0004 | Discussion Documentation Process | Accepted | Every significant discussion produces recap, design decision, implementation notes, and repository docs. |
| DD-0005 | Gameplay Flag System | Proposed | Flags should be data-driven definitions with effects such as AC, mana, stealth, critical, backstab, dodge, meditate, and quest unlocks. |
| DD-0006 | Quest State Architecture | Proposed | Quests should define stages, requirements, NPCs, items, monsters, flags, rewards, repeatability, source references, and verification status. |
| DD-0007 | World Verification Assets | Accepted | MegaMUD paths, maps, room databases, and reference sources verify world topology rather than replacing `Data/`. |
| DD-0008 | MegaMUD Path Verification | Accepted | `.mp` files are navigation verification assets and can support automated route tests. |
| DD-0009 | Reverse Engineering Reference Policy | Accepted | Mud Hex and similar docs validate field mappings and importer decoding. |
| DD-0010 | Discovery-Based Player Map | Proposed | Rooms and map locations remain hidden until discovered; map improves navigation after discovery. |
| DD-0011 | Launcher and Multi-Character Sessions | Proposed | Start menu supports IP/port, saved profiles, and up to three simultaneous character sessions. |
| DD-0012 | In-Game Explorer | Proposed | Explorer supports lookups; only room/location visibility is discovery-gated. |
| DD-0013 | Editable Classic Mode | Proposed | Classic defaults reproduce original behavior, but hosts can override selected pain points. |
| DD-0014 | Host-Controlled Limited Item Policy | Proposed | Hosts can choose strict limited, unlimited, percentage, limited-percentage, disabled, and recovery rules. |
| DD-0015 | Optional AI-Enhanced Monsters, NPCs, and Loot | Proposed | Enhanced AI and difficulty-aware loot are optional; classic remains original by default. |
| DD-0016 | Ganghouse Foundation | Proposed | Original ganghouses are the baseline housing-like system; optional expansions should build on that model. |
| DD-0017 | Host/Admin Command Editor | Proposed | Hosts can grant permissions and define custom admin/sys commands with logging. |
| DD-0018 | Future Expansion Framework | Proposed | New expansions should be optional and should feel authentic to original MajorMUD. |

## Documentation Rule

Before implementation begins for a major feature, the related decision should be expanded from this index into a dedicated design decision document or implementation plan.
