#!/usr/bin/env python3
"""
Promote directly mapped MMExplorer MDB room rows into canonical Reforged Data.

This intentionally promotes only fields proven by the raw MDB row surface:
map id, room number, room name, and printable map/room edge references.
Other room fields remain conservative defaults until table-specific bytes are
decoded.
"""
from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path


EDGE_RE = re.compile(r'(?P<map>\d+)/(?P<room>\d+)(?P<suffix>[A-Za-z0]*)')


def parse_hex_prefix(prefix: str):
    raw = bytes.fromhex(prefix)
    if len(raw) < 8:
        return None
    map_id = int.from_bytes(raw[2:4], 'little')
    room_number = int.from_bytes(raw[6:8], 'little')
    if map_id <= 0 or room_number <= 0:
        return None
    return map_id, room_number


def normalize_room_number(raw_room: str, suffix: str) -> int:
    digits = raw_room
    if suffix and suffix.isalpha():
        return int(digits)
    if len(digits) > 4 and digits.endswith('0'):
        return int(digits[:-1])
    return int(digits)


def parse_edges(segments):
    edges = []
    for segment in segments[1:]:
        match = EDGE_RE.search(segment)
        if not match:
            continue
        target_map = int(match.group('map'))
        target_room = normalize_room_number(match.group('room'), match.group('suffix'))
        edges.append({
            'map': target_map,
            'room': target_room,
            'raw': segment,
        })
    return edges


def load_mdb_rooms(owner_rows_path: Path):
    rows = json.loads(owner_rows_path.read_text(encoding='utf-8'))
    rooms = {}
    for row in rows:
        if row.get('owner_table_page') != 1664:
            continue
        segments = row.get('segments') or []
        identity = parse_hex_prefix(row.get('hex_prefix', ''))
        if not identity or not segments:
            continue
        map_id, room_number = identity
        room_id = f'wcc_map{map_id}_room{room_number}'
        rooms[room_id] = {
            'id': room_id,
            'sourceId': {
                'map': map_id,
                'room': room_number,
            },
            'mapId': map_id,
            'roomNumber': room_number,
            'name': segments[0],
            'description': '',
            'descriptionLines': [],
            'exits': [
                {
                    'dir': f'MDB{index + 1}',
                    'to_room': edge['room'],
                    'to_map': edge['map'],
                    'exit_type': -1,
                    'exitTypeKnown': False,
                    'raw': edge['raw'],
                }
                for index, edge in enumerate(parse_edges(segments))
            ],
            'rawExitFields': {
                'mdb_segments': segments[1:],
            },
            'roomType': 0,
            'shopNumber': 0,
            'lightLevel': 0,
            'gangHouse': 0,
            'roomItems': [],
            'hiddenItemIds': [],
            'groundCurrency': {
                'copper': 0,
                'silver': 0,
                'gold': 0,
                'platinum': 0,
                'runic': 0,
            },
            'spawn': {
                'monsterType': 0,
                'respawnMinutes': 0,
                'maxAreaCap': 0,
                'linkedSpawnRoom': 0,
                'bossTypeId': 0,
            },
            'behavior': {
                'attributes': 0,
                'deathRoomId': 0,
            },
            'source': {
                'kind': 'ImportedDatabases/data-v1.11n.mdb owner_table_page 1664',
                'file': 'Data/StockGameRaw/MMExplorerMDB/owner_table_rows.json',
                'promotedBy': 'CodeUpdate-0065',
                'ownerTablePage': row['owner_table_page'],
                'page': row['page'],
                'rowIndexOnPage': row['row_index_on_page'],
                'offset': row['offset'],
                'confidence': 'High for map, room, name, and raw printable edge references; other room fields remain conservative defaults pending byte-level field decode.',
            },
        }
    return rooms


def sort_room(room):
    return room.get('mapId', 0), room.get('roomNumber', 0), room.get('id', '')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--owner-rows', default='Data/StockGameRaw/MMExplorerMDB/owner_table_rows.json')
    parser.add_argument('--rooms', default='Data/rooms.json')
    parser.add_argument('--world', default='Data/world.json')
    parser.add_argument('--report', default='Reports/CodeUpdates/CodeUpdate-0065/MMExplorerMdbCanonicalRoomPromotion.csv')
    parser.add_argument('--ids', default='wcc_map1_room2150,wcc_map1_room2156')
    args = parser.parse_args()

    owner_rows_path = Path(args.owner_rows)
    rooms_path = Path(args.rooms)
    world_path = Path(args.world)
    report_path = Path(args.report)
    report_path.parent.mkdir(parents=True, exist_ok=True)

    mdb_rooms = load_mdb_rooms(owner_rows_path)
    selected_ids = [value.strip() for value in args.ids.split(',') if value.strip()]
    canonical_rooms = json.loads(rooms_path.read_text(encoding='utf-8'))
    world = json.loads(world_path.read_text(encoding='utf-8'))
    canonical_index = {room['id']: index for index, room in enumerate(canonical_rooms)}
    world_ids = set(world.get('rooms', []))

    report_rows = []
    for room_id in selected_ids:
        mdb_room = mdb_rooms.get(room_id)
        if mdb_room is None:
            report_rows.append({
                'room_id': room_id,
                'action': 'blocked',
                'reason': 'room id not found in MMExplorer MDB owner table 1664 export',
            })
            continue
        if room_id not in canonical_index:
            canonical_rooms.append(mdb_room)
            canonical_index[room_id] = len(canonical_rooms) - 1
            action = 'added_canonical_room'
        else:
            action = 'already_present'
            existing_source = canonical_rooms[canonical_index[room_id]].get('source', {})
            if existing_source.get('promotedBy') == 'CodeUpdate-0065':
                canonical_rooms[canonical_index[room_id]] = mdb_room
                action = 'refreshed_canonical_room'
        if room_id not in world_ids:
            world['rooms'].append(room_id)
            world_ids.add(room_id)
            world_action = 'added_world_room'
        else:
            world_action = 'already_present'
        report_rows.append({
            'room_id': room_id,
            'action': action,
            'world_action': world_action,
            'map': mdb_room['mapId'],
            'room': mdb_room['roomNumber'],
            'name': mdb_room['name'],
            'exit_count': len(mdb_room['exits']),
            'source': mdb_room['source']['kind'],
        })

    canonical_rooms.sort(key=sort_room)
    world['rooms'] = sorted(world['rooms'], key=lambda item: (
        int(item.split('_map', 1)[1].split('_room', 1)[0]),
        int(item.rsplit('room', 1)[1]),
    ))
    world['version'] = '0065-mmexplorer-mdb-room-repair'

    rooms_path.write_text(json.dumps(canonical_rooms, indent=2) + '\n', encoding='utf-8')
    world_path.write_text(json.dumps(world, indent=2) + '\n', encoding='utf-8')

    fields = ['room_id', 'action', 'world_action', 'map', 'room', 'name', 'exit_count', 'source', 'reason']
    with report_path.open('w', newline='', encoding='utf-8') as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(report_rows)

    print(json.dumps({
        'selected': selected_ids,
        'report': str(report_path),
        'canonicalRoomCount': len(canonical_rooms),
        'worldRoomCount': len(world['rooms']),
        'actions': report_rows,
    }, indent=2))


if __name__ == '__main__':
    main()
