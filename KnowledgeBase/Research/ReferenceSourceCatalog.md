# Reference Source Catalog

This catalog records external and historical resources that may help verify Reforged data, gameplay behavior, world topology, importer decoding, and future feature design.

## Source Policy

- Reforged `Data/` remains the canonical runtime database.
- `ImportedDatabases/` remains archive/reference only.
- External sources are evidence for audits, not automatic overwrite sources.
- Proposed corrections should record source field, current Reforged field, difference, confidence, and approval status.
- Raw third-party binaries or archives should not be committed unless licensing and storage policy are explicitly approved.

## Source Hierarchy

| Priority | Source | Status | Use |
|---:|---|---|---|
| 1 | Original WSGSERVE `.dat` files | Desired / pending acquisition | Highest-confidence runtime-derived reference when available. |
| 2 | Original WCC data | Desired / partial historical context | Primary original-world source. |
| 3 | LLT XML databases | Uploaded/analyzed in chat | Verify class and race definitions from `ldbclass.xml` and `ldbrace.xml`. |
| 4 | MegaMMUD v2.0.3 stock database | Uploaded/analyzed in chat | Verify classes, races, rooms, items, monsters, shops, spells, and path metadata. |
| 5 | MegaMUD `.mp` path files | Uploaded/analyzed in chat | Verify routes, named locations, scripted travel, and navigation expectations. |
| 6 | Mud Hex documentation | Uploaded/analyzed in chat | Validate binary layouts and importer field mappings. |
| 7 | OmniVerse | Documented in CodeUpdate-0034 | Read-only comparison source for WCC-derived public reference data. |
| 8 | MudInfo maps/resources | User-provided URLs / partial access | Verify stock maps, navigation, and historical community knowledge. |
| 9 | MudCentral | Research candidate | Historical MajorMUD community site, pending accessible archive or saved pages. |
| 10 | r/MajorMUD | Research candidate | Community discussion source, pending direct accessible content. |

## Reference Categories

### Data Verification

- Items
- Monsters
- Spells
- Classes
- Races
- Shops
- Rooms
- Trainers
- Quests
- Gameplay flags

### World Verification

- Room graph
- Exits
- Secret paths
- Travel commands
- Boats/skiffs/portals
- Landmark routes
- Reachability tests

### Reverse Engineering

- Binary record layouts
- Field names
- Unknown fields
- Importer validation
- Save/user layout research

### Gameplay Behavior

- Quest flags
- Enhancement flags
- Limited items
- Ganghouses
- Combat pacing
- NPC/monster behavior
- Loot/drop policy

## Known Uploaded Reference Sets

- `lltxpcal-v102.zip`: LLT XP calculator with XML class/race data.
- `MegaMMUD v2.0.3 (Stock).zip`: MegaMUD stock database and path set.
- Mud Hex HTML pages: class, item, monster, room, user, shop, and spell layout references.
- User-provided quest/enhancement flag notes for MajorMUD 1.11p.

## Future Work

- Obtain and inspect original WCC `default.rar` if available.
- Obtain and preserve original WSGSERVE `.dat` files if available.
- Build structured research documents for each source.
- Add confidence ratings per field category.
- Build comparison reports instead of importing blindly.
