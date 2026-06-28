# App Platform Vision

## Long-term goal

MajorMud Reforged should become a standalone application suite:

- Launcher
- Server
- Editor
- Importer
- Client
- Shared Core

## Launcher

The Launcher should eventually become the main app:

- Start/stop server
- Open editor
- Install database versions
- Install code patches
- Validate database
- Create/restore backups
- View logs
- Open documentation

## Server

The Server loads only from `Data/` and builds the game world in memory. It does not depend on original MajorMud files after import.

## Editor

The Editor should be inspired by Nightmare-Redux but should work through Reforged APIs and Reforged `Data/`, not direct live file mutation.

## Importer

The Importer converts source data into Reforged `Data/` and verifies data integrity. It should not require Access, OLEDB, Office, or external database engines.

## Client

The future Client should be inspired by MegaMud 2.0, including mapping, triggers, aliases, macros, profiles, hotkeys, and modern UI.
