# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0024
- Current Database Version: 0009
- Milestone: Milestone 1 - Core Framework / Core Verification
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0024 — Core Verification
- Milestone 1 closure track is active.
- Final build/test validation is expected from GitHub Actions.

## Update Packaging

- CodeUpdate ZIPs are patch-only packages, not full repository rebuilds, unless explicitly requested.
- Each update ZIP must contain exactly one top-level folder and an included `Apply-CodeUpdate-XXXX.bat` installer.
