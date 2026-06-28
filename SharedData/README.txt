Shared data plan: the live Data folder at the project root is the shared source of truth. HostEditor-PC, Client-PC, and Client-Android should read/write these same JSON files.

Data/world.json: rooms, exits, races, classes, items, monsters, NPCs.
Data/leveling.json: max level, points per level, training costs.
Data/skills.json: skills and gameplay hooks.
Data/spells.json: starter spell definitions.
Data/derived_stats.json: formulas/notes for secondary stats.
