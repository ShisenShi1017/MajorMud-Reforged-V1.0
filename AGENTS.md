# Reforged Agent Instructions

These instructions are mandatory for every Codex, ChatGPT, AI assistant, and developer handoff working on this repository.

## Current Position

- Mode: Codex local repository work.
- Repository: `ShisenShi1017/MajorMud-Reforged-V1.0`.
- Local path: `E:\MajorMud-Reforged Github`.
- Current Code Update: `0138`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0138 - Server Runtime and Host Enablement`.
- Current status: closed locally, awaiting commit/push and green GitHub Actions confirmation.
- Latest local verification: PASS 55/55 at `Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md`.

## Active Documentation

Use the small active doc surface only:

- `AGENTS.md`
- `AI_CONTEXT.md`
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- `ReleaseNotes.md`
- `ProjectManifest.json`
- `UpdateManifest.json`
- `MANIFEST.md`
- `HANDOFF.md`
- `PATCH_INSTRUCTIONS.md`
- `LibrariumManifest.json`
- `Docs/PROJECT_STATUS.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- `Docs/Architecture/HOST_VISION_V1.md`
- `DEVELOPMENT_PRINCIPLES.md`

Do not recreate old per-update handoff/changelog/manifest clutter unless the user explicitly asks.

## Source of Truth

- GitHub is the authoritative pushed source of truth.
- Codex normally edits the local repository only; if the user explicitly asks Codex to push, push only after local verification and commit succeed.
- Do not claim GitHub Actions is green unless GitHub or the user confirms it for the current pushed commit.
- Local ZIPs and prior chat handoffs are input material only; they do not override the repo.

## Verification Rules

- Never guess method names, paths, update numbers, build state, package contents, or milestone state.
- Verify before claiming something exists, passed, failed, or changed.
- If verification cannot be run, say exactly what was verified and what remains unverified.
- Local build/test results are useful, but GitHub Actions is the pushed gate after the commit is pushed.

## Required Startup Check

Before code or architecture work, read or verify:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/PROJECT_STATUS.md`
4. `Docs/Architecture/PROJECT_ROADMAP.md`
5. `README.md`
6. `VERSION.md`
7. `CHANGELOG.md`
8. `ProjectManifest.json`
9. `DEVELOPMENT_PRINCIPLES.md` when architecture, runtime, or code-quality decisions are involved
10. Relevant source/test files for the requested change

## Architecture Rules

- Librarium is the only gameplay authority.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Server/Host is authoritative for world state, character state, combat, inventory, NPCs, quests, economy, timers, resets, and permissions.
- `ImportedDatabases/` must remain untouched unless the user explicitly approves a reviewed cleanup.
- Preserve real `Saves/`; do not commit smoke-save artifacts.

## CodeUpdate Rules

Every Code Update must:

- Confirm current Code Update, Database Version, and milestone from repo files.
- Make the smallest safe change for the approved scope.
- Update the active continuity docs listed above.
- Record verification status and known limitations.
- Wait for green GitHub Actions before moving to the next public update, unless the user explicitly overrides the gate.

## CodeUpdate0138 State

Verified locally in this working copy:

- Server runtime and host enablement surfaces.
- Host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audits.
- Runtime live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world systems.
- Generated Classic data refresh from Librarium.
- Map-qualified movement promotion from MMExplorer evidence through Librarium.
- Textblock quest-flow fix 0142 promoted into Librarium quests.
- Complete runtime verification gate passes 55/55.

Next update begins only after CodeUpdate0138 is pushed and GitHub Actions is green.
