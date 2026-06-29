# CodeUpdate-0036 - Knowledge Base and Design Decisions

## Purpose

CodeUpdate-0036 records the major design, research, and reference-source decisions discussed after CodeUpdate-0035. This is a documentation-only update. It does not add runtime gameplay systems, combat, character creation, world simulation, networking expansion, or editor implementation.

## Scope

This update establishes the Reforged Knowledge Base direction and records future implementation requirements for data reconciliation, reference sources, gameplay flags, quests, discovery maps, Explorer lookup, launcher/session design, host customization, AI-enhanced systems, limited-item policy, ganghouses, and host/admin commands.

## Key Decisions Recorded

- Reforged `Data/` remains the runtime canonical database.
- External sources are reference and verification material, not blind import targets.
- Original WSGSERVE `.dat`, WCC data, LLT XML, MegaMUD databases, MegaMUD `.mp` paths, Mud Hex documentation, OmniVerse, MudInfo, MudCentral, and other verified sources should be cataloged with confidence and intended use.
- Major design discussions should produce discussion recaps, design decisions, implementation notes, and repository documentation.
- Gameplay flags and quests should become data-driven systems rather than scattered hard-coded logic.
- MegaMUD `.mp` path files should be world-verification assets, not runtime world data.
- Discovery-based mapping should hide only rooms/map locations until discovered.
- The integrated Explorer should allow lookup of items, shops, monsters, spells, quests, classes, races, trainers, and other reference records, while room/location visibility remains discovery-gated.
- The launcher/start menu should be titled `MajorMud - Reforged`, support IP/port login and saved server profiles, and support up to three simultaneous character sessions for party play.
- Classic gameplay should have a strict default preset but be host-editable where desired.
- Limited-item behavior should be configurable by the host, including strict limited, unlimited, limited percentage, percentage drop, disabled, and recovery policies.
- AI-enhanced monsters, NPCs, and loot should be optional Reforged/enhanced systems, not required for classic mode.
- Ganghouses are the original housing-like mechanic and should be faithfully recreated before any optional housing expansions.
- Host/admin tools should support permission-controlled system commands and custom command definitions with audit logging.

## Reference Materials Discussed

- LLTXPCal v1.02 XML class/race database files.
- MegaMMUD v2.0.3 stock database and `.mp` path files.
- Mud Hex HTML field-layout documentation for classes, items, monsters, rooms, users, shops, and spells.
- User-provided MajorMUD 1.11p enhancement/quest flag notes.
- MudInfo maps and historical resources.
- MudCentral and r/MajorMUD as research candidates pending direct verification.
- Original WCC `default.rar` and WSGSERVE `.dat` files as high-priority future reference sources when obtained.

## Boundaries

- No source implementation code is changed by this update.
- No `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are implemented.
- No combat, character creation, world simulation loop, networking expansion, live editor implementation, or AI runtime is added.
- No raw third-party ZIPs, executables, RAR files, or binary reference assets are committed by this update.

## Next-Scope Candidate

After CodeUpdate-0036 is pushed and GitHub Actions is green by user report, CodeUpdate-0037 should be approved by the user before implementation. A safe candidate is a planning or foundation slice for gameplay flags, quest data modeling, or host/admin permissions, still without broad gameplay implementation unless explicitly approved.
