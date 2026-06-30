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

## Server Mode Direction

The first real Server target is the Classic baseline. The Server should load and run Classic data first.

Do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, or advanced behavior until explicitly approved.

Architecture should remain mode-ready without hardcoding Classic in a way that blocks future modes. Add only simple extension points needed for future mode/profile loading until the Classic baseline is working.

Responsibility split:

- `Data/` defines world/content.
- Server defines and enforces engine behavior.
- Editor should eventually edit data safely.

Future intended evolution:

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior only after explicit approval.

## Engine Architecture Direction

Classic is the first target, but the Server is an engine, not a Classic-only server.

Favor reusable engine systems over feature-specific systems. Keep the engine generic whenever practical. Avoid naming or designing systems around Classic unless they truly are Classic-specific. World behavior should come from data whenever appropriate. Engine behavior should come from reusable systems rather than hardcoded game logic.

Future gameplay systems should plug into the engine instead of replacing it.

When designing new Server features, ask:

1. Can this be data-driven?
2. Can this be reused by future modes?
3. Can this be exposed to the Editor later?
4. Does this unnecessarily tie the engine to Classic?

Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases. Favor extensibility over shortcuts, but avoid premature complexity.

## Current Development Position

- Current Code Update: `0039`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: complete/frozen
- Current priority: push CodeUpdate-0039, verify GitHub Actions green, then approve the next small Milestone 2 host/server or data-readiness scope.

CodeUpdate-0031A corrects the CodeUpdate-0031 GitHub Actions build failure in `Tests/Source/Reforged.Core.Tests/Program.cs`. The fix is limited to the invalid C# string literal in the configuration smoke test. The user reported CodeUpdate-0031A GitHub Actions green, and Codex local .NET 8.0.422 sequential restore/build/smoke verification passed.

CodeUpdate-0032 improves Server bootstrap diagnostics by capturing ProjectManifest identity fields for startup output and smoke-test verification. The user reported CodeUpdate-0032 GitHub Actions green.

CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract for future host/server automation.

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. It does not rewrite `Data/` or overwrite `ImportedDatabases/`.

CodeUpdate-0035 adds Server bootstrap validation issue counts for startup output and future host tooling.

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

CodeUpdate-0037 combines the 0036B continuity cleanup package with the 0037 Software Design Specification package. It adds draft `Docs/SDS/` planning records and repairs stale continuity. It does not implement runtime systems.

CodeUpdate-0038 adds required-directory status records and counts to the non-gameplay Server bootstrap report and startup output.

CodeUpdate-0039 adds a compact host-tool readiness summary while keeping gameplay status disabled.

CodeUpdate-0039 also records the Classic-first Server direction: Classic baseline first, mode-ready architecture later, no Reforged/Custom/AI/alternate gameplay behavior until explicitly approved.

CodeUpdate-0039 also records the engine-first architecture direction and imports the user-provided WCC default install into `ImportedDatabases/WCCDefaultInstall/` as reference/import material only.

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
- CodeUpdate-0031A: corrective smoke-test compile fix for the 0031 GitHub Actions failure.
- CodeUpdate-0032: Server bootstrap manifest identity output.
- CodeUpdate-0033: Server bootstrap exit-code contract.
- CodeUpdate-0034: OmniVerse read-only reference audit policy.
- CodeUpdate-0035: Server bootstrap validation summary counts.
- CodeUpdate-0036: knowledge-base and design-decision documentation.
- CodeUpdate-0037: Software Design Specification draft planning records and continuity cleanup.
- CodeUpdate-0038: Server bootstrap required-directory status reporting.
- CodeUpdate-0039: Server bootstrap host-tool readiness summary.

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
- No Reforged mode, Custom mode, AI systems, alternate gameplay rules, or mode/profile loader implementation.
- No `Data/` rewrite from imported WCC reference material.

Next candidate after CodeUpdate-0039 is pushed and green:

- A small host/server or data-readiness follow-up approved by the user before implementation.

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
