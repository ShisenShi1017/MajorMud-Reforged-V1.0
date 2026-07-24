#!/usr/bin/env python3
import json,sys
from pathlib import Path
r=Path(sys.argv[1] if len(sys.argv)>1 else '.')
cs=(r/'Apps/Source/Reforged.Server/RuntimeCharacterProgressionSchedule.cs').read_text()
world=(r/'Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs').read_text()
for token in ['ExecutableClassifications','strongly_correlated','source_id','Conflicting executable progression rows','not greater than prior threshold','MaximumLevel']:
    assert token in cs, token
assert 'RequireCharacterPointAward(character, targetLevel)' in world
contract=json.loads((r/'Data/NativeDecodeReports/CodeUpdate0153CharacterProgression/character_level_progression_contract_0153.json').read_text())
assert contract['code_update']=='0153' and len(contract['unknown_native'])>=7
candidates=json.loads((r/'Data/NativeDecodeReports/CodeUpdate0153CharacterProgression/progression_schedule_candidates_0153.json').read_text())
assert all(not x['runtime_promoted'] and x['classification']=='weak' for x in candidates['candidates'])
print('CodeUpdate0153 character progression validation passed')
