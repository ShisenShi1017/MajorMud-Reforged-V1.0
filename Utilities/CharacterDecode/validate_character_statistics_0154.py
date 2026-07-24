#!/usr/bin/env python3
import argparse, json, re
from pathlib import Path

def require(cond, msg):
    if not cond: raise SystemExit(msg)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root', required=True); a=ap.parse_args()
    root=Path(a.root)
    contract=json.loads((root/'Data/NativeDecodeReports/CodeUpdate0154CharacterStatistics/character_statistics_contract_0154.json').read_text())
    require(contract['record_count']==1950,'unexpected item record count')
    expected={'armor_class_bonus':565,'damage_resistance_bonus':246,'accuracy':285,'speed':393}
    for k,v in expected.items(): require(contract['explicit_numeric_fields'][k]['nonzero_records']==v,f'{k} count mismatch')
    cs=(root/'Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs').read_text()
    sem=(root/'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs').read_text()
    for token in ('RuntimeCharacterCombatStatisticProjector','character.statistics.explicit_modifier_projection_0154','armor_class_bonus','damage_resistance_bonus','accuracy_bonus','speed_bonus','ProjectionMarker'):
        require(token in cs,f'missing runtime token {token}')
    require('CurrentSaveVersion = 7' in sem,'save version 7 missing')
    require('RuntimeCharacterCombatStatisticProjector.Validate(semantic, character.SaveVersion);' in sem,'stat validation hook missing')
    require(cs.count('{')==cs.count('}'),'derived statistics C# braces unbalanced')
    require(sem.count('{')==sem.count('}'),'semantic state C# braces unbalanced')
    print('character statistics 0154 validation passed')
if __name__=='__main__': main()
