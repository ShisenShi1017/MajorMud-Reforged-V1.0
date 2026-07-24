#!/usr/bin/env python3
from pathlib import Path
import json,sys
p=Path("Librarium/Evidence/native_support_consumers_0023.json")
d=json.loads(p.read_text(encoding="utf-8"))
assert d['evidence_id']=='native-support-consumers-0023'
assert d['sto']['count']==7
assert d['hse']['count']==len(d['hse']['records'])
assert all(r['fixed_suffix']==15 for r in d['hse']['records'])
assert any(r['asset'].lower()=='wccaddon.sys' for r in d['native_static_references'])
print('native support consumer evidence valid')
