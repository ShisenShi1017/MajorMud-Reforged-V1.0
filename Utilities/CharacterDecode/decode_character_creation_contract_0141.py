#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main() -> int:
    ap=argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--output', default='Data/NativeDecodeReports/CodeUpdate0141CharacterCreation/character_creation_contract_0141.json')
    args=ap.parse_args(); root=Path(args.root)
    source=root/'Data/Classic/character_foundation.json'
    data=json.loads(source.read_text(encoding='utf-8'))
    rules=data['character_creation_rules']; races=data['races']; classes=data['classes']
    pairs=[{'race_number':r['number'],'race':r['name'],'class_number':c['number'],'class':c['name']} for r in races for c in classes]
    contract={
      'schema_version':1,'code_update':'0141','subsystem':'character_creation','authority':'Librarium',
      'classification':'proven_or_promoted_strongly_correlated','source':{'path':str(source).replace('\\','/'),'sha256':sha(source)},
      'counts':{'races':len(races),'classes':len(classes),'race_class_pairs':len(pairs)},
      'validation':{'all_race_class_combinations_allowed':rules['all_race_class_combinations_allowed'],'declared_pair_count':rules['allowed_pair_count'],'computed_pair_count':len(pairs),'pair_matrix_exact':rules['allowed_pair_count']==len(pairs)},
      'initial_state':{
        'level':1,'experience':0,'statistics_rule':rules['starting_stats_rule'],'alignment_value':rules['starting_alignment'],'alignment_name':rules['starting_alignment_name'],
        'gold':rules['starting_gold'],'inventory':rules['starting_inventory'],'equipment':rules['starting_equipment'],'spells':rules['starting_spells'],
        'abilities_rule':rules['starting_abilities_rule'],'start':rules['new_character_start']},
      'race_constraints':[{'number':r['number'],'name':r['name'],'minimum_stats':r['minimum_stats'],'maximum_stats':r['maximum_stats'],'base_cp':r['base_cp'],'experience_add_percent':r['experience_add_percent'],'hp_per_level_adjustment':r['hp_per_level_adjustment'],'ability_slots':r['ability_slots'],'source':r['source']} for r in races],
      'class_constraints':[{'number':c['number'],'name':c['name'],'min_hits':c['min_hits'],'max_hits_bonus':c['max_hits_bonus'],'experience_percent':c['experience_percent'],'magery_type':c['magery_type'],'magery_level':c['magery_level'],'weapon_type':c['weapon_type'],'armour_type':c['armour_type'],'combat_level':c['combat_level'],'ability_slots':c['ability_slots'],'source':c['source']} for c in classes],
      'allowed_pairs':pairs,
      'unknowns':[{'id':'character.creation.ability_generation_selection','classification':'native_dependency','runtime_behavior':'creation uses promoted race minimum statistics and deterministic full HP-roll cap; interactive point-buy/roll UI semantics remain unpromoted','fallback':'fail closed for unsupported custom stat selection'}]
    }
    out=root/args.output; out.parent.mkdir(parents=True,exist_ok=True); out.write_text(json.dumps(contract,indent=2)+'\n',encoding='utf-8')
    print(out)
    return 0
if __name__=='__main__': raise SystemExit(main())
