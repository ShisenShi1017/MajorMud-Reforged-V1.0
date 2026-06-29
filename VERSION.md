# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0025
- Current Database Version: 0009
- Milestone: Milestone 1 - Core Framework / Rebrand Completion Audit
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0025 - Rebrand Completion Audit
- Milestone 1 closure track is active.
- Local build/test validation should be run when the .NET SDK is available; GitHub Actions remains the official validation gate.

## Update Packaging

- CodeUpdate ZIPs are patch-only packages, not full repository rebuilds, unless explicitly requested.
- Each update ZIP must contain exactly one top-level folder and an included `Apply-CodeUpdate-XXXX.bat` installer.

- CodeUpdate-0024B is a corrective patch for the CodeUpdate-0024 startup diagnostics logger API mismatch and adds no gameplay features.
- No BAT installer or update ZIP was created for CodeUpdate-0024B per current user instruction.

- CodeUpdate-0025 is a rebrand completion audit and direct local patch with no gameplay features.
- No BAT installer or update ZIP was created for CodeUpdate-0025 per current workflow; the user controls GitHub push.
