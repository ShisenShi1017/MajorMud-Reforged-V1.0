# Reforged Development Roadmap and Project Constitution

## READ THIS FIRST

If you are an AI assistant, new ChatGPT window, or new developer joining this project, read this document before changing anything.

1. The latest uploaded repository is the only code source of truth.
2. Inspect the repository before making changes.
3. Read `AI_CONTEXT.md`, `README.md`, `VERSION.md`, `CHANGELOG.md`, `ProjectManifest.json`, and this document.
4. Continue from the latest completed Code Update.
5. Never recreate existing work from memory.
6. Never restructure the repository unless the user explicitly approves it.
7. Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
8. `Data/` is the live game database after import.
9. `ImportedDatabases/` is archive/reference only.
10. Runtime must never require the .NET SDK.
11. Every Code Update must execute directly from its own extracted update folder.
12. Every Code Update must automatically locate the project root.
13. Every Code Update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, `ProjectManifest.json`, and relevant documentation.
14. Never claim an update package exists until the ZIP has actually been created and verified.
15. All approved architectural or project suggestions become implementation requirements for the next applicable Code Update unless the user explicitly defers or rejects them.
16. Every update ZIP must contain exactly one named top-level update folder.
17. The user must be able to extract the ZIP directly into the project folder, open the update folder, and run the apply BAT from there.

## Project Vision

Reforged is not a runtime-dependent copy of the original legacy MUD. It is a clean, standalone game engine and toolset inspired by legacy MUD. Original legacy MUD data may be imported, decoded, compared, and audited, but after import the project must operate from its own simple, editable `Data/` files.

The long-term goal is a standalone server, native importer, live editor, launcher, Windows client, Android client, gameplay systems, and plugin framework that do not require the original legacy MUD runtime.

## Milestone 1 — Core Framework (Current)

Goal: Build the shared foundation that every application depends on.

Deliverables:

- Core configuration system
- Core logging system
- Validation framework
- Serialization framework
- Repository interfaces
- Shared data models
- Event system
- Dependency injection/service registry
- Version management
- Project manifest reader
- Repository health checker
- Shared utility library
- Unit-test framework when appropriate

Applications using Core:

- Server
- Importer
- Editor
- Launcher
- Client

Completion Criteria:

- No duplicated models across applications.
- Every application references Core.
- Core has no dependencies on application projects.
- Runtime does not require the .NET SDK.
- Handoff documents accurately track Milestone 1 progress after every update.

## Milestone 2 — Native Importer

Goal: Completely remove dependence on the original legacy runtime.

Import Sources:

- Reforged `.DAT`
- CSV exports
- companion client `.mp`

Features:

- Native DAT decoder
- CSV importer
- companion client MP importer
- Duplicate detection
- Cross-reference validation
- Unknown field preservation
- Database verification
- JSON generation
- Import audit reports
- Database version generation

Completion Criteria:

- Server loads only from `Data/`.
- Imported databases become reference-only.

## Milestone 3 — Standalone Server

Goal: Build the standalone game server.

Features:

- Load `Data/`
- World initialization
- Room manager
- Item manager
- Monster manager
- Spell manager
- Shop manager
- Player manager
- Save system
- Main game loop
- Session manager
- Network framework
- Command dispatcher
- Validation before startup

Completion Criteria:

- Server boots successfully using only Reforged data.

## Milestone 4 — Live Editor

Goal: Replace static editing with live server editing.

Features:

- Connect to running server
- Live edits
- Validation
- Undo history
- Database browser
- Search
- Diff viewer
- Relationship editor
- Room editor
- Monster editor
- Item editor
- Spell editor
- Shop editor

Completion Criteria:

- Server updates safely without direct file editing.

## Milestone 5 — Launcher

Goal: Replace BAT files and PowerShell.

Features:

- Start server
- Start editor
- Start client
- Apply Code Updates
- Apply Database Versions
- Backup manager
- Restore manager
- Validation
- Repository health
- Update manager
- Log viewer

Completion Criteria:

- No routine use of BAT files beyond compatibility.

## Milestone 6 — Windows Client

Goal: Build a modern companion-client-inspired client.

Features:

- Telnet/network engine
- Map window
- Inventory
- Equipment
- Spell windows
- Trigger system
- Macro system
- Logging
- Themes
- Plugins

Completion Criteria:

- Fully functional Windows client.

## Milestone 7 — Android Client

Goal: Mobile companion.

Features:

- Touch interface
- Saved layouts
- Map
- Macros
- Inventory
- Notifications

## Milestone 8 — Gameplay Systems

Goal: Complete the server.

Systems:

- Combat
- AI
- Skills
- Magic
- Shops
- Banking
- Gangs
- Lairs
- Quests
- Events
- Scripting
- Respawns

## Milestone 9 — Plugin Framework

Goal: Allow extensions without modifying Core.

Features:

- Plugin API
- Events
- Commands
- Data hooks
- Server extensions
- Editor extensions

## Milestone 10 — Version 1.0

Goal: Production-ready release.

Final Deliverables:

- Standalone server
- Native importer
- Live editor
- Launcher
- Windows client
- Android client
- Complete documentation
- Automated validation
- Stable update system

## Permanent Project Rules

- The latest uploaded repository is the only code source of truth.
- The `Data/` folder is the authoritative game database after import.
- `ImportedDatabases/` remains archive/reference only.
- Every Code Update and Database Version must inspect the repository before making changes.
- Every update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, `ProjectManifest.json`, and relevant documentation.
- Every update must include a self-contained `Apply-*.bat` until the Launcher replaces it.
- Every update must automatically locate the project root.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Never claim an update package exists unless it has actually been created and verified.
- Repository layout is frozen unless the user explicitly approves a structural change.
- Runtime must never require the .NET SDK.
- Every update package must be executable directly from its own extracted folder and apply to the main project folder.
- All approved suggestions must be implemented in the next applicable update unless explicitly deferred or rejected.

## Suggestion Implementation Policy

Suggestions made by the assistant are not loose ideas after the user approves them. Approved suggestions become tracked implementation requirements.

Rules:

- Implement all approved suggestions in the next applicable Code Update or Database Version.
- If a suggestion cannot be completed safely, document the reason and mark it deferred.
- Do not silently drop suggestions.
- Record completed suggestions in `CHANGELOG.md` and `UpdateManifest.json`.
- Future sessions must treat this section as a standing instruction.

## Architecture Rules

- Core contains no application-specific logic.
- Every application references Core.
- No duplicated models across applications.
- Dependency direction always points toward Core.
- Core remains independently testable.
- Shared data contracts live in Core.
- Applications may compose Core services but must not fork shared behavior.

## Runtime Rules

- Reforged must run without the .NET SDK.
- Distributed runtime uses compiled/runtime-ready artifacts from `Bin/`.
- Development/build machines may use SDKs, but SDK commands are not the user launch path.
- No update script may fall back to SDK runtime behavior.

## Update Rules

Each Code Update must:

- run directly from its extracted folder
- automatically locate the project root
- validate repository identity and version before applying
- create backups before replacing files
- preserve `Saves/`
- preserve `ImportedDatabases/`
- update project documentation and manifests
- include `UpdateManifest.json`
- include `Apply-CodeUpdate-XXXX-Name.bat`
- be ZIP-created and verified before being offered

## Database Rules

- `Data/` is the live editable database.
- `ImportedDatabases/` is archive/reference only.
- Database Versions are separate from Code Updates.
- Unknown imported fields must never be discarded.
- Imported data should be converted into simple, human-editable Reforged data files.

## AI Development Rules

Every new ChatGPT session should:

1. Read `AI_CONTEXT.md`.
2. Read this roadmap.
3. Read `README.md`.
4. Read `VERSION.md`.
5. Read `CHANGELOG.md`.
6. Read `ProjectManifest.json`.
7. Inspect repository files before making changes.
8. Continue from the latest completed Code Update.
9. Never regenerate existing work from memory.
10. Never claim a ZIP exists until it is physically present and verified.

## Current Development Position

The project is currently in Milestone 1. Core Update 0008 adds project governance documents and continues Core framework work by adding shared configuration, serialization, event bus, service registry, manifest, health, utility, and versioning components.

### CodeUpdate-0009 Continuation Notes

Core Update 0009 adds the shared `IReforgedDataRecord` contract, extensible `GameEntity`, read-only repository contract, repository catalog contract, Core reference audit model, duplicate model audit model, and dependency-free Core test foundation. These items advance Milestone 1 but do not close it.

Remaining Milestone 1 emphasis: every application must integrate with Core, duplicate application models must be removed or redirected to Core, and compiled `Bin/` runtime handoff must be validated without requiring the .NET SDK.

## CodeUpdate-0010 Handoff and Update Folder Rule

Added in CodeUpdate-0010:

- `AI_CONTEXT.md` is a living handoff document and must be updated with every Code Update.
- The handoff must include the current milestone, update number, completed/substantially-underway table, Milestone 1 completion checklist, remaining work, and next recommended work.
- Every Code Update and Database Version ZIP must contain exactly one labeled top-level folder named for the update.
- The user must be able to extract the ZIP directly into the main project folder without manually creating a folder.
- The `Apply-*.bat` file must live inside that labeled update folder and run from there.
- The apply script must automatically locate the project root before copying payload files.
- Approved suggestions are implementation requirements unless explicitly deferred or rejected.

