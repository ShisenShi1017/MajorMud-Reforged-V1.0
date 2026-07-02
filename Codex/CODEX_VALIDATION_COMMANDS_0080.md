# Codex Validation Commands — Patch 0080

Run from the package root after extraction.

```bash
python3 - <<'PYVALIDATE'
import json, csv, pathlib
root = pathlib.Path('.')
assert (root/'Data').is_dir(), 'Missing recreated Data folder'
assert not list(root.rglob('*.zip')), 'Old ZIPs must not be bundled inside Patch 0080'
rooms = json.load(open(root/'Data/rooms.json'))
rg = json.load(open(root/'Data/room_graph.json'))
resolved = list(csv.DictReader(open(root/'Reports/room_exit_endpoint_resolved_0080.csv')))
remaining = list(csv.DictReader(open(root/'Reports/room_exit_endpoint_remaining_0080.csv')))
assert len(rooms) == 1246, len(rooms)
assert len(rg.get('edges', [])) == 10097, len(rg.get('edges', []))
assert len(resolved) == 1, len(resolved)
assert len(remaining) == 210, len(remaining)
assert resolved[0]['expected_endpoint_id_0080'] == 'wcc_map12_room3158'
print('Patch 0080 validation passed')
PYVALIDATE
```
