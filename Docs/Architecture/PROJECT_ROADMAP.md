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
8. `DEVELOPMENT_PRINCIPLES.md`
9. Relevant source/test/application docs for the requested change

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

- Current Code Update: `0031`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: complete/frozen
- Current priority: apply/push CodeUpdate-0031, verify GitHub Actions green, then approve the next small Milestone 2 host/server scope.

CodeUpdate-0031 adds the first non-gameplay Server bootstrap entry point, a bootstrap report, ProjectManifest/folder checks, smoke coverage, and `DEVELOPMENT_PRINCIPLES.md`.

## Milestone 1 - Core Framework (Frozen)

Goal: Build the shared foundation that every application depends on.

Status: Complete/frozen after the user-reported green GitHub Actions freeze sequence.

Deliverables include Core configuration, logging, validation, serialization, repository interfaces, shared data models, event system, service registry, version management, manifest reading, repository health checks, shared utilities, smoke-test foundation, and application Core references.

## Milestone 2 - Engine Modernization / Host & World Editor

Goal: Modernize the engine host path and prepare safe host/world editor workflows while preserving the verified Milestone 1 Core baseline.

Status: Active.

Completed/started slices:

- CodeUpdate-0029: startup-only Server diagnostics.
- CodeUpdate-0030: governance/source-of-truth/handoff cleanup.
- CodeUpdate-0031: non-gameplay Server bootstrap foundation.

Focus Areas:

- Server improvements.
- Host/world editor planning and later implementation.
- Data validation tools.
- Runtime performance.
- Client/editor modernization.
- Gameplay systems only after explicit approval.

Boundaries until approved:

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No live editor implementation beyond approved scope.

Next candidate after CodeUpdate-0031 is pushed and green:

- A small host/server follow-up approved by the user after GitHub Actions verifies CodeUpdate-0031.

## Milestone 3 - Standalone Server

Goal: Build the standalone game server.

Features:

- Load `Data/`.
- World initialization.
- Room manager.
- Item manager.
- Monster manager.
- Spell manager.
- Shop manager.
- Player manager.
- Save system.
- Main game loop.
- Session manager.
- Network framework.
- Command dispatcher.
- Validation before startup.

Completion Criteria:

- Server boots successfully using only Reforged data.

## Later Milestones

- Milestone 4: Live Editor.
- Milestone 5: Launcher.
- Milestone 6: Windows Client.
- Milestone 7: Android Client.
- Milestone 8: Gameplay Systems.
- Milestone 9: Plugin Framework.
- Milestone 10: Version 1.0.

## Permanent Project Rules

- GitHub is the source of truth.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` remains archive/reference only.
- Every update inspects repository truth before making changes.
- Every update updates all relevant continuity documents.
- Every ChatGPT update ZIP includes a self-contained `Apply-*.bat` until the Launcher replaces it.
- Every update automatically locates the project root.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Never claim an update package exists unless it has actually been created and verified.
- Repository layout is frozen unless the user explicitly approves a structural change.
- Runtime must never require the .NET SDK.
- All approved suggestions must be implemented in the next applicable update unless explicitly deferred or rejected.
