#!/usr/bin/env python3
import json
from pathlib import Path
R=Path(__file__).resolve().parents[2]
p=R/'Reports/NativeDecode/native_host_iat_layout_0007.json'; d=json.loads(p.read_text())
assert d['schema']=='majormud.native-host-iat-layout.v1'
assert len(d['modules'])==4
for m in d['modules']:
 assert m['pointer_width_bytes']==4 and m['contiguous_iat_run_count']==1
 r=m['iat_runs'][0]; assert r['slot_count']==m['import_count']
 assert int(r['end_va'],16)-int(r['start_va'],16)==4*(r['slot_count']-1)
 assert all(int(b['iat_va'],16)==int(r['start_va'],16)+4*i for i,b in enumerate(r['symbols']))
print('native host IAT layout 0007 validation passed')
