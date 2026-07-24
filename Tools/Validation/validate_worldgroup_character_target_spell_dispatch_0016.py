#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
for p in [root/'Reports/WorldgroupNative/worldgroup_character_target_spell_dispatch_0016.json',root/'Librarium/Evidence/worldgroup_character_target_spell_dispatch_0016.json']:
 d=json.loads(p.read_text())
 assert d['function']['start']=='0x00444712'
 assert d['function']['bounded_end']=='0x00447110'
 assert d['function']['arguments']==5
 assert len(d['proven_findings'])>=10
cs=(root/'Apps/Source/Reforged.Server/WorldgroupCharacterTargetSpellDispatchRegistry.cs').read_text()
for t in ['DispatcherStart = 0x00444712','DispatcherEnd = 0x00447110','ArgumentCount = 5','MaximumBaseChance = 100']:
 assert t in cs
print('worldgroup character-target spell dispatch 0016: PASS')
