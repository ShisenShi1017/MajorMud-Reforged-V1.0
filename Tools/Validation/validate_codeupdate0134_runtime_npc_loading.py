#!/usr/bin/env python3
import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

root = Path(__file__).resolve().parents[2]
data = root / 'Data' / 'Classic'
npcs = json.loads((data / 'npcs.json').read_text(encoding='utf-8'))['records']
spawns = json.loads((data / 'spawns.json').read_text(encoding='utf-8'))['records']
rooms = json.loads((data / 'rooms.json').read_text(encoding='utf-8'))['records']
matrix_path = root / 'Tools' / 'Simulator' / 'MajorMUD-Reforged-SimulatorPrototype' / 'Evidence' / 'RuntimePrep0002-WorldPopulationExtraction' / 'Payload' / 'RuntimePrep0002' / 'RoomPopulationMatrix_RuntimePrep0002.csv'

npc_by_legacy = {}
npcs_by_group = defaultdict(list)
for npc in npcs:
    legacy_id = int(npc.get('legacy_id') or 0)
    group_id = int(npc.get('behavior', {}).get('group_id') or 0)
    if legacy_id > 0:
        npc_by_legacy[legacy_id] = npc
    if group_id > 0:
        npcs_by_group[group_id].append(npc)

room_keys = {(int(room.get('map_id') or room.get('mapId') or 0), int(room.get('room_number') or room.get('roomNumber') or room.get('legacy_id') or 0)) for room in rooms}
unresolved_groups = []
unresolved_bosses = []
missing_identity = []
bad_link_keys = []
available_primary = []
unavailable_primary = []
linked_records = []
spawn_maps = Counter()

for spawn in spawns:
    model = spawn.get('spawn_model', {})
    map_id = int(spawn.get('map_id') or model.get('map_id') or 0)
    room_number = int(spawn.get('room_number') or 0)
    group_id = int(model.get('monster_type') or 0)
    boss_id = int(spawn.get('npc_legacy_id') or 0)
    linked_room = int(model.get('linked_spawn_room') or 0)
    linked_map = int(model.get('linked_spawn_map_id') or 0)
    linked_key = model.get('linked_population_key') or ''
    spawn_maps[map_id] += 1

    if map_id <= 0 or room_number <= 0:
        missing_identity.append(spawn.get('id'))
    elif (map_id, room_number) in room_keys:
        available_primary.append(spawn.get('id'))
    else:
        unavailable_primary.append(spawn.get('id'))

    if group_id > 0 and group_id not in npcs_by_group:
        unresolved_groups.append((spawn.get('id'), group_id))
    if boss_id > 0 and boss_id not in npc_by_legacy:
        unresolved_bosses.append((spawn.get('id'), boss_id))
    if linked_room > 0:
        linked_records.append(spawn.get('id'))
        expected = f'map{map_id}-room{linked_room}'
        if linked_map != map_id or linked_key != expected:
            bad_link_keys.append((spawn.get('id'), linked_map, linked_key, expected))

with matrix_path.open(newline='', encoding='utf-8-sig') as handle:
    matrix = list(csv.DictReader(handle))
expected_keys = {(int(row['map_id']), int(row['room_number']), int(row.get('linked_spawn_room') or 0)) for row in matrix}
actual_keys = {(int(s['map_id']), int(s['room_number']), int(s.get('spawn_model', {}).get('linked_spawn_room') or 0)) for s in spawns}

assert npcs, 'No generated NPC definitions.'
assert len(spawns) == 962, f'Expected 962 spawns, got {len(spawns)}.'
assert not missing_identity, f'Spawns missing map-qualified identity: {missing_identity[:10]}'
assert not bad_link_keys, f'Invalid linked population keys: {bad_link_keys[:10]}'
assert not unresolved_groups, f'Unresolved monster groups: {unresolved_groups[:10]}'
assert not unresolved_bosses, f'Unresolved boss NPC definitions: {unresolved_bosses[:10]}'
assert actual_keys.issubset(expected_keys), 'Generated spawn identities diverge from RuntimePrep room population evidence.'
assert len(linked_records) == 748, f'Expected 748 linked population records, got {len(linked_records)}.'
assert len(spawn_maps) > 1, 'Spawn data was not map-qualified.'

print(f'NPC definitions: {len(npc_by_legacy)}')
print(f'NPC groups: {len(npcs_by_group)}')
print(f'Spawns: {len(spawns)}')
print(f'Spawn maps: {dict(sorted(spawn_maps.items()))}')
print(f'Linked population records: {len(linked_records)}')
print('Linked population records missing map context: 0')
print(f'Primary spawn rooms available in current generated room slice: {len(available_primary)}')
print(f'Primary spawn rooms outside current generated room slice: {len(unavailable_primary)}')
print('CodeUpdate0134 complete map-qualified NPC population validation: PASS')
