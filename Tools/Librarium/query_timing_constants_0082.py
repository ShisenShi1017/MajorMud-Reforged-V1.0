#!/usr/bin/env python3
import json, pathlib
root = pathlib.Path(__file__).resolve().parents[2]
path = root / 'Librarium' / 'TimingConstants0082' / 'timing_constant_claims_0082.json'
data = json.loads(path.read_text(encoding='utf-8'))
for c in data['claims']:
    print(f"{c['id']}: {c['status_after_0082']} - {c['claim']}")
