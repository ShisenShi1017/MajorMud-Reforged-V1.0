# Coding Standards and Development Rules

## Project rules

- Never recreate the project from scratch.
- Always inspect the existing project before changing anything.
- Use the current repository ZIP/GitHub repo as the source of truth for code.
- Use `Data/` as the active Reforged game database.
- Use `ImportedDatabases/` only as source/reference/fallback after conversion.
- Never overwrite `Saves/`.
- Never overwrite imported databases.
- Maintain backward compatibility unless a migration is explicitly approved.
- Every Code Update and Database Version must update documentation.
- Every update must replace/update the root `README.md` with what changed.
- Every update must run from its own extracted update folder.
- Do not require manual path entry for normal apply scripts.
- Do not claim a feature, executable, or conversion is complete unless it was actually created and verified.

## Naming

- Database Version: game data/import/database changes.
- Code Update: server/editor/importer/launcher/client/tool/repository changes.

## Executable rule

Every future executable must live in its own dedicated folder under:

```text
Bin/Windows/<AppName>/
```

## PowerShell rule

PowerShell is prototype/legacy only going forward. New core project behavior should move toward compiled applications and shared Core code.
