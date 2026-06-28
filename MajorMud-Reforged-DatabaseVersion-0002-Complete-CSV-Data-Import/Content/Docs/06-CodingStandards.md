# Coding and Release Standards

## General Rules

- Never recreate the project from scratch.
- Inspect the existing project before changes.
- Keep the existing folder structure unless approved.
- Never overwrite `Saves/` or `ImportedDatabases/`.
- Maintain backward compatibility unless a migration is explicitly approved.
- Add or update documentation for every patch and database version.

## Patches vs Database Versions

- A **patch** changes code, tools, server, editor, or clients.
- A **database version** changes converted editable game data under `Data/`.

## Apply Script Rule

Every official patch or database version must be applied from its extracted folder using an `Apply-*.bat` file.
