# MajorMud Reforged Session Handoff

Last Updated: 2026-06-27

## Current Focus

Project foundation before server development.

## Current Project Direction

MajorMud Reforged is a standalone MajorMud server project with editor tools and future client work similar to MegaMud 2.0.

## Current Patch

Patch 0001: Documentation Foundation.

## Last Completed

- Created the project documentation system.
- Established project vision, roadmap, coding standards, suggestions, decision log, and handoff files.

## Next Recommended Task

Inspect the existing server files and determine the first targeted server patch.

Recommended starting files/areas to inspect:

- `Server.ps1`
- `Client.ps1`
- `Source/`
- `SharedData/`
- `Data/`
- Existing docs/changelogs under `Docs/`

## Protected Areas

Do not overwrite or migrate without explicit user approval:

- `Saves/`
- `ImportedDatabases/`
- Save formats.
- Imported database files.

## Notes For Future ChatGPT Sessions

Before implementing any feature:

1. Inspect the current uploaded ZIP or repository.
2. Read this handoff file.
3. Read `00-ProjectVision.md`.
4. Read `06-CodingStandards.md`.
5. Make only a targeted patch.
6. Include an `Apply-*.bat` that runs from the extracted patch folder.
7. Update docs, release notes, and patch history.
