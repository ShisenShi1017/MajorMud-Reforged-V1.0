#!/usr/bin/env python3
import json, sys
from pathlib import Path
root=Path(sys.argv[1] if len(sys.argv)>1 else '.')
c=json.loads((root/'Data/NativeDecodeReports/CodeUpdate0144CharacterEquipment/character_equipment_contract_0144.json').read_text())
assert c['source_records']=={'equipment_count':532,'slot_count':17,'rule_count':3}
slots={x['id'] for x in c['slots']}
assert {'NONE','WIELD','OFF_HAND','SHIELD','LIGHT'} <= slots
rules={x['rule'] for x in c['rules']}
assert 'TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD' in rules
cs=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
for token in ['Same(slot, "NONE")','TWO_HANDED_CONFLICT','Two-handed equipment conflicts with off-hand or shield state']:
    assert token in cs, token
print('PASS: Character equipment pass 0144 validated')
