#!/usr/bin/env python3
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
e=json.loads((root/'Librarium/Evidence/worldgroup_room_area_spell_dispatch_0024.json').read_text())
assert e['entry']=='0x00449232' and e['end']=='0x00449BF0'
assert e['sizeBytes']==2494
assert e['arguments']==['spellOrPowerDefinition','casterSessionIndex','roomOrAreaHandle','invocationMode']
assert e['effectExecution']['effectSlotBound']==10
assert e['definitionAndCasterFields']['allowedDefinitionTypes']==[6,7]
cs=(root/'Apps/Source/Reforged.Server/WorldgroupRoomAreaSpellDispatchRegistry.cs').read_text()
for token in ['0x00449232','SizeBytes = 2494','MaximumEffectSlots = 10','id is 105 or 106','RoomMutationHelper = 0x0046C241']:
    assert token in cs
print('Worldgroup room/area spell dispatch Update0024 validation passed')
