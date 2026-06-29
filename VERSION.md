# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0034
- Current Database Version: 0009
- Milestone: Milestone 2 - Engine Modernization / Host & World Editor Planning
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0034 - OmniVerse Reference Audit Plan
- Milestone 1 Core Framework is complete/frozen.
- CodeUpdate-0031 added a non-gameplay Server bootstrap entry point, bootstrap validation, and smoke coverage.
- CodeUpdate-0031 failed GitHub Actions in the test build due to invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`.
- CodeUpdate-0031A fixes the syntax error only.
- CodeUpdate-0031A GitHub Actions is green by user report.
- Codex local .NET 8.0.422 sequential restore/build/smoke verification passed.
- CodeUpdate-0032 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0032 GitHub Actions is green by user report.
- CodeUpdate-0033 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0033 GitHub Actions is green by user report.
- CodeUpdate-0034 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0034 GitHub Actions is pending until the user pushes and reports the result.

## Reference Data Policy

- Reforged `Data/` remains canonical.
- `ImportedDatabases/` remains archive/reference only.
- OmniVerse may be used as a read-only comparison source to identify possible corrections.
- No website/API data may overwrite Reforged data without a reviewed, approved follow-up update.

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
