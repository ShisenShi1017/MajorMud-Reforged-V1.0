# Session Handoff

## Current Focus

Building MajorMud Reforged as a standalone server using editable `Data/` files as the live database.

## Last Completed Work

Database Version 0002 was created from the user-provided CSV export archive. It converts every provided CSV table into editable JSON with zero duplicate primary keys detected in the converted input.

## Next Recommended Step

Begin server foundation work against the new `Data/` structure:

1. Add a server data loader.
2. Load `Data/_DatabaseVersionManifest.json`.
3. Load rooms/items/monsters/classes/races/spells/shops/lairs/textblocks from JSON.
4. Validate required IDs and references.
5. Keep `ImportedDatabases/` reference-only.
