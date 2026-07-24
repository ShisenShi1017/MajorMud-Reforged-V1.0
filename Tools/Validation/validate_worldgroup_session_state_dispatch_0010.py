#!/usr/bin/env python3
import json, pathlib
p=pathlib.Path(__file__).resolve().parents[2]/"Reports/WorldgroupNative/worldgroup_session_state_dispatch_0010.json"
d=json.loads(p.read_text())
assert d["dispatch"]["index_table_length"] == 137
assert d["dispatch"]["unique_selector_count"] == 38
assert len(d["handlers"]) == 38
states=[s for h in d["handlers"] for s in h["states"]]
assert len(states)==137 and len(set(states))==137
assert any(h["target_va"]=="0x004088CC" and "0x82" in h["states"] for h in d["handlers"] )
print("Worldgroup Update0010 session-state evidence validated")
