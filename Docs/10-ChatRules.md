# Chat and Development Rules

- The project is MajorMud Reforged.
- Code changes are patches.
- Converted/imported game-data releases are database versions.
- Every patch or database version must run from its own extracted folder using an `Apply-*.bat` file.
- The apply script must auto-locate the project folder when possible and must not require manual path entry.
- Never overwrite `Saves/`.
- Never overwrite original `ImportedDatabases/` source files.
- The Reforged server must eventually run from editable `Data/` only.
- Imported/original MajorMud files are reference/fallback after conversion.
- Every patch/database version must update documentation.
- Never claim full DAT decoding until verified.

