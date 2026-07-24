#!/usr/bin/env python3
import json, pathlib
p=pathlib.Path(__file__).resolve().parents[2]/"Reports/WorldgroupNative/worldgroup_gameplay_command_gateway_0011.json"
d=json.loads(p.read_text())
assert d["function"]["va"]=="0x0045003C"
assert any(x.get("address")=="0x0046B010" for x in d["proven_findings"])
assert any(x.get("text")=="Your command had no effect." for x in d["proven_findings"])
assert len(d["command_order"])==7
print("Worldgroup Update0011 gameplay command gateway evidence validated")
