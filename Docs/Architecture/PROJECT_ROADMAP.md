# Reforged Development Roadmap and Project Constitution

## READ THIS FIRST

Every AI assistant, Codex session, ChatGPT session, or developer joining this project must read this document before changing anything.

Required startup order:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. Relevant source/test/application docs for the requested change

## Source of Truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Do not use old ZIPs, prior patches, or conversation memory as truth when they conflict with GitHub. In ChatGPT mode, ZIPs are local-application vehicles only. In Codex mode, local work must be pushed and verified green in GitHub Actions before the next update continues.

## Never Guess Rule

Never guess when creating, fixing, packaging, reviewing, or reporting updates.

- Verify current code before changing it.
- Verify file paths before referencing them.
- Verify package contents before linking them.
- Verify build/test/GitHub Actions status before claiming success.
- If verification is not possible in the current environment, state exactly what was and was not verified.

## Work Modes

### Codex mode

- Codex updates local repository files only.
- Codex must stop after each Code Update until the user pushes to GitHub and states the current push has green GitHub Actions.
- Codex must not move to the next update until that green gate is confirmed or the user explicitly overrides the gate.

### ChatGPT mode

- ChatGPT cannot directly update the user's local repository.
- ChatGPT uses patch-only ZIP updates for local application unless the user explicitly requests direct GitHub edits.
- Every ChatGPT ZIP must contain exactly one top-level folder, an apply BAT, and an update manifest.
- The ZIP must be verified before delivery.

## Project Vision

Reforged is not a runtime-dependent copy of the original legacy MUD. It is a clean, standalone game engine and toolset inspired by legacy MUD. Original legacy MUD data may be imported, decoded, compared, and audited, but after import the project must operate from its own simple, editable `Data/` files.

The long-term goal is a standalone server, native importer, live editor, launcher, Windows client, Android client, gameplay systems, and plugin framework that do not require the original legacy MUD runtime.

## Current Development Position

- Current Code Update: `0030`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: complete/frozen
- Current priority: apply/push CodeUpdate-0030, verify GitHub Actions green, then approve the next small Milestone 2 host/server scope.

CodeUpdate-0030 is documentation/governance only. It adds `AGENTS.md`, records the source-of-truth rules, records the Codex-vs-ChatGPT workflow split, and adds the permanent no-guess creation rule.

## Milestone 1 - Core Framework (Frozen)

Goal: Build the shared foundation that every application depends on.

Status: Complete/frozen after the user-reported green GitHub Actions freeze sequence.

Deliverables:

- Core configuration system
- Core logging system
- Validation framework
- Serialization framework
- Repository interfaces
- Shared data models
- Event system
- Dependency injection/service registry
- Version management
- Project manifest reader
- Repository health checker
- Shared utility library
- Unit-test framework
- Application Core references

Completion Criteria:

- No duplicated models across applications.
- Every application references Core.
- Core has no dependencies on application projects.
- Runtime does not require the .NET SDK.
- Handoff documents accurately track status after every update.

## Milestone 2 - Engine Modernization / Host & World Editor

Goal: Modernize the engine host path and prepare safe host/world editor workflows while preserving the verified Milestone 1 Core baseline.

Status: Active.

CodeUpdate-0029 implemented the first constrained slice: startup-only Server diagnostics. CodeUpdate-0030 cleans governance and handoff rules before further implementation.

Focus Areas:

- Server improvements
- Host/world editor planning and later implementation
- Data validation tools
- Runtime performance
- Client/editor modernization
- Gameplay systems only after explicit approval

Boundaries until approved:

- No gameplay systems
- No combat
- No character creation
- No world simulation loop
- No networking expansion
- No live editor implementation beyond approved scope

Next candidate after CodeUpdate-0030 is pushed and green:

- A small host/server follow-up approved by the user after GitHub Actions verifies CodeUpdate-0030.

## Milestone 3 - Standalone Server

Goal: Build the standalone game server.

Features:

- Load `Data/`
- World initialization
- Room manager
- Item manager
- Monster manager
- Spell manager
- Shop manager
- Player manager
- Save system
- Main game loop
- Session manager
- Network framework
- Command dispatcher
- Validation before startup

Completion Criteria:

- Server boots successfully using only Reforged data.

## Milestone 4 - Live Editor

Goal: Replace static editing with live server editing.

Features:

- Connect to running server
- Live edits
- Validation
- Undo history
- Database browser
- Search
- Diff viewer
- Relationship editor
- Room editor
- Monster editor
- Item editor
- Spell editor
- Shop editor

Completion Criteria:

- Server updates safely without direct file editing.

## Milestone 5 - Launcher

Goal: Replace BAT files and PowerShell for routine use.

Features:

- Start server
- Start editor
- Start client
- Apply Code Updates
- Apply Database Versions
- Backup manager
- Restore manager
- Validation
- Repository health
- Update manager
- Log viewer

Completion Criteria:

- No routine use of BAT files beyond compatibility.

## Milestone 6 - Windows Client

Goal: Build a modern companion-client-inspired Windows client.

Features:

- Telnet/network engine
- Map window
- Inventory
- Equipment
- Spell windows
- Trigger system
- Macro system
- Logging
- Themes
- Plugins

## Milestone 7 - Android Client

Goal: Mobile companion.

Features:

- Touch interface
- Saved layouts
- Map
- Macros
- Inventory
- Notifications

## Milestone 8 - Gameplay Systems

Goal: Complete the server gameplay layer after host/server foundations are approved and stable.

Systems:

- Combat
- AI
- Skills
- Magic
- Shops
- Banking
- Gangs
- Lairs
- Quests
- Events
- Scripting
- Respawns

## Milestone 9 - Plugin Framework

Goal: Allow extensions without modifying Core.

Features:

- Plugin API
- Events
- Commands
- Data hooks
- Server extensions
- Editor extensions

## Milestone 10 - Version 1.0

Goal: Production-ready release.

Final Deliverables:

- Standalone server
- Native importer
- Live editor
- Launcher
- Windows client
- Android client
- Complete documentation
- Automated validation
- Stable update system

## Permanent Project Rules

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the source of truth.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` remains archive/reference only.
- Every Code Update and Database Version must inspect the repository before making changes.
- Every update must update `AGENTS.md` when workflow rules change and update all relevant continuity documents.
- Every ChatGPT ZIP update must include a self-contained `Apply-*.bat` until the Launcher replaces it.
- Every ChatGPT ZIP update must automatically locate the project root.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Never claim an update package exists unless it has actually been created and verified.
- Repository layout is frozen unless the user explicitly approves a structural change.
- Runtime must never require the .NET SDK.
- All approved suggestions must be implemented in the next applicable update unless explicitly deferred or rejected.
