#!/usr/bin/env python3
import json
from pathlib import Path
p=Path(__file__).resolve().parents[2]/"Reports/WorldgroupNative/worldgroup_character_target_effect_dispatch_0017.json"
d=json.loads(p.read_text())
assert d["dispatcher"]["effect_map_entries"] == 165
assert d["dispatcher"]["dispatch_classes"] == 33
assert len(d["dispatch_classes"]) == 33
ids=[]
for c in d["dispatch_classes"]: ids.extend(c["effect_ids"])
assert sorted(ids)==list(range(165))
assert len(set(ids))==165
print("worldgroup character-target effect dispatch 0017: PASS")
