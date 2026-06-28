# Coding and Release Standards

## Project Rules

- Never recreate the project from scratch.
- Always inspect existing project structure before changing anything.
- Code patches update code. Database versions update imported/converted data.
- Every code patch and database version must update documentation.
- Official update packages must run from their extracted folder.
- Apply scripts must not require the user to type the project path.
- Never overwrite `Saves/`.
- Never overwrite original files in `ImportedDatabases/` unless the update is explicitly an archival copy into a new versioned/reference folder.
- Maintain backward compatibility unless a migration is explicitly approved.
- Do not claim a feature, executable, decoder, or import is complete unless it has actually been created and verified.

## Database Version Rules

- Database versions must be zero-duplicate for exported record IDs.
- Database versions must include an audit report.
- Unknown fields must be preserved, not discarded.
- Native source files are reference/import inputs only.
- Reforged `Data/` is the server's intended runtime database.
