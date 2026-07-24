#!/usr/bin/env python3
import argparse, hashlib, json
from pathlib import Path

def sha256(path):
    h=hashlib.sha256(); h.update(path.read_bytes()); return h.hexdigest()

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--repo',required=True); ap.add_argument('--output',required=True); a=ap.parse_args()
    repo=Path(a.repo); source=repo/'Data/Classic/character_foundation.json'; data=json.loads(source.read_text())
    formulas=data['derived_stat_formulas']
    out={
      'schema_version':1,'code_update':'0142','subsystem':'character_statistics','authority':'Librarium',
      'classification':'proven_for_hp_mana_creation_path',
      'source':{'path':'Data/Classic/character_foundation.json','sha256':sha256(source)},
      'canonical_primary_statistics':['str','int','wil','agl','hea','cha'],
      'promoted_formulas':{
        'maximum_hit_points':formulas['maximum_hit_points'],
        'maximum_mana':formulas['maximum_mana'],
        'spellcasting':formulas['spellcasting']
      },
      'evaluation_order':[
        'read canonical base HEA','compute signed constitution adjustment','seed creation accumulated HP rolls with class hp roll cap',
        'sum HEA/2, class minimum HP, race HP adjustment, constitution adjustment, accumulated rolls, duplicate constitution adjustment, MaxHP ability',
        'clamp maximum HP to minimum 1','select mana formula by class magic type','apply MaxMana ability/equipment cache where defined','clamp mana to minimum 0'
      ],
      'overflow_policy':'checked integer arithmetic; fail safely on overflow',
      'unresolved_formula_families':[
        'SP/EP distinction and regeneration','armor class','attack','defense','initiative','speed','elemental resistances','magic resistance','saving throws','equipment-derived cache order beyond mana cache input'
      ],
      'runtime':{'formula_id':'character.derived.hp_mana.classic_0142','file':'Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs'},
      'tests':{'creation_matrix_cases':13*15,'expected_pair_count':195}
    }
    Path(a.output).write_text(json.dumps(out,indent=2)+'\n')
if __name__=='__main__': main()
