#!/usr/bin/env python3
"""Build local Librarium graph/index artifacts from canonical Librarium records.
Generated outputs are convenience artifacts and are not canonical truth.
"""
import json, hashlib, argparse
from pathlib import Path

def read_json(path, default):
    try:
        return json.loads(Path(path).read_text(encoding='utf-8'))
    except Exception:
        return default

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--out', default='Generated/LibrariumLocal')
    args=ap.parse_args()
    root=Path(args.root)
    out=root/args.out; out.mkdir(parents=True, exist_ok=True)
    entities=read_json(root/'Librarium/Assertions/knowledge_entities_0011.json', [])
    assertions=read_json(root/'Librarium/Assertions/knowledge_assertions_0011.json', [])
    formulas=read_json(root/'Librarium/FormulaValidation/formula_records_0005.json', [])
    nodes=[]; edges=[]
    for e in entities:
        nodes.append({'id':e.get('id'), 'type':e.get('kind') or e.get('type') or 'entity', 'name':e.get('name'), 'evidence_state':e.get('evidence_state') or e.get('evidenceState'), 'confidence':e.get('confidence')})
    for a in assertions:
        edges.append({'id':a.get('assertion_id'), 'subject':a.get('subject'), 'predicate':a.get('predicate'), 'object':a.get('object'), 'evidence_state':a.get('evidence_state'), 'confidence':a.get('confidence')})
    for f in formulas:
        nodes.append({'id':f.get('formulaId'), 'type':'formula', 'name':f.get('name'), 'evidence_state':f.get('evidenceState'), 'confidence':f.get('confidence'), 'classicExactEquationVerified':f.get('classicExactEquationVerified')})
    (out/'core_graph.json').write_text(json.dumps({'metadata':{'canonical_source':'/Librarium','generated_not_canonical':True},'counts':{'nodes':len(nodes),'edges':len(edges)},'nodes':nodes,'edges':edges}, indent=2))
    (out/'formula_index.json').write_text(json.dumps({'metadata':{'generated_not_canonical':True},'formulas':formulas}, indent=2))
    print(f'built {len(nodes)} nodes and {len(edges)} edges into {out}')
if __name__ == '__main__': main()
