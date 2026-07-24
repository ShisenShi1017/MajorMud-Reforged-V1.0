#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'Data/NativeDecodeReports/CodeUpdate0160CharacterDeath/character_death_contract_0160.json'
contract=json.loads(out.read_text())
assert contract['classification']=='STRONGLY CORRELATED'
assert contract['behavior']['defeat_order'][0]=='reject duplicate death transition'
assert 'native XP loss formula' in contract['unknown']
print(json.dumps({'code_update':'0160','status':'preserved','contract':str(out.relative_to(ROOT))},indent=2))
