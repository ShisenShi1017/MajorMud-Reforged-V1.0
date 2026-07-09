#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
required={
 "items":"item_records_0122.json", "spells":"spell_records_0122.json", "npcs":"npc_records_0122.json", "rooms":"room_records_0122.json", "spawns":"spawn_records_0122.json", "shops":"shop_records_0122.json"}
errors=[]; counts={}
for folder,fname in required.items():
    p=root/"Librarium"/"Objects"/folder/fname
    if not p.exists(): errors.append(f"missing {p}"); continue
    data=json.loads(p.read_text(encoding="utf-8")); counts[folder]=data.get("record_count",0)
    if data.get("record_count",0)<=0: errors.append(f"empty {p}")
rel=root/"Librarium"/"Relationships"/"librarium_relationship_graph_0122.json"
if not rel.exists(): errors.append(f"missing {rel}")
else:
    r=json.loads(rel.read_text(encoding="utf-8")); counts["relationships"]=r.get("relationship_count",0)
    if r.get("relationship_count",0)<=0: errors.append("empty relationship graph")
if errors:
    print("FAIL")
    for e in errors: print(e)
    sys.exit(1)
print("PASS")
print(counts)
