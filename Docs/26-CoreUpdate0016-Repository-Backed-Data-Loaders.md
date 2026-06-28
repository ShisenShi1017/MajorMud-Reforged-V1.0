# Core Update 0016 - Repository-Backed Data Loaders

CodeUpdate-0016 continues Milestone 1 by adding dependency-free Core loaders for the current canonical `Data/` JSON categories.

## Added

- `CoreDataRepositoryLoader` loads supported `Data/` files into Core repositories.
- `CoreDataCatalog` exposes loaded repositories by category.
- `CanonicalDataRecord` preserves raw JSON fields while projecting common identity and name fields.
- `CoreDataRepositoryHealthCheck` verifies that current categories can be loaded through the Core repository layer.
- `.github/workflows/dotnet-build.yml` provides cloud build verification through GitHub Actions so the user does not need the .NET SDK locally.

## Supported Categories

- classes
- items
- loot_tables
- monsters
- quests
- races
- rooms
- shops
- trainers
- messages
- skills
- spells
- world_rooms

Empty placeholder categories such as quests, shops, trainers, loot tables, and standalone rooms remain allowed during Milestone 1.
