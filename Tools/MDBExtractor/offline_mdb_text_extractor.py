#!/usr/bin/env python3
"""
Evidence-oriented offline MDB extractor for Nightmare Redux ability.mdb.

This is not a general Access/Jet replacement. It is a local parser for the
readable Jet data pages used by Nightmare Redux's ability.mdb. It never uses
ODBC, ACE, DAO, mdbtools, or the original MDB at runtime; it emits static CSV/JSON
evidence for editor reverse engineering.
"""
from __future__ import annotations
import argparse, csv, json, re
from pathlib import Path

PAGE_SIZE = 4096
TEXT_RE = re.compile(rb"[\x20-\x7e]{3,}")


def printable_segments(blob: bytes):
    return [m.group(0).decode('latin-1', errors='replace') for m in TEXT_RE.finditer(blob)]


def parse_data_page(page: bytes, page_no: int):
    if len(page) != PAGE_SIZE or page[0] != 0x01:
        return []
    owner_table_page = int.from_bytes(page[4:8], 'little')
    row_count = int.from_bytes(page[12:14], 'little')
    rows = []
    offsets = []
    for i in range(row_count):
        raw = int.from_bytes(page[14 + 2*i:16 + 2*i], 'little')
        off = raw & 0x0FFF
        if 0 < off < PAGE_SIZE:
            offsets.append((off, raw))
    for idx, (off, raw_ptr) in enumerate(offsets):
        end = offsets[idx-1][0] if idx > 0 else PAGE_SIZE
        if end <= off:
            continue
        blob = page[off:end]
        rows.append({
            'page': page_no,
            'row_index_on_page': idx,
            'offset': off,
            'row_pointer_raw': raw_ptr,
            'row_pointer_flags_hex': hex(raw_ptr & 0xF000),
            'length': len(blob),
            'owner_table_page': owner_table_page,
            'hex_prefix': blob[:32].hex(),
            'hex_suffix': blob[-16:].hex(),
            'segments': printable_segments(blob),
            'blob': blob,
        })
    return rows


def parse_ability_row(row):
    blob = row['blob']
    first_marker = blob.find(b'\xff\xfe')
    if first_marker < 0:
        return None
    # Ability rows observed in Nightmare Redux begin with a 6 byte fixed prefix.
    ability_id = int.from_bytes(blob[2:4], 'little') if len(blob) >= 4 else None
    numeric_prefix = blob[:first_marker]
    embedded_name = ''
    if len(numeric_prefix) > 12:
        candidate = numeric_prefix[12:].replace(b'\x00', b'').decode('latin-1', errors='replace').strip()
        if candidate and all(32 <= ord(ch) < 127 for ch in candidate):
            embedded_name = candidate
            numeric_prefix = numeric_prefix[:12]
    text_area = blob[first_marker:]
    parts = []
    for part in text_area.split(b'\xff\xfe'):
        part = part.strip(b'\x00')
        if not part:
            continue
        # Strip the observed Access variable-column trailer:
        # <description_length> 00 <name_length> 00 0c 00 02 00 3f
        # The removed bytes remain auditable in hex_suffix/source_length.
        if len(part) >= 9 and part[-8] == 0x00 and part[-5:] == b'\x0c\x00\x02\x00?':
            part = part[:-9]
        txt = part.decode('latin-1', errors='replace')
        txt = re.sub(r'[\x00-\x1f]+$', '', txt)
        if txt:
            parts.append(txt)
    if embedded_name:
        name = embedded_name
        description = parts[0] if parts else ''
    else:
        name = parts[0] if parts else ''
        description = parts[1] if len(parts) > 1 else ''
    return {
        'ability_id': ability_id,
        'name': name,
        'description': description,
        'text_part_count': len(parts),
        'numeric_prefix_hex': numeric_prefix.hex(),
        'source_page': row['page'],
        'source_offset': row['offset'],
        'source_length': row['length'],
        'row_pointer_flags_hex': row['row_pointer_flags_hex'],
        'record_status': 'base/active-candidate' if row['row_pointer_raw'] < 0x4000 else 'flagged/supplemental-candidate',
        'confidence': 'High for id/name/description extraction; Medium for unidentified numeric prefix bytes',
    }


def build_owner_summary(rows):
    owners = {}
    for row, raw in rows:
        owner = raw['owner_table_page']
        info = owners.setdefault(owner, {
            'owner_table_page': owner,
            'row_count': 0,
            'pages': set(),
            'sample_strings': [],
            'min_row_length': None,
            'max_row_length': 0,
        })
        info['row_count'] += 1
        info['pages'].add(raw['page'])
        length = raw['length']
        info['min_row_length'] = length if info['min_row_length'] is None else min(info['min_row_length'], length)
        info['max_row_length'] = max(info['max_row_length'], length)
        for segment in raw['segments']:
            if segment not in info['sample_strings']:
                info['sample_strings'].append(segment)
            if len(info['sample_strings']) >= 12:
                break

    summary = []
    for info in owners.values():
        page_numbers = sorted(info.pop('pages'))
        info['page_count'] = len(page_numbers)
        info['first_pages'] = page_numbers[:20]
        summary.append(info)
    summary.sort(key=lambda r: (-r['row_count'], r['owner_table_page']))
    return summary


def build_owner_rows(rows, selected_owners):
    selected = set(selected_owners)
    exported = []
    for _row, raw in rows:
        owner = raw['owner_table_page']
        if owner not in selected:
            continue
        exported.append({
            'owner_table_page': owner,
            'page': raw['page'],
            'row_index_on_page': raw['row_index_on_page'],
            'offset': raw['offset'],
            'length': raw['length'],
            'hex_prefix': raw['hex_prefix'],
            'hex_suffix': raw['hex_suffix'],
            'segments': raw['segments'],
            'record_status': 'raw-mdb-row',
            'confidence': 'High for raw MDB row location and printable segments; field semantics require a table-specific decoder before canonical promotion.',
        })
    exported.sort(key=lambda r: (r['owner_table_page'], r['page'], r['row_index_on_page'], r['offset']))
    return exported


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('mdb')
    ap.add_argument('--outdir', default='extracted')
    ap.add_argument(
        '--owner-export',
        default='1664,322,2566,15,1660',
        help='Comma-separated owner_table_page ids to export as raw stock rows.',
    )
    args = ap.parse_args()
    path = Path(args.mdb)
    out = Path(args.outdir); out.mkdir(parents=True, exist_ok=True)
    data = path.read_bytes()
    pages = []
    all_rows = []
    for page_no in range(len(data)//PAGE_SIZE):
        page = data[page_no*PAGE_SIZE:(page_no+1)*PAGE_SIZE]
        segs = printable_segments(page)
        page_info = {
            'page': page_no,
            'type': page[0],
            'owner_table_page': int.from_bytes(page[4:8], 'little') if len(page) >= 8 else None,
            'row_count': int.from_bytes(page[12:14], 'little') if page[0] == 1 and len(page) >= 14 else None,
            'sample_strings': segs[:10],
        }
        pages.append(page_info)
        for row in parse_data_page(page, page_no):
            raw = dict(row); raw.pop('blob')
            raw['segments'] = raw['segments'][:10]
            all_rows.append((row, raw))
    selected_owners = [int(v.strip()) for v in args.owner_export.split(',') if v.strip()]
    owner_rows = build_owner_rows(all_rows, selected_owners)
    ability_rows = []
    for row, raw in all_rows:
        # Table page 22 is the readable ability/attribute record store in this MDB.
        if row['owner_table_page'] == 22:
            parsed = parse_ability_row(row)
            if parsed and parsed['name'] and row['blob'][:1] == b'\x06':
                ability_rows.append(parsed)
    ability_rows.sort(key=lambda r: (r['ability_id'] is None, r['ability_id'], r['source_page'], r['source_offset']))
    owner_summary = build_owner_summary(all_rows)

    (out/'mdb_page_inventory.json').write_text(json.dumps(pages, indent=2), encoding='utf-8')
    (out/'raw_data_rows.json').write_text(json.dumps([r for _, r in all_rows], indent=2), encoding='utf-8')
    (out/'owner_table_summary.json').write_text(json.dumps(owner_summary, indent=2), encoding='utf-8')
    (out/'owner_table_rows.json').write_text(json.dumps(owner_rows, indent=2), encoding='utf-8')
    with (out/'owner_table_summary.csv').open('w', newline='', encoding='utf-8') as f:
        fields = ['owner_table_page','row_count','page_count','min_row_length','max_row_length','first_pages','sample_strings']
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for row in owner_summary:
            out_row = dict(row)
            out_row['first_pages'] = '|'.join(str(v) for v in out_row['first_pages'])
            out_row['sample_strings'] = '|'.join(out_row['sample_strings'])
            w.writerow(out_row)
    (out/'ability_rows.json').write_text(json.dumps(ability_rows, indent=2), encoding='utf-8')
    with (out/'ability_rows.csv').open('w', newline='', encoding='utf-8') as f:
        fields = ['ability_id','name','description','text_part_count','numeric_prefix_hex','source_page','source_offset','source_length','row_pointer_flags_hex','record_status','confidence']
        w=csv.DictWriter(f, fieldnames=fields); w.writeheader(); w.writerows(ability_rows)
    print(json.dumps({'mdb': str(path), 'pages': len(pages), 'raw_rows': len(all_rows), 'ability_rows': len(ability_rows)}, indent=2))

if __name__ == '__main__':
    main()
