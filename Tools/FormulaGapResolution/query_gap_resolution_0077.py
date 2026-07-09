#!/usr/bin/env python3
"""Query CodeUpdate-0077 formula gap resolution tracks.
Usage: python Tools/FormulaGapResolution/query_gap_resolution_0077.py combat
"""
import json, sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
data=json.loads((root/'Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json').read_text(encoding='utf-8'))
term=' '.join(sys.argv[1:]).lower() if len(sys.argv)>1 else ''
for r in data.get('records',[]):
    hay=json.dumps(r).lower()
    if not term or term in hay:
        print(f"{r['resolution_id']} | {r['subsystem']} | {r['title']} | candidates={r['candidate_pool_count']} prioritized={r['prioritized_candidate_count']} | {r['behavior_resolution']['status']}")
