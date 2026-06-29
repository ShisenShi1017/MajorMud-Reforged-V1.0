# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0031
- Current Database Version: 0009
- Milestone: Milestone 2 - Engine Modernization / Host & World Editor Planning
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0031 - Server Bootstrap Foundation
- Milestone 1 Core Framework is complete/frozen.
- CodeUpdate-0029 added startup-only Server diagnostics as the first constrained Milestone 2 implementation slice.
- CodeUpdate-0030 was reported green by the user and is now the governance baseline.
- CodeUpdate-0031 adds a non-gameplay Server bootstrap entry point, bootstrap validation, and smoke coverage.
- The ChatGPT packaging container does not have the .NET SDK, so local restore/build/test could not be run here.
- GitHub Actions remains pending until the user applies, commits, and pushes CodeUpdate-0031.

## Source of Truth

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the authoritative source of truth.
- Old ZIPs, prior generated patches, and conversation memory are not authoritative when they conflict with GitHub.

## Work Mode Rule

- Codex mode updates local only and waits for the user to push and state that GitHub Actions is green before continuing.
- ChatGPT mode uses patch ZIPs for local application unless the user explicitly requests direct GitHub edits.

## Update Packaging

- ChatGPT-created CodeUpdate ZIPs are patch-only packages, not full repository rebuilds, unless explicitly requested.
- Each ChatGPT-created update ZIP must contain exactly one top-level folder and an included `Apply-CodeUpdate-XXXX.bat` installer.
- Each ChatGPT-created update ZIP must include `UpdateManifest.json`.
- The ZIP must be physically created and inspected before it is announced.

## Never Guess Rule

- Never guess when creating, fixing, reviewing, or reporting an update.
- Verify current file paths, APIs, package contents, build status, milestone status, and update status before claiming success.
- If something cannot be verified in the current environment, say so clearly.
