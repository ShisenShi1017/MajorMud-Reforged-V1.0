# Core Library Foundation

Code Update 0004 starts Milestone 1: Core Library.

## Purpose

Core becomes the shared foundation for every MajorMud Reforged application.

## Added Concepts

- Project versioning model
- Repository path contract
- Logging entry model
- Validation issue/result model
- Base data record contract
- Source/provenance reference contract
- Initial room and item record examples
- Import audit record
- Serialization and event bus contracts

## Important Rule

No application should create a separate competing definition of shared data records. If Server, Importer, Editor, Launcher, or Client needs shared game/data behavior, it belongs in Core first.

## Executables

No executable is created in this update. Future compiled Windows applications must live in their own folders under `Bin/Windows/`.
