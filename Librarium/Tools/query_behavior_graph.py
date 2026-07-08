#!/usr/bin/env python3
import json, sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
behavior = json.loads((root/'Librarium'/'BehaviorGraph'/'behavior_actions_0008.json').read_text())['behaviors']
impact = json.loads((root/'Librarium'/'BehaviorGraph'/'impact_dependency_index_0008.json').read_text())['topImpactRecords']
q = ' '.join(sys.argv[1:]).lower() if len(sys.argv)>1 else ''
if not q:
    print('Usage: python Librarium/Tools/query_behavior_graph.py <term>')
    sys.exit(0)
for b in behavior:
    hay = json.dumps(b).lower()
    if q in hay:
        print(json.dumps(b, indent=2))
for i in impact[:100]:
    if q in json.dumps(i).lower():
        print(json.dumps(i, indent=2))
