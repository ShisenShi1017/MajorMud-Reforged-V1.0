# Reforged Agent Instructions

These instructions are mandatory for every AI assistant, Codex session, ChatGPT session, and developer handoff working on this repository.

## 1. Identify the work mode first

Before changing anything, identify which environment is being used.

### Codex mode

Use Codex mode only when the assistant is operating inside a Codex/local-repository coding environment.

Rules:

- Codex updates the local repository only.
- Codex does not create ZIP-based local handoff packages unless the user explicitly asks.
- Codex must wait after each update until the user pushes to GitHub and states that the current push has a green GitHub Actions result.
- Codex must not continue to the next Code Update until the current pushed update is green or the user explicitly overrides the gate.
- Codex must update continuity documents before ending the update.

### ChatGPT mode

Use ChatGPT mode when the assistant is operating in a regular ChatGPT chat window without direct local filesystem access.

Rules:

- ChatGPT cannot directly update the user's local repository.
- ChatGPT must work through local ZIP patch uploads unless the user explicitly chooses direct GitHub edits.
- ChatGPT update ZIPs must be patch-only packages, not rebuilt full repositories.
- Every ChatGPT update ZIP must contain exactly one named top-level folder.
- Every ChatGPT update ZIP must include an `Apply-CodeUpdate-XXXX.bat` installer and `UpdateManifest.json`.
- ChatGPT must verify the ZIP exists and has the required structure before announcing it.

## 2. Source of truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Rules:

- Read the current GitHub repository state before making any update plan.
- Do not use old ZIP files, generated patches, or prior conversation memory as truth when they conflict with GitHub.
- If a local ZIP is used in ChatGPT mode, treat it only as a user-provided local-application vehicle. GitHub remains the truth after the user pushes.
- Do not continue from an unverified assumption.

## 3. Never guess

Accuracy is required.

Rules:

- Never guess method names, file paths, project state, build state, package contents, current update number, or milestone status.
- Verify before claiming something exists, passed, failed, or was changed.
- If something cannot be verified in the current environment, state exactly what was verified and exactly what remains unverified.
- Do not invent missing code, files, reports, or build results.
- Do not claim GitHub Actions is green unless GitHub or the user explicitly confirms the current pushed commit is green.

## 4. Required startup order

Every new work session must read or verify:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. `DEVELOPMENT_PRINCIPLES.md` when architecture, runtime, or code quality decisions are involved
9. Relevant app/source/test files for the requested change
10. Current GitHub Actions status when build verification is part of the task

## 5. Update rules

Every Code Update must:

- Confirm the current Code Update number from repository files.
- Confirm the current milestone from repository files.
- Confirm the current Database Version from repository files.
- Make the smallest safe change that satisfies the approved scope.
- Avoid gameplay work unless the user explicitly approves it.
- Preserve `Saves/`.
- Preserve `ImportedDatabases/` unless the user explicitly approves otherwise.
- Update continuity documents.
- Record verification status and known limitations.

## 6. Documentation continuity

Every Code Update must update all relevant continuity documents, including at minimum:

- `AGENTS.md` when workflow rules or current project position change
- `AI_CONTEXT.md`
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- `ProjectManifest.json`
- `ReleaseNotes.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- Any affected app README or report

Do not leave stale milestone, update, roadmap, source-of-truth, or next-step text behind.

## 7. Build and verification rules

- GitHub Actions is the official build gate after a push.
- Local build/test results are useful but do not replace GitHub Actions.
- Do not say an update is complete until the required local/package verification has been done.
- Do not say a pushed update is accepted until the user or GitHub status confirms green Actions for that push.

## 8. Packaging rules for ChatGPT ZIP updates

Every ChatGPT-created Code Update ZIP must:

- Be patch-only.
- Contain exactly one top-level folder.
- Include `Apply-CodeUpdate-XXXX.bat` inside that folder.
- Include `UpdateManifest.json` inside that folder.
- Include changed/new files under a `Payload/` folder using repository-relative paths.
- Automatically locate the repository root.
- Back up replaced files before copying when practical.
- Never overwrite `Saves/` or `ImportedDatabases/`.
- Be physically created and inspected before being linked.

## 9. Engineering principles

`DEVELOPMENT_PRINCIPLES.md` defines the long-term engineering standards for Reforged. Read it when making architecture, runtime, or code quality decisions.

## 10. Current project position after CodeUpdate-0031A

- Current Code Update: `0031A`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: frozen
- Current priority: apply/push CodeUpdate-0031A, verify GitHub Actions green, then approve the next small Milestone 2 host/server scope.

CodeUpdate-0031A is a corrective build-fix patch for CodeUpdate-0031. It fixes invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`. It does not add gameplay features, networking, combat, character creation, or world simulation.
