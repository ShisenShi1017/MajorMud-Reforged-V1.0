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
ROOM_SPELL = 0x600
ROOM_SPELL_SHA256 = '797237c16c294e082197adecd132696c675d1fbc9409cf3bd3ca7901d0bf4fca'
ROOM_LOCAL_CAP = 0x55C
CAP_ROWS_SHA256 = 'e45013fcca7350d9ff1561e17f780a1a116b53d58c345cb055b7fda1e760fe2b'


def extract_caps(path: Path) -> tuple[str, dict[str, int]]:
    blob = path.read_bytes()
    if hashlib.sha256(blob).hexdigest() != SOURCE_SHA256:
        raise ValueError('Original WCCMP002 hash mismatch')
    seen: dict[tuple[int, int], int] = {}
    for offset in range(0, len(blob), PAGE_SIZE):
        page = memoryview(blob)[offset:offset + PAGE_SIZE]
        if page[:2] != ROOM_HEADER:
            continue
        map_id, room_number = struct.unpack_from('<II', page, ROOM_OFFSET)
        if not (0 < map_id <= 64 and 0 < room_number <= 65535):
            continue
        cap = int(page[ROOM_OFFSET + ROOM_LOCAL_CAP])
        key = (map_id, room_number)
        if key in seen and seen[key] != cap:
            raise ValueError(f'Conflicting native room cap at {key}')
        seen[key] = cap
    raw = ''.join(f'{m}:{r}:{seen[(m, r)]}\n' for m, r in sorted(seen)).encode('ascii')
    actual = hashlib.sha256(raw).hexdigest()
    if actual != CAP_ROWS_SHA256 or len(seen) != 13360:
        raise ValueError('Native room cap source or checksum mismatch')
    return base64.b64encode(brotli.compress(raw, quality=11)).decode('ascii'), {
        'native_room_caps': len(seen), 'raw_sha256': actual
    }


def extract(path: Path) -> tuple[str, dict[str, int]]:
    blob = path.read_bytes()
    if len(blob) % PAGE_SIZE:
        raise ValueError('Native file size must be divisible by 4096')
    actual_sha = hashlib.sha256(blob).hexdigest()
    if SOURCE_SHA256 != 'PLACEHOLDER' and actual_sha != SOURCE_SHA256:
        raise ValueError('Unexpected native database SHA-256; refusing to project uncertain data')

    observed = {}
    observed_spells = {}
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
        room_spell = struct.unpack_from('<H', page, ROOM_OFFSET + ROOM_SPELL)[0]
        if lower > upper or group > 65535:
            raise ValueError(f'Invalid native group/index at map {map_id}, room {room_number}')
        value = (group, lower, upper, direct, cap, linked)
        key = (map_id, room_number)
        if key in observed:
            duplicate_count += 1
            if observed_spells[key] != room_spell:
                raise ValueError(f'Conflicting native room spell for {key}')
            if observed[key] != value:
                # An area controller's mutable live population count is not part of
                # this snapshot. Static fields must match across duplicate pages.
                raise ValueError(f'Conflicting native room generation rules: {key}: {observed[key]} vs {value}')
            continue
        observed[key] = value
        observed_spells[key] = room_spell

    rows = [':'.join(str(v) for v in (*key, *observed[key]))
            for key in sorted(observed)]
    plain = ('\n'.join(rows) + '\n').encode('ascii')
    payload = base64.b64encode(brotli.compress(plain, quality=11)).decode('ascii')
    # Second source section stores only positive room spell IDs.
    spell_rows = ''.join(f'{m}:{r}:{observed_spells[(m, r)]}\n'
                         for m, r in sorted(observed)
                         if observed_spells[(m, r)] > 0).encode('ascii')
    spell_sha = hashlib.sha256(spell_rows).hexdigest()
    if spell_sha != ROOM_SPELL_SHA256:
        raise ValueError('Native room spell evidence checksum mismatch')
    payload += '\n' + base64.b64encode(brotli.compress(spell_rows, quality=11)).decode('ascii')
    metrics = {'source_pages': len(blob) // PAGE_SIZE,
               'unique_rooms': len(observed),
               'duplicate_pages': duplicate_count,
               'raw_bytes': len(plain),
               'compressed_base64_bytes': len(payload),
               'source_sha256': actual_sha,
               'rows_sha256': hashlib.sha256(plain).hexdigest(),
               'room_spell_references': sum(v > 0 for v in observed_spells.values()),
               'room_spell_rows_sha256': spell_sha}
    return payload, metrics


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument('native_database', type=Path)
    p.add_argument('output', type=Path)
    p.add_argument('--caps-output', type=Path,
                   help='also export original +0x55C per-room generation caps')
    args = p.parse_args()
    payload, metrics = extract(args.native_database)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(payload + '\n', encoding='ascii')
    for key, value in metrics.items():
        print(f'{key}={value}')
    if args.caps_output is not None:
        caps_payload, caps_metrics = extract_caps(args.native_database)
        args.caps_output.parent.mkdir(parents=True, exist_ok=True)
        args.caps_output.write_text(caps_payload + '\n', encoding='ascii')
        for key, value in caps_metrics.items():
            print(f'{key}={value}')


if __name__ == '__main__':
    main()