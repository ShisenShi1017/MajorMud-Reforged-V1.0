# MajorMud Reforged Architecture

Last Updated: 2026-06-27

## Current Architectural Position

This document records architecture decisions for MajorMud Reforged. It should be updated as the server, editor, shared data, and future clients evolve.

## Current Repository Structure Observed

The uploaded project already uses a structured layout. The existing layout must be preserved unless the user explicitly approves a change.

Observed top-level areas include:

- `Source/`
- `Data/`
- `SharedData/`
- `Docs/`
- `ImportedDatabases/`
- `Saves/`
- `PatchFiles/`
- `PatchBackup/`
- `Backups/`
- `Client-PC/`
- `Client-Android/`
- `HostEditor-PC/`
- `Tools/`

## Architectural Source of Truth

- Code source of truth: GitHub or the uploaded project ZIP.
- Vision source of truth: `Docs/00-ProjectVision.md` and user-provided summaries.
- Session source of truth: `Docs/04-SessionHandoff.md`.

## Safety Boundaries

The following locations are protected unless the user explicitly approves a specific change:

- `Saves/`
- `ImportedDatabases/`
- Existing imported database files.
- Existing save formats.

## Future Architecture Topics To Document

- Server loop.
- Command parsing.
- Combat system.
- Character persistence.
- Database import strategy.
- Editor data model.
- Network protocol.
- Client protocol.
- Save compatibility rules.
- Plugin or scripting architecture, if approved.
