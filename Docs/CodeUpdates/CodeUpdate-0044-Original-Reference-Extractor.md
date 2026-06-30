# CodeUpdate-0044 - Original Reference Extractor

## Purpose

Add a safe reference-capture extractor for original/source material so future ChatGPT-assisted extraction work can start from verified inventories, hashes, strings, and source manifests instead of assumptions.

This update supports the user's direction to build the actual extractor now, while leaving the detailed data extraction and mapping work for a follow-up ChatGPT workflow.

## Scope

- Added `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1`.
- Added `Tools/ReferenceCapture/README.md`.
- Records CodeUpdate-0043 as green by user report.
- Updates continuity documents for CodeUpdate-0044.

## Tool Behavior

The capture tool:

- Accepts one or more source files, archives, or directories.
- Preserves source archives under `ImportedDatabases/ReferenceCaptures/<CaptureName>/SourceArchives/`.
- Optionally preserves source directories under `ImportedDatabases/ReferenceCaptures/<CaptureName>/SourceDirectories/`.
- Extracts `.zip` archives through built-in .NET support.
- Extracts `.rar` and `.7z` archives when 7-Zip is installed.
- Inventories files, sizes, timestamps, SHA-256 hashes, extensions, category hints, and printable strings.
- Writes reports under `Reports/ReferenceCaptures/<CaptureName>/`.

## Boundaries

- No canonical `Data/` rewrite.
- No imported/reference deletion.
- No original executable execution.
- No keygen use.
- No license bypass.
- No automatic WCC/WBB/WSGSERVE reconciliation.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No exact MajorMUD parity claim.

## Known Limitations

- The extractor captures files and printable strings but does not yet decode every proprietary binary record into structured Reforged data.
- `.rar` and `.7z` extraction requires 7-Zip, or the user must manually extract the archive into a folder and rerun the tool on that folder.
- Capture outputs are evidence only. A separate approved reconciliation update is required before any canonical `Data/` change.

## Handoff Guidance

Future extraction work should stay staged:

1. Capture original/reference material.
2. Decode and document proposed record mappings.
3. Compare against Reforged-native `Data/`.
4. Apply only reviewed, approved reconciliation changes.

This keeps original data research useful without letting reference material silently become live game data.
