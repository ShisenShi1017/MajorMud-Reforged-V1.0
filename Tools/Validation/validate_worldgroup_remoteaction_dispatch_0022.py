#!/usr/bin/env python3
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
e=json.loads((root/'Librarium/Evidence/worldgroup_remoteaction_dispatch_0022.json').read_text())
assert e['entry']=='0x0046C573' and e['end']=='0x0046CC0B'
assert e['arguments']==['roomActionRecord','actionIndex','selector','mode']
classes=e['typeDispatch']['classes']
assert classes[0]['types']==[7,11]
assert classes[1]['types']==[9,24]
assert classes[2]['types']==[6]
assert len(e['typeDispatch']['selectorBytesByType6Through24'])==19
cs=(root/'Apps/Source/Reforged.Server/WorldgroupRemoteActionDispatchRegistry.cs').read_text()
for token in ['0x0046C573','ActionTypeOffset = 0x360','RoomObjectSlotCount = 15']:
    assert token in cs
print('Worldgroup remoteaction dispatch Update0022 validation passed')
