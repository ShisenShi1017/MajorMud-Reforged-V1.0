#!/usr/bin/env python3
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
data = root / 'Data' / 'Classic'
npcs = json.loads((data / 'npcs.json').read_text(encoding='utf-8'))['records']
spawns = json.loads((data / 'spawns.json').read_text(encoding='utf-8'))['records']
rooms = json.loads((data / 'rooms.json').read_text(encoding='utf-8'))['records']

npc_by_legacy = {int(n['legacy_id']): n for n in npcs if str(n.get('legacy_id', '')).isdigit() and int(n['legacy_id']) > 0}
room_ids = {r.get('id') for r in rooms}
resolved = 0
unresolved = 0
missing_rooms = []
name_mismatches = []
for spawn in spawns:
    room_id = spawn.get('room_ref')
    if room_id not in room_ids:
        missing_rooms.append((spawn.get('id'), room_id))
    raw = spawn.get('spawn_model', {}).get('monster_type')
    try:
        legacy_id = int(raw)
    except (TypeError, ValueError):
        legacy_id = 0
    npc = npc_by_legacy.get(legacy_id)
    if npc is None:
        unresolved += 1
        continue
    resolved += 1
    if str(npc.get('name', '')).casefold() != str(spawn.get('npc_name', '')).casefold():
        name_mismatches.append((spawn.get('id'), legacy_id, spawn.get('npc_name'), npc.get('name')))

assert npcs, 'No generated NPC definitions.'
assert spawns, 'No generated NPC spawns.'
assert not missing_rooms, f'Missing spawn rooms: {missing_rooms[:10]}'
assert not name_mismatches, f'NPC name mismatches: {name_mismatches[:10]}'
print(f'NPC definitions: {len(npc_by_legacy)}')
print(f'Spawns: {len(spawns)}')
print(f'Resolved spawns: {resolved}')
print(f'Unresolved spawns retained without guessing: {unresolved}')
print(f'Spawn rooms: {len({s.get("room_ref") for s in spawns})}')
print('CodeUpdate0134 NPC loading validation: PASS')
