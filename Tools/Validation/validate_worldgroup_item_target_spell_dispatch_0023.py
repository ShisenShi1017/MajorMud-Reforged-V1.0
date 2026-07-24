#!/usr/bin/env python3
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
e=json.loads((root/'Librarium/Evidence/worldgroup_item_target_spell_dispatch_0023.json').read_text())
assert e['entry']=='0x00447110' and e['end']=='0x00449232'
assert e['arguments']==['spellOrPowerDefinition','casterSessionIndex','inventoryOrItemHandle','invocationMode']
assert e['effectExecution']['effectSlotBound']==10
assert e['effectExecution']['outerAttemptBound']==20
cs=(root/'Apps/Source/Reforged.Server/WorldgroupItemTargetSpellDispatchRegistry.cs').read_text()
for token in ['0x00447110','MaximumEffectSlots = 10','DefinitionTypeOffset = 0xCC','type is 6 or 8']:
    assert token in cs
print('Worldgroup item-target spell dispatch Update0023 validation passed')
