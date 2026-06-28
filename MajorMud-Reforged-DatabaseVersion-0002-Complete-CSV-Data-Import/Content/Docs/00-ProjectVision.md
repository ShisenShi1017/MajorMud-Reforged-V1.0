# MajorMud Reforged Project Vision

MajorMud Reforged is a standalone MajorMUD-style server and editor project.

The project uses original MajorMUD data only as an import source. After conversion, the game should run from simple editable files under `Data/`.

## Core Goals

- Standalone server first.
- Editable data owned by MajorMud Reforged.
- Imported databases are reference-only after conversion.
- Editor support should eventually allow safe live editing through the server.
- Future clients may be inspired by MegaMud 2.0.

## Database Direction

`ImportedDatabases/` is not the live game database. `Data/` is the live/editable database.
