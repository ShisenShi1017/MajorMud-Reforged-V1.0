# ChatGPT Handoff — CodeUpdate-0006

This file exists so a new chat window can continue the project without losing direction.

## Must-read files for every new chat

1. `README.md`
2. `VERSION.md`
3. `ProjectManifest.json`
4. `CHANGELOG.md`
5. `Docs/SessionHandoff.md`

## Current milestone

Milestone 1 — Core Framework.

## Current next work

Continue implementing real Core source files and contracts. Do not restart repository reorganization unless the user explicitly requests it.

## Critical rules

- The latest uploaded/current repository is the only code source of truth.
- Use CodeUpdate-0006 as the current update marker after this update is applied.
- DatabaseVersion-0009 is the current server-ready data baseline.
- Preserve Saves and ImportedDatabases.
- All updates must run from their own extracted folder.
- All update ZIPs must be generated and verified before being linked.

## Project vision summary

Reforged is a standalone Reforged platform with:

- Server
- Core library
- Importer
- Editor
- Launcher
- Future Windows client
- Future Android client

Runtime server data must come from `Data/`, not original DAT/MDB/ACCDB/CSV files.
