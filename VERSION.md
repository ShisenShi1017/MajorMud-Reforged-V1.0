# MajorMud Reforged Version Status

- Current Code Update: 0002 - Repository Deduplication
- Current Database Version: 0009 - Server-Ready Data Baseline
- Current Milestone: Standalone Server Foundation
- Current Source of Truth: latest user-uploaded repository ZIP after Code Update 0001

## Active Direction

The project is moving away from PowerShell prototypes toward a standalone application platform with:

- `Core/` as the shared model/validation/runtime foundation
- `Apps/Server/` as the first real application target
- `Apps/Importer/` for repeatable database conversion and verification
- `Apps/Editor/` for future real-time editing through the server
- `Apps/Launcher/` for future update/app management

No `.DAT`, MDB, ACCDB, or MegaMud files are used by the server at runtime. They remain source/reference inputs only.
