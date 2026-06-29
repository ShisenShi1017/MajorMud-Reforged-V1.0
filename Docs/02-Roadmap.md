# Reforged Roadmap

Last Updated: 2026-06-28

## Current Milestone Closure Track

The project is in Milestone 1 - Core Framework Completion. Remaining work is verification-only: dependency injection setup, configuration loading, repository/data loading health, event system behavior, startup diagnostics, smoke tests, GitHub Actions build validation, continuity document updates, and final Milestone 1 baseline freeze after verification passes.

CodeUpdate-0024B is a corrective build-fix patch for the CoreStartupDiagnostics logger API mismatch. It does not add gameplay features.

CodeUpdate-0025 completes the active rebrand audit. No gameplay features are added.

CodeUpdate-0026 prepares the Milestone 1 finalization candidate. Milestone 1 should be frozen only after CodeUpdate-0026 GitHub Actions is green.


## Development Phases

### Phase 1: Standalone Server

Primary goal: stable standalone Reforged server.

Planned focus areas:

- Server startup/shutdown stability.
- Character creation and login.
- Save/load reliability.
- Room movement.
- Commands.
- Combat.
- Monsters.
- Items.
- Shops.
- Spells and skills.
- Admin tools.
- Imported database support.

### Phase 2: Host/World Editor

Primary goal: safe editor for game content.

Planned focus areas:

- Data validation.
- Room editor.
- Monster editor.
- Item editor.
- Race/class editor.
- Spell/skill editor.
- Message editor.
- Import/export tooling.
- Documentation of database fields.

### Phase 3: Client Work

Primary goal: modern client inspired by companion client 2.0.

Planned focus areas:

- PC client improvements.
- Android client exploration.
- Mapping.
- Triggers.
- Aliases.
- Macros.
- Profiles.
- Scripting.
- UI themes.

## Roadmap Rule

The roadmap is a planning document, not an implementation claim. Items listed here are not complete unless recorded in release notes or patch history.

## Milestone 2 Preview

After Milestone 1 is frozen, the next phase should focus on server improvements, host/world editor planning, data validation tools, runtime performance, gameplay systems, and client/editor modernization. Begin implementation only after the user approves the Milestone 2 starting scope.
