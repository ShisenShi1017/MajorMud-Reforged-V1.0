# CodeUpdate-0026 - Milestone 1 Finalization

This update prepares the Milestone 1 Core Framework baseline for final freeze.

## Changed

- Added a Milestone 1 verification report.
- Updated continuity documents and manifests for CodeUpdate-0026.
- Prepared Milestone 2 roadmap direction without starting Milestone 2 implementation.

## Local Verification

- .NET SDK 8.0.422 was detected locally.
- Restore/build passed for Core, all application projects, and Core smoke tests after the 0026 finalization edits.
- Core smoke tests passed and reported all Milestone 1 core smoke tests passed.

## Notes

- No gameplay features are added.
- No BAT installer or update ZIP was created by Codex.
- The user controls the GitHub push.
- Milestone 1 should be marked frozen only after CodeUpdate-0026 GitHub Actions is green.

## Previous Release Notes
# CodeUpdate-0025 - Rebrand Completion Audit

This update completes the active rebrand audit for Milestone 1 closure.

## Changed

- Removed obsolete duplicate importer utility Tools/Importer/MajorMudDatDecoder.py.
- Confirmed active source projects, assemblies, namespaces, and workflows use Reforged.* naming.
- Updated continuity documents for CodeUpdate-0025.

## Local Verification

- .NET SDK 8.0.422 was detected locally.
- Restore/build passed for Core, all application projects, and Core smoke tests after the 0026 finalization edits.
- Core smoke tests passed and reported all Milestone 1 core smoke tests passed.

## Notes

- No gameplay features are added.
- No BAT installer or update ZIP was created by Codex.
- The user controls the GitHub push; GitHub Actions remains the official validation gate after push.

## Previous Release Notes

# CodeUpdate-0024B - Core Startup Diagnostics Logger Fix

This corrective patch repairs the CodeUpdate-0024 build failure in CoreStartupDiagnostics.

## Fixed

- Replaced the invalid concrete logger Info(...) call with the verified existing Write(LogSeverity.Information, ...) API.


## Notes

- This is a build-fix patch only.
- No gameplay features are added.
- No BAT installer or update ZIP was created per current user instruction.
- GitHub Actions remains the official validation gate after commit/push.

## Previous Release Notes

# CodeUpdate-0023 - Fresh GitHub Actions Workflow with BAT Installer

This update repeats the fresh GitHub Actions workflow setup and adds the missing Windows batch installer.

## Added

- `.github/workflows/dotnet-build.yml`
- `GitHubActions/dotnet-build.yml`
- `Install-GitHub-Actions-Workflow.bat`

## How to use

1. Extract this update over your repository.
2. Run `Install-GitHub-Actions-Workflow.bat` from the repository root.
3. Commit and push to GitHub.
4. Open the GitHub Actions tab and check `Reforged Cloud Build`.

## Notes

- The workflow uses only `Reforged.*` project paths.
- The Node.js warning from GitHub-hosted actions is not treated as the project build failure.
- No local .NET SDK is required for normal runtime use.
