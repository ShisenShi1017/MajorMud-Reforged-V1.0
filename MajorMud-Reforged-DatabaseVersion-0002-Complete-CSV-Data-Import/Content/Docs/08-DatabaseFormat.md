# Database Format

MajorMud Reforged uses `Data/` as the working standalone database.

Database Version 0002 stores each converted record as one JSON file:

```text
Data/Items/item_0298_scroll-of-shockshield.json
Data/Rooms/map_001_room_00001_town-gates.json
Data/Monsters/monster_0001_giant-rat.json
```

Each file contains:

- `databaseVersion`
- `sourceTable`
- `primaryKey`
- `displayName`
- `fields`

All CSV column names are preserved inside `fields` so unknown or not-yet-understood fields are not lost.
