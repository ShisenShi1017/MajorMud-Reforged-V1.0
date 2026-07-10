# CodeUpdate0134 — Map-Qualified NPC Population Evidence

## Finding

Room numbers are not globally unique. MMExplorer screenshots independently show room number `1457` on Maps 1, 2, 6, 7, 8, 12, 15, 16, and 17, with different names and layouts.

The authoritative RuntimePrep room-population matrix preserves `map_id`, `room_number`, and `linked_spawn_room`. The later `NpcSpawnEvidenceExtract.csv` omitted `map_id`, creating a lossy intermediate representation.

## Verified identity rules

- Primary spawn room identity: `(map_id, room_number)`.
- Linked population identity: `(map_id, linked_spawn_room)`.
- `linked_spawn_room` is a same-map shared population-area key.
- The linked room does not need an independent spawn record.
- A room number must never be resolved globally.

## CodeUpdate0134 correction

All 962 Librarium spawn records were matched one-to-one against `RoomPopulationMatrix_RuntimePrep0002.csv` using room name and population fields. The match was unique for every record.

Generated `Data/Classic/spawns.json` now carries the same map-qualified identities. All 748 nonzero linked population records have `linked_spawn_map_id` and `linked_population_key`.

The runtime attaches a spawn only when a generated room matches both map and room number. Spawn definitions for maps not present in the current room slice remain valid and loaded, but are not incorrectly attached to a same-numbered Map 1 room.

## Status

- Missing map context: 0
- Ambiguous linked population keys: 0
- Unresolved monster groups: 0
- Unresolved boss definitions: 0
- Gameplay values changed: none
