#!/usr/bin/env python3
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Data/NativeDecodeReports/CodeUpdate0161CharacterDeath/character_death_contract_0161.json'
contract = json.loads(path.read_text())
assert contract['classification'] == 'STRONGLY CORRELATED'
assert contract['behavior']['repeat_recovery'].startswith('fails closed')
assert 'resurrection spell ordering and penalties' in contract['unknown']
print(json.dumps({'code_update': '0161', 'status': 'preserved', 'contract': str(path.relative_to(ROOT))}, indent=2))
