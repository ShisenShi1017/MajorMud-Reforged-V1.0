# MajorMud Reforged Coding Standards And Development Rules

Last Updated: 2026-06-27

## Source Of Truth

- The GitHub repository or uploaded project ZIP is the code source of truth.
- `Docs/00-ProjectVision.md` and user-provided summaries are the vision source of truth when the original PDF is unavailable.

## Required Workflow

Before implementing a feature:

1. Inspect the existing project.
2. Compare the request against the project vision.
3. Make the smallest safe change.
4. Preserve backward compatibility unless a migration is explicitly approved.
5. Never recreate the project from scratch.
6. Never overwrite saves or imported databases.
7. Update documentation under `Docs/`.
8. Include release notes.
9. Include patch history.
10. Be honest about what was actually created or changed.

## Patch Rules

Every official patch must:

- Be targeted.
- Run from the extracted patch folder.
- Include an `Apply-*.bat` file.
- Include release notes.
- Include patch history.
- Avoid touching `Saves/` unless explicitly approved.
- Avoid touching `ImportedDatabases/` unless explicitly approved.
- Avoid changing save/database formats unless explicitly approved.

## Batch Patch Rule

The `Apply-*.bat` file must be designed to run from the extracted patch folder, not from inside the project root.

The patch should safely copy only the intended payload into the target project folder.

## Documentation Rules

Every development session should update:

- `03-DevelopmentJournal.md`
- `04-SessionHandoff.md`

Update these when relevant:

- `01-Architecture.md`
- `02-Roadmap.md`
- `05-Suggestions.md`
- `07-KnownIssues.md`
- `08-DatabaseFormat.md`
- `09-NetworkProtocol.md`
- `10-DecisionLog.md`

## Claim Rule

Do not claim that a patch, executable, feature, or file exists unless it has actually been created.
