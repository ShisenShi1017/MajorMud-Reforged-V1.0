#!/usr/bin/env python3
import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
paths = [
 root/'Reports/WorldgroupNative/worldgroup_room_broadcast_dispatch_0012.json',
 root/'Librarium/Evidence/worldgroup_room_broadcast_dispatch_0012.json'
]
for path in paths:
    data=json.loads(path.read_text(encoding='utf-8'))
    assert data['function']['va']=='0x0046B010'
    assert data['function']['argument_count']==8
    assert len(data['proven_findings'])>=7
    assert len(data['negative_findings'])==3
cs=(root/'Apps/Source/Reforged.Server/WorldgroupRoomBroadcastDispatchRegistry.cs').read_text(encoding='utf-8')
assert 'IsCommandTokenizer => false' in cs
assert 'IsCommandLookup => false' in cs
print('worldgroup room broadcast dispatch 0012: PASS')
