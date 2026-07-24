#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.')
p=root/'Data/NativeDecodeReports/CodeUpdate0146CharacterAbilities/character_ability_ownership_contract_0146.json'
d=json.loads(p.read_text())
assert d['ability_catalog_record_count']==188
assert d['creation_grant_count']>0
assert d['distinct_creation_ability_count']>0
assert d['all_references_resolve'] is True
assert all(x['canonical_ability_id']==f"ability.{x['ability_id']}" for x in d['grants'])
assert all(x['saved'] and x['reset_condition']=='never' for x in d['grants'])
print('Character abilities 0146 validation passed')
