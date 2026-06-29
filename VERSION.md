# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0026B
- Current Database Version: 0009
- Milestone: Milestone 1 - Core Framework / Finalization Candidate
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0026B - Baseline Cleanup
- Milestone 1 finalization candidate is active; freeze is pending the CodeUpdate-0026B GitHub Actions gate.
- Local .NET 8.0.422 restore/build/smoke-test verification passed; GitHub Actions remains the official validation gate after user push.

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
