# CodeUpdate-0031A - Server Smoke Test Compile Fix

## Purpose

Correct the CodeUpdate-0031 GitHub Actions failure reported by the user.

## Failure

GitHub Actions failed while restoring/building Reforged source projects with repeated errors at:

`Tests/Source/Reforged.Core.Tests/Program.cs#L59`

Error:

`Syntax error, ',' expected`

## Root Cause

The configuration smoke test used unescaped JSON quotes inside a normal C# string literal:

```csharp
File.WriteAllText(temp, "{"server":{"port":4000},"enabled":true}");
```

That is invalid C# syntax.

## Fix

The JSON string is now escaped correctly:

```csharp
File.WriteAllText(temp, "{\"server\":{\"port\":4000},\"enabled\":true}");
```

## Files Changed

- `Tests/Source/Reforged.Core.Tests/Program.cs`
- `AGENTS.md`
- `AI_CONTEXT.md`
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- `ProjectManifest.json`
- `ReleaseNotes.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- `Reports/CodeUpdates/CodeUpdate-0031A/CodeUpdate-0031A-Server-Smoke-Test-Compile-Fix.md`

## Boundaries

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No live editor implementation.

## Verification

Verified before packaging:

- GitHub truth was inspected.
- User-provided GitHub Actions failure output identified the same file and line.
- The patch is limited to the compile fix and continuity documentation.
- The update ZIP was verified to contain exactly one top-level folder, an apply BAT, an UpdateManifest, and patch-only payload.

Not verified locally:

- `.NET` restore/build/test could not be run in the ChatGPT packaging container because the .NET SDK is not installed.

Official gate:

- GitHub Actions after user apply/commit/push.
