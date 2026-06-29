# Reforged Roadmap

Last Updated: 2026-06-29

## Current Milestone

Milestone 1 - Core Framework Completion is frozen after the user reported CodeUpdate-0026B GitHub Actions green.

CodeUpdate-0024B is a corrective build-fix patch for the CoreStartupDiagnostics logger API mismatch. It does not add gameplay features.

CodeUpdate-0025 completes the active rebrand audit. No gameplay features are added.

CodeUpdate-0026 prepares the Milestone 1 finalization candidate.

CodeUpdate-0026B cleans the baseline by removing generated source-project build outputs and repairing documentation encoding.

CodeUpdate-0027 records the green CodeUpdate-0026B GitHub Actions gate and freezes the Milestone 1 baseline.

CodeUpdate-0028 records the green CodeUpdate-0027 GitHub Actions gate and defines the first Milestone 2 scope plan.

CodeUpdate-0029 implements the first approved Milestone 2 slice: host/server startup and runtime diagnostics foundation.

CodeUpdate-0030 records permanent GitHub/source-of-truth, handoff, and Codex-vs-ChatGPT workflow rules.

CodeUpdate-0031 adds the first non-gameplay Server bootstrap entry point and smoke coverage.

CodeUpdate-0031A corrects the CodeUpdate-0031 smoke-test compile failure and is green by user report.

CodeUpdate-0032 improves Server bootstrap diagnostics by capturing ProjectManifest identity fields for startup output and future handoff clarity.

CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract for host/server automation.

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. It does not rewrite Reforged data.


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

Milestone 2 should focus on engine modernization, host/world editor planning, server improvements, data validation tools, runtime performance, gameplay systems, and client/editor modernization. Begin implementation only after the user approves the Milestone 2 starting scope.

First implementation slice: CodeUpdate-0029 wires Server startup through existing Core configuration, logging, repository health, event bus, and startup diagnostics without adding gameplay systems.

Current implementation slice: CodeUpdate-0034 documents OmniVerse reference-audit policy. Next work should wait for user push plus green GitHub Actions, then continue with one small approved scope.
