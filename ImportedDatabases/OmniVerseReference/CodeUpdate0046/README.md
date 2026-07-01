# CodeUpdate0046 OmniVerse/MudInfo Reference Capture

This folder contains reference-only validation/correlation evidence gathered during CodeUpdate-0046.

## Contents

- `OmniVerseCodexApi/wcc/` - WCC OmniVerse API JSON captures for abilities, classes, items, monsters, races, shops, spells, world status, roommaps, and room details.
- `MudInfoMaps/SourceArchive/maps.rar` - preserved MudInfo maps archive.
- `MudInfoMaps/Extracted/` - extracted MudInfo map HTML/GIF files.
- `ReferenceCaptureManifest.json` - capture manifest and summary counts.
- `ReferenceCaptureInventory.csv` - per-file inventory and SHA-256 hashes for captured payload files.

## Verification Counts

- Repository folder total: 1,532 files, including this README, manifest, and inventory.
- Captured payload inventory: 1,529 files.
- Room detail files: 1,246.
- Roommap files: 168.
- Extracted MudInfo map files: 102.

## Policy

Original MajorMUD binary files remain authoritative.

These files are evidence for validation and correlation only. Do not import fields, rewrite canonical `Data/`, or assume OmniVerse public IDs equal binary row indexes without a reviewed binary-backed reconciliation update.
