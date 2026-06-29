# CodeUpdate-0035 - Server Bootstrap Validation Summary

This update improves non-gameplay Server bootstrap diagnostics for host tooling.

## Changed

- Server bootstrap reports now expose validation issue counts.
- Server startup output now prints a validation summary by severity.
- Smoke tests verify issue-count behavior for success and failure paths.
- CodeUpdate-0034 is recorded as green by user report.

## Boundaries

- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions remains the official validation gate after the user pushes this update.

## Previous Release Notes
# CodeUpdate-0034 - OmniVerse Reference Audit Plan

This update records OmniVerse as a read-only comparison source for future data audits.

## Changed

- Documented OmniVerse public reference endpoints verified during discovery.
- Added a reference-source policy: OmniVerse can help identify mismatches, but it cannot overwrite Reforged `Data/`.
- Recorded CodeUpdate-0033 as green by user report.
- Added a 0034 audit plan and report.

## Boundaries

- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- No files under `Data/` or `ImportedDatabases/` were changed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0033 - Server Bootstrap Exit Code Contract

This update improves the non-gameplay Server bootstrap contract for automation and diagnostics.

## Changed

- Server bootstrap reports now expose an explicit exit code.
- Server startup returns the report exit code on success and failure.
- Smoke tests verify success returns exit code 0 and failure returns exit code 1.
- Continuity documents record CodeUpdate-0032 as green by user report.

## Boundaries

- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0032 - Server Bootstrap Manifest Identity

This update improves the non-gameplay Server bootstrap diagnostics.

## Changed

- Server bootstrap report now captures ProjectManifest identity fields.
- Server bootstrap console output now prints project, CodeUpdate, database version, and milestone.
- Smoke tests now verify those manifest identity fields are captured.
- Continuity documents record the 0032 scope and next handoff rule.

## Handoff Improvement

Future updates should include a short next-scope candidate and explicit boundaries separately from completed work. This makes handoff safer without starting unapproved work.

## Boundaries

- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0031A - Server Smoke Test Compile Fix

This update is a corrective build-fix patch for CodeUpdate-0031.

## Fixed

- Corrected the configuration smoke-test JSON string in `Tests/Source/Reforged.Core.Tests/Program.cs`.
- The previous CodeUpdate-0031 file contained unescaped JSON quotes inside a normal C# string literal, causing GitHub Actions to fail with `Syntax error, ',' expected` at `Program.cs#L59`.

## Boundaries

- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- GitHub truth was read before creating this patch.
- The user-provided GitHub Actions failure output was matched to the current GitHub file.
- The update ZIP was verified for one top-level folder, BAT installer, manifest, and patch-only payload before delivery.
- The ChatGPT packaging container did not include the .NET SDK, so local restore/build/test could not be run during ZIP creation.
- The user later reported CodeUpdate-0031A GitHub Actions green.
- Codex local .NET 8.0.422 sequential restore/build/smoke verification passed after the patch was applied locally.
