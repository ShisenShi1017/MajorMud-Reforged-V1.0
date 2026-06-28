# MajorMud Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0017
- Current Database Version: 0009
- Milestone: Milestone 1 - Core Framework / Runtime Artifact Expectations and GitHub Verification
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions is the recommended cloud build path with `.github/workflows/dotnet-build.yml`; the user does not need the .NET SDK locally.
- Update Requirement: Code Updates run from their own extracted update folder and apply to the main project folder.
- Update ZIP Requirement: Every update ZIP contains exactly one named top-level update folder.
- Suggestion Policy: Approved suggestions become implementation requirements unless explicitly deferred or rejected.
- Handoff Requirement: `AI_CONTEXT.md` is the authoritative handoff document and must be updated every Code Update.
