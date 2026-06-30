# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0039
- Current Database Version: 0009
- Milestone: Milestone 2 - Engine Modernization / Host & World Editor Planning
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0039 - Server Bootstrap Readiness Summary
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
- CodeUpdate-0034 GitHub Actions is green by user report.
- CodeUpdate-0035 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0035 GitHub Actions is green by user report.
- CodeUpdate-0036 is documentation/research only and is present in GitHub `origin/main` at commit `105ed76b`.
- CodeUpdate-0037 is documentation-only; it combines the CodeUpdate-0036B continuity cleanup package with the CodeUpdate-0037 Software Design Specification package.
- CodeUpdate-0037 JSON parse and local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0037 GitHub Actions is green by user report.
- CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap path.
- CodeUpdate-0038 local .NET 8.0.422 JSON parse, restore, sequential Release build, smoke tests, and Server bootstrap run passed.
- CodeUpdate-0038 GitHub Actions is green by user report.
- CodeUpdate-0039 adds a host-tool readiness summary while keeping gameplay disabled.
- CodeUpdate-0039 records the Classic-first Server direction as planning/continuity documentation.
- CodeUpdate-0039 records engine-first architecture direction as planning/continuity documentation.
- CodeUpdate-0039 adds the user-provided WCC default install under `ImportedDatabases/WCCDefaultInstall/` as reference/import material only.
- CodeUpdate-0039 local .NET 8.0.422 JSON parse, affected Release builds, smoke tests, Server bootstrap run, and WCC default archive integrity verification passed after folded documentation/import work.

## Reference Data Policy

- Reforged `Data/` remains canonical.
- `ImportedDatabases/` remains archive/reference only.
- OmniVerse may be used as a read-only comparison source to identify possible corrections.
- LLT XML, MegaMUD databases, MegaMUD `.mp` paths, Mud Hex documentation, MudInfo, MudCentral, Reddit/community sources, WCC archives, and WSGSERVE `.dat` files are reference or verification sources only unless explicitly approved for a reviewed import/reconciliation update.
- No website/API/community/tool data may overwrite Reforged data without a reviewed, approved follow-up update.

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
