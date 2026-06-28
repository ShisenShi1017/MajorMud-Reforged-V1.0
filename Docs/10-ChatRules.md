# Chat and Update Rules

These rules allow future ChatGPT sessions to continue the project correctly.

## Source of truth

- Current repository ZIP/GitHub repo is the code source of truth.
- `Data/` is the standalone Reforged database source of truth.
- `ImportedDatabases/` is reference/fallback only.

## Update rules

- All Database Versions and Code Updates must run from their own extracted folder.
- Every update must include an `Apply-*.bat` until the Launcher update manager replaces it.
- The apply script must auto-locate the project root and should not ask the user to type a path.
- Every update must update root `README.md`.
- Every update must update Docs, release notes, and session handoff.
- Saves and imported databases must not be overwritten.

## Architecture rules

- Move away from PowerShell prototypes.
- Create real applications under `Apps/`.
- Put shared logic under `Core/`.
- Put every future `.exe` in its own folder under `Bin/Windows/`.
- Do not duplicate data models across Server, Editor, Importer, Launcher, and Client.

## Honesty rule

Do not claim that a patch, database version, executable, decoded format, or feature exists unless it has actually been created and verified.
