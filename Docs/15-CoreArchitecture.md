# Core Architecture

## Goal

`Core/` is the shared technical heart of MajorMud Reforged.

The Server, Editor, Importer, Launcher, and future Client should not each define their own incompatible versions of rooms, items, monsters, spells, shops, races, classes, or database manifests.

## Core owns

- Data models
- Data manifest structure
- Validation rules
- Serialization rules
- Import contracts
- Runtime world contracts
- Logging contracts
- Event contracts
- Configuration contracts

## Why this matters

A real-time editor requires the same rules as the server. If the editor writes data differently than the server reads it, the project will become unstable.

By making Core the shared foundation, every app uses the same definitions.

## Initial folders

```text
Core/Models/
Core/Validation/
Core/Importing/
Core/Runtime/
Core/Serialization/
Core/Logging/
Core/Events/
Core/Configuration/
```

## Next implementation step

Code Update 0002 should begin the actual Core implementation by adding models for:

- Database manifest
- Room
- Item
- Monster
- Spell
- Shop
- Race
- Class
- Text block/message

Then the Server app can load those models from `Data/`.
