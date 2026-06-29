# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0029
- Current Database Version: 0009
- Milestone: Milestone 2 - Engine Modernization / Host & World Editor Planning
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0029 - Host Server Startup Diagnostics
- Milestone 1 Core Framework is complete/frozen; the user reported CodeUpdate-0028 GitHub Actions green.
- CodeUpdate-0029 local .NET 8.0.422 restore/build/core-server-smoke verification passed; GitHub Actions remains pending until user push.

## Update Packaging

- CodeUpdate ZIPs are patch-only packages, not full repository rebuilds, unless explicitly requested.
- Each update ZIP must contain exactly one top-level folder and an included `Apply-CodeUpdate-XXXX.bat` installer.

- CodeUpdate-0024B is a corrective patch for the CodeUpdate-0024 startup diagnostics logger API mismatch and adds no gameplay features.
- No BAT installer or update ZIP was created for CodeUpdate-0024B per current user instruction.

- CodeUpdate-0025 is a rebrand completion audit and direct local patch with no gameplay features.
- No BAT installer or update ZIP was created for CodeUpdate-0025 per current workflow; the user controls GitHub push.

- CodeUpdate-0026 is a Milestone 1 finalization candidate with local verification passed and GitHub Actions pending after user push.
- No BAT installer or update ZIP was created for CodeUpdate-0026; the user controls GitHub push.

- CodeUpdate-0026B is a baseline cleanup patch for generated artifacts, ignore rules, and documentation encoding cleanup.
- No BAT installer or update ZIP was created for CodeUpdate-0026B; the user controls GitHub push.

- CodeUpdate-0027 freezes the Milestone 1 baseline after user-reported green GitHub Actions for CodeUpdate-0026B.
- No BAT installer or update ZIP was created for CodeUpdate-0027; the user controls GitHub push.

- CodeUpdate-0028 records CodeUpdate-0027 green and defines the first Milestone 2 scope plan.
- No BAT installer or update ZIP was created for CodeUpdate-0028; the user controls GitHub push.

- CodeUpdate-0029 adds startup-only Server diagnostics as the first Milestone 2 implementation step.
- No BAT installer or update ZIP was created for CodeUpdate-0029; the user controls GitHub push.
