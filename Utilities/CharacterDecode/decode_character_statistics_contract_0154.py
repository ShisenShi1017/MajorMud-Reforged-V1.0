#!/usr/bin/env python3
import argparse, hashlib, json
from pathlib import Path

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--items', required=True)
    ap.add_argument('--out', required=True)
    a=ap.parse_args()
    src=Path(a.items); data=json.loads(src.read_text(encoding='utf-8'))
    records=data.get('records', [])
    fields=('armor_class_bonus','damage_resistance_bonus','accuracy','speed')
    counts={k:0 for k in fields}
    minimum={k:None for k in fields}; maximum={k:None for k in fields}
    for row in records:
        combat=row.get('combat') or {}
        for key in fields:
            value=int(combat.get(key,0) or 0)
            if value: counts[key]+=1
            minimum[key]=value if minimum[key] is None else min(minimum[key], value)
            maximum[key]=value if maximum[key] is None else max(maximum[key], value)
    out={
      'schema_version':1,'code_update':'0154','subsystem':'character_statistics','authority':'Librarium',
      'classification':'strongly_correlated_explicit_modifier_projection',
      'source':{'path':str(src).replace('\\','/'),'sha256':hashlib.sha256(src.read_bytes()).hexdigest()},
      'record_count':len(records),'explicit_numeric_fields':{},
      'runtime':{'formula_id':'character.statistics.explicit_modifier_projection_0154','save_version':8},
      'evaluation_order':['authoritative equipped-item state','stable slot and instance ordering','checked additive aggregation','canonical derived-stat replacement','canonical resistance/save normalization','projection marker persistence'],
      'unresolved_native_formulas':['base armor class','base attack value','base defense value','initiative','weapon timing conversion','resistance resolution','saving throw resolution','regeneration','cross-category modifier ordering']
    }
    for key in fields:
        out['explicit_numeric_fields'][key]={'nonzero_records':counts[key],'minimum':minimum[key] or 0,'maximum':maximum[key] or 0}
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    Path(a.out).write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
if __name__=='__main__': main()
