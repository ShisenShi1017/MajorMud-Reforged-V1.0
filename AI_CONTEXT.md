# AI Context for MajorMud Reforged

Read this file first in every new ChatGPT window or development session.

MajorMud Reforged is a standalone game engine and toolset inspired by the original MajorMUD. The original data is an import source only. After import, `Data/` is the live editable game database and `ImportedDatabases/` is archive/reference only.

## Start Here

1. Read `Docs/Architecture/PROJECT_ROADMAP.md`.
2. Read `README.md`.
3. Read `VERSION.md`.
4. Read `CHANGELOG.md`.
5. Read `ProjectManifest.json`.
6. Inspect the repository before changing anything.

## Current Direction

Current milestone: Milestone 1 — Core Framework.

The goal is to finish the shared Core foundation used by Server, Importer, Editor, Launcher, and Client.

## Permanent Rules

- The latest uploaded repository is the only source of truth.
- Never recreate existing files from memory.
- Repository layout is frozen unless the user explicitly approves a structural change.
- Runtime must not require the .NET SDK.
- Runtime launches compiled/runtime-ready artifacts from `Bin/`.
- Every Code Update must run from its own extracted folder and apply to the main project folder.
- Every update must automatically locate the project root.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Every update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, `ProjectManifest.json`, and relevant docs.
- Never claim a package exists unless the ZIP has actually been created and verified.
- All approved suggestions become implementation requirements unless explicitly deferred or rejected.

## Update Naming

The next Code Update after 0007 is Core Update 0008.

Database conversions/imports are called Database Versions, not patches.
