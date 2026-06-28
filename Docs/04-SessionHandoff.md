# Session Handoff

## Current State

The repository has been reorganized and deduplicated.

Active layout:

- `Apps/`
- `Core/`
- `Data/`
- `Docs/`
- `ImportedDatabases/`
- `Reports/`
- `Saves/`
- `Tools/`
- `Updates/`
- `Bin/`
- `Backups/` local output only
- `Logs/` local output only
- `Legacy/` minimal note only

## Last Completed Update

Code Update 0002 - Repository Deduplication

## Important Rules

- Database Versions are for game data/import changes.
- Code Updates are for code, repository, tooling, launcher, server, editor, and client changes.
- Every update must run from its own extracted folder.
- Every update must update root `README.md`.
- Do not overwrite `Saves/` or `ImportedDatabases/` without explicit approval.

## Next Recommended Work

Start the real `Core` implementation for server loading:

1. Define core model contracts.
2. Add data manifest loader.
3. Add validation result model.
4. Add server bootstrap that reads only `Data/`.


## Current Handoff After CodeUpdate-0003

Repository layout is frozen. Next recommended task is Core Foundation implementation: data models, validation contracts, serialization, logging, configuration, and server load contract.
