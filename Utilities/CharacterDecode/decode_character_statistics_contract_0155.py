#!/usr/bin/env python3
import argparse, hashlib, json, re
from pathlib import Path

ABILITY_MAP = {
    3: ('cold', 'resistance'), 5: ('fire', 'resistance'), 65: ('stone', 'resistance'),
    66: ('lightning', 'resistance'), 87: ('speed_bonus', 'derived_stat'),
    123: ('hit_point_regeneration_percent', 'regeneration'),
    145: ('mana_regeneration_percent', 'regeneration'),
    147: ('water', 'resistance'),
}

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--source-root', required=True); ap.add_argument('--output-root', required=True); a=ap.parse_args()
    src=Path(a.source_root); out=Path(a.output_root)
    path=src/'Data/Classic/abilities.json'
    payload=json.loads(path.read_text())
    by_id={int(r['legacy_id']):r for r in payload['records']}
    rows=[]
    for ability_id,(canonical,target) in ABILITY_MAP.items():
        r=by_id[ability_id]; f=r['public_reference_0139']['fields']
        rows.append({
            'ability_id': ability_id, 'canonical_id': canonical, 'target': target,
            'name': f['name'], 'description': f['description'],
            'classification': 'strongly_correlated',
            'provenance': r['evidence_refs'][0],
            'assumptions': ['ability value is the numeric parameter exposed by RuntimeCharacterAbilityValues'],
            'fallback': 'preserve zero when the character has no owned value for this ability',
            'runtime_impact': 'direct additive projection only; no hidden base formula inferred',
            'save_impact': 'projected values persist in semantic derived-stat/resistance maps with formula marker',
            'world_state_impact': 'none outside character-local combat/statistic state'
        })
    contract={
      'schema_version':1,'code_update':'0155','subsystem':'character_statistics',
      'authority':'Librarium','classification':'strongly_correlated_explicit_ability_projection',
      'source':{'path':'Data/Classic/abilities.json','sha256':hashlib.sha256(path.read_bytes()).hexdigest()},
      'record_count':len(rows),'records':rows,
      'runtime':{'formula_id':'character.statistics.explicit_modifier_projection_0155','save_version':7},
      'evaluation_order':['persisted ability ownership restore','explicit equipment modifier aggregation','explicit ability modifier projection','canonical map replacement','stable provenance marker persistence'],
      'unresolved_native_formulas':['base armor class','base attack value','base defense value','initiative','weapon timing conversion','saving throw resolution','base regeneration tick quantity and cadence','cross-category hidden modifier ordering']
    }
    op=out/'Data/NativeDecodeReports/CodeUpdate0155CharacterStatistics/character_statistics_contract_0155.json'; op.parent.mkdir(parents=True,exist_ok=True); op.write_text(json.dumps(contract,indent=2)+'\n')
    print(op)
if __name__=='__main__': main()
