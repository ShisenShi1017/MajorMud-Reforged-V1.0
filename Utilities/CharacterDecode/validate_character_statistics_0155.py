#!/usr/bin/env python3
import argparse,json
from pathlib import Path

def req(c,m):
    if not c: raise SystemExit(m)
def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',required=True); a=ap.parse_args(); r=Path(a.root)
    c=json.loads((r/'Data/NativeDecodeReports/CodeUpdate0155CharacterStatistics/character_statistics_contract_0155.json').read_text())
    req(c['record_count']==8,'expected eight explicit ability mappings')
    ids={x['ability_id'] for x in c['records']}; req(ids=={3,5,65,66,87,123,145,147},'ability id set mismatch')
    cs=(r/'Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs').read_text()
    for t in ('character.statistics.explicit_modifier_projection_0155','HitPointRegenerationAbilityId = 123','ManaRegenerationAbilityId = 145','ResistWaterAbilityId = 147','hit_point_regeneration_percent','mana_regeneration_percent','["cold"]','["fire"]','["stone"]','["lightning"]','["water"]'):
        req(t in cs,'missing runtime token '+t)
    req(cs.count('{')==cs.count('}'),'C# braces unbalanced')
    print('character statistics 0155 validation passed')
if __name__=='__main__': main()
