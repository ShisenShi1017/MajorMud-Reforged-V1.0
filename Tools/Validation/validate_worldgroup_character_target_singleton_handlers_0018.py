#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
d=json.loads((root/'Reports/WorldgroupNative/worldgroup_character_target_singleton_handlers_0018.json').read_text())
assert d['proven_textblock_edge']['effect_id']==148
assert d['proven_textblock_edge']['callee']=='0x00471D55'
assert d['dispatcher']['maximum_effect_slots']==10
assert any(x['effect_id']==151 for x in d['singleton_findings'])
assert any(x['effect_id']==164 for x in d['singleton_findings'])
text=(root/'Reports/WorldgroupNative/disassembly_004458F0_singleton_handlers_0018.txt').read_text()
for needle in ['44596b:', 'call\t0x471d55', '446e9b:', '447076:']:
    assert needle in text, needle
print('worldgroup character-target singleton handlers 0018: PASS')
