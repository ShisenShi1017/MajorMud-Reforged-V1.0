#!/usr/bin/env python3
import json, sys
from collections import Counter
from pathlib import Path
root=Path(sys.argv[1] if len(sys.argv)>1 else '.')
def load(name): return json.loads((root/'Data'/'CompilerSmokeTest'/name).read_text())
e=load('equipment.json')['records']; s=load('equipment_slots.json')['records']; r=load('equipment_rules.json')['records']
print(json.dumps({'equipment_count':len(e),'slot_count':len(s),'rule_count':len(r),'slot_population':dict(sorted(Counter(x['slot'] for x in e).items()))},indent=2))
