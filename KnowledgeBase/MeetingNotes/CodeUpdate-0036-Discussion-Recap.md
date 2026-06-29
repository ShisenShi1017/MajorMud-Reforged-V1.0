# CodeUpdate-0036 Discussion Recap

## Topic

Preserving MajorMUD knowledge, reference sources, and future Reforged feature requirements after CodeUpdate-0035.

## Summary

The discussion expanded Reforged from a code-only reconstruction effort into a preservation, modernization, and platform project. The main repository should preserve not only code and data, but also the reasoning behind design decisions, reference-source confidence, reverse-engineering notes, gameplay mechanics, and future implementation requirements.

## Implementation Themes

### Preservation

- Recreate original MajorMUD gameplay first.
- Preserve original data behavior as a strict default preset.
- Treat original WCC/WSGSERVE data as highest-priority references when obtained.
- Treat ganghouses as the original housing-like system.

### Modernization

- Add discovery-based mapping that hides room/map visibility until discovered.
- Add an integrated in-game Explorer for items, shops, monsters, spells, quests, classes, races, trainers, and similar records.
- Only room/location visibility is discovery-gated by default.
- Add a launcher/start menu titled `MajorMud - Reforged` with IP/port login and saved server profiles.
- Support up to three simultaneous character sessions to support party play.

### Platform

- Allow hosts to run Classic Strict, Classic Editable, or Enhanced/Reforged rules.
- Allow host-configurable limited item behavior.
- Support optional AI-enhanced monster/NPC behavior and difficulty-aware loot in enhanced modes.
- Add host/admin permission management and custom command definitions with audit logging.
- Build future expansion support only after the original baseline is faithfully recreated and verified.

## Reference Materials Discussed

- LLTXPCal v1.02 XML race/class data.
- MegaMMUD v2.0.3 stock database and path files.
- MegaMUD `.mp` path files for navigation verification.
- Mud Hex class/item/monster/room/user/shop/spell layout documentation.
- User-provided 1.11p quest and enhancement flag notes.
- MudInfo maps and stock MajorMUD map resources.
- MudCentral as a historical resource candidate.
- r/MajorMUD as a community source candidate.
- WCC `default.rar` and WSGSERVE `.dat` files as desired high-priority future sources.

## Design Notes

- Data reconciliation should compare sources and produce auditable correction candidates.
- Gameplay flags should be data-driven definitions instead of hard-coded logic.
- Quests should define stage progression, flags, requirements, rewards, and references as data.
- MegaMUD paths should verify routes and travel mechanics, not define canonical room data.
- Mud Hex docs should validate importer field mapping and reverse-engineering assumptions.
- Limited item lockout should be configurable by host policy.
- AI systems must be optional and must not change strict classic behavior.

## Boundaries

This recap does not approve immediate gameplay implementation. It documents future design direction for later approved updates.
