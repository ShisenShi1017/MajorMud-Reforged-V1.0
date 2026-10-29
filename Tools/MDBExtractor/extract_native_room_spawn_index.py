#!/usr/bin/env python3
"""Read-only extraction of Classic WCC native room generation rules.

Reads original wccmp002.dat fixed 4096-byte data pages. Uses the field offsets
independently verified against WCCMMUD.DLL (_generate_monster and its caller).
Generates a compact, reproducible Brotli/base64 snapshot for the Librarium.
No modification of the original game installation or input file.
"""
from __future__ import annotations

import argparse
import base64
import brotli
import hashlib
from pathlib import Path
import struct

PAGE_SIZE = 4096
ROOM_HEADER = b'\x00D'
SOURCE_SHA256 = '06eec04e1020ded5f63d6a9e67a794b871696ca90f6afc0fcdfcccdd4ac657be'
# Per-page record begins at byte 8. Offsets below are native room-struct offsets.
ROOM_OFFSET = 8
ROOM_GROUP = 0x560
INDEX_MIN = 0x462
INDEX_MAX = 0x464
DIRECT_NPC = 0x468
AREA_CAP = 0x5BE
LINKED_AREA_ROOM = 0x5C4


def extract(path: Path) -> tuple[str, dict[str, int]]:
    blob = path.read_bytes()
    if len(blob) % PAGE_SIZE:
        raise ValueError('Native file size must be divisible by 4096')
    actual_sha = hashlib.sha256(blob).hexdigest()
    if SOURCE_SHA256 != 'PLACEHOLDER' and actual_sha != SOURCE_SHA256:
        raise ValueError('Unexpected native database SHA-256; refusing to project uncertain data')

    observed = {}
    duplicate_count = 0
    for offset in range(0, len(blob), PAGE_SIZE):
        page = memoryview(blob)[offset:offset + PAGE_SIZE]
        if page[:2] != ROOM_HEADER:
            continue
        map_id, room_number = struct.unpack_from('<II', page, ROOM_OFFSET)
        if map_id <= 0 or map_id > 64 or room_number <= 0 or room_number > 65535:
            continue
        group = struct.unpack_from('<I', page, ROOM_OFFSET + ROOM_GROUP)[0]
        lower, upper = struct.unpack_from('<HH', page, ROOM_OFFSET + INDEX_MIN)
        direct = struct.unpack_from('<H', page, ROOM_OFFSET + DIRECT_NPC)[0]
        cap = struct.unpack_from('<H', page, ROOM_OFFSET + AREA_CAP)[0]
        linked = struct.unpack_from('<I', page, ROOM_OFFSET + LINKED_AREA_ROOM)[0]
        if lower > upper or group > 65535:
            raise ValueError(f'Invalid native group/index at map {map_id}, room {room_number}')
        value = (group, lower, upper, direct, cap, linked)
        key = (map_id, room_number)
        if key in observed:
            duplicate_count += 1
            if observed[key] != value:
                # An area controller's mutable live population count is not part of
                # this snapshot. Static fields must match across duplicate pages.
                raise ValueError(f'Conflicting native room generation rules: {key}: {observed[key]} vs {value}')
            continue
        observed[key] = value

    rows = [':'.join(str(v) for v in (*key, *observed[key]))
            for key in sorted(observed)]
    plain = ('\n'.join(rows) + '\n').encode('ascii')
    payload = base64.b64encode(brotli.compress(plain, quality=11)).decode('ascii')
    metrics = {'source_pages': len(blob) // PAGE_SIZE,
               'unique_rooms': len(observed),
               'duplicate_pages': duplicate_count,
               'raw_bytes': len(plain),
               'compressed_base64_bytes': len(payload),
               'source_sha256': actual_sha,
               'rows_sha256': hashlib.sha256(plain).hexdigest()}
    return payload, metrics


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument('native_database', type=Path)
    p.add_argument('output', type=Path)
    args = p.parse_args()
    payload, metrics = extract(args.native_database)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(payload + '\n', encoding='ascii')
    for key, value in metrics.items():
        print(f'{key}={value}')


if __name__ == '__main__':
    main()