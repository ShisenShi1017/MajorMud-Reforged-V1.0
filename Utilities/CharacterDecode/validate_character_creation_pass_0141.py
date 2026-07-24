#!/usr/bin/env python3
from __future__ import annotations
import json, sys
from pathlib import Path
root=Path(sys.argv[1] if len(sys.argv)>1 else '.')
p=root/'Data/NativeDecodeReports/CodeUpdate0141CharacterCreation/character_creation_contract_0141.json'
d=json.loads(p.read_text())
assert d['counts']=={'races':13,'classes':15,'race_class_pairs':195}
assert d['validation']['pair_matrix_exact'] is True
assert len(d['allowed_pairs'])==195
assert d['initial_state']['start']['room_id']=='room.map1.2140'
assert d['initial_state']['gold']==0 and d['initial_state']['inventory']==[] and d['initial_state']['spells']==[]
for r in d['race_constraints']:
    assert set(r['minimum_stats'])=={'int','wil','str','hea','agl','cha'}
    assert all(r['minimum_stats'][k] <= r['maximum_stats'][k] for k in r['minimum_stats'])
for c in d['class_constraints']:
    assert c['min_hits'] >= 0 and c['max_hits_bonus'] >= 0
print('PASS character creation 0141')
