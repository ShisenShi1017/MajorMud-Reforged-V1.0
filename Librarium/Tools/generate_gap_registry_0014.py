#!/usr/bin/env python3
"""Generate a Librarium knowledge gap registry from formula records and known handoff unknowns."""
import json, argparse, hashlib
from pathlib import Path
KNOWN=['combat equations','XP equations','regen equations','loot equations','spawn timing','AI weighting','damage rounding','tick timing','quest ordered stages','quest branching','quest failure states','dialogue ordering','dialogue conditions','conversation trees','script semantics','runtime timing']
def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root', default='.'); args=ap.parse_args(); root=Path(args.root)
    formulas=json.loads((root/'Librarium/FormulaValidation/formula_records_0005.json').read_text(encoding='utf-8'))
    gaps=[]
    for i,g in enumerate(KNOWN,1):
        gaps.append({'gap_id':f'gap:0014:{i:04d}','name':g,'priority':'High' if i<=8 else 'Medium','evidence_state':'Hypothesis','promotion_requirements':['precise evidence','consumer mapping','runtime or simulator validation']})
    for f in formulas:
        for need in f.get('needs',[]):
            gaps.append({'gap_id':'gap:formula:'+hashlib.sha1((f.get('formulaId','')+need).encode()).hexdigest()[:12],'name':need,'priority':'High','evidence_state':'Derived','current_consumers':[f.get('formulaId')]})
    out=root/'Librarium/GapRegistry'; out.mkdir(parents=True, exist_ok=True)
    (out/'knowledge_gap_registry_0014.json').write_text(json.dumps({'gap_count':len(gaps),'gaps':gaps}, indent=2))
    print(f'wrote {len(gaps)} gaps')
if __name__ == '__main__': main()
