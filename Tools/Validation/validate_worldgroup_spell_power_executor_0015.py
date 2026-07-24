#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
for p in [root/'Reports/WorldgroupNative/worldgroup_spell_power_executor_0015.json',root/'Librarium/Evidence/worldgroup_spell_power_executor_0015.json']:
 d=json.loads(p.read_text())
 assert d['function']['start']=='0x00461273'
 assert d['function']['end']=='0x0046196B'
 assert set(d['target_modes'])=={'0','1','2','4','8'}
 assert len(d['proven_findings'])>=15
cs=(root/'Apps/Source/Reforged.Server/WorldgroupSpellPowerExecutorRegistry.cs').read_text()
for t in ['ExecutorStart = 0x00461273','CastSelector = 0x1F','InvokeSelector = 0x6C','TargetClassifier = 0x004699FC','CharacterOrCreature = 1','RoomOrArea = 8']:
 assert t in cs
print('worldgroup spell/power executor 0015: PASS')
