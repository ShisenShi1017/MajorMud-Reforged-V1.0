#!/usr/bin/env python3
"""Query CodeUpdate-0071-0076 formula behavior verification records.
Usage: python Tools/FormulaBehavior/query_formula_behavior_0071_0076.py combat
"""
import json, sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
idx = root/'Librarium/FormulaBehavior/formula_behavior_index_0071_0076.json'
data=json.loads(idx.read_text(encoding='utf-8'))
term=' '.join(sys.argv[1:]).lower() if len(sys.argv)>1 else ''
for r in data.get('records',[]):
    hay=json.dumps(r).lower()
    if not term or term in hay:
        print(f"{r['behavior_id']} | {r['subsystem']} | {r['title']} | {r['evidence_state']} | candidates={r['supporting_candidate_count']}")
