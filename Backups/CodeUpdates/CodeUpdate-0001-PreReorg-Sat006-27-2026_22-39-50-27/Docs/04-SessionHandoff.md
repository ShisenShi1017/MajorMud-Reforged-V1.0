# Session Handoff

## Current focus

Transitioning MajorMud Reforged from PowerShell prototype layout to a cleaner standalone app platform.

## Latest update

CodePatch 0002 - Clean Repository / App Platform Foundation

## Important rules

- Updates run from their own extracted folder.
- Root README.txt is updated every update.
- Docs are updated every update.
- Saves and ImportedDatabases are protected.
- User should not need to install .NET SDK or developer tooling for official builds.
- Future executables must live in their own folders under `Bin/Windows/<AppName>/`.

## Next recommended work

Start replacing script-based behavior with standalone app components, beginning with shared Core contracts and server data loading.

## Caution

Do not claim executable files exist until they are actually produced and placed in their own executable folder.
