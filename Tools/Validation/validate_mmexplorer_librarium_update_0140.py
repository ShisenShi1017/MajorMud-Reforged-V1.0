#!/usr/bin/env python3
import json,sys
from pathlib import Path
root=Path(sys.argv[1] if len(sys.argv)>1 else '.')
def load(p): return json.load(open(root/p,encoding="utf-8"))
r=load("Librarium/Objects/rooms/mmexplorer_room_records_0140.json")
e=load("Librarium/Objects/exits/mmexplorer_exit_records_0140.json")
t=load("Librarium/Objects/triggers/mmexplorer_room_interaction_records_0140.json")
room_ids={x["id"] for x in r["records"]}
assert r["record_count"]==len(r["records"])==26666
assert e["record_count"]==len(e["records"])==61976
assert t["record_count"]==len(t["records"])==284
assert len(room_ids)==26666
assert all(x["relationships"]["from_room"] in room_ids for x in e["records"])
missing=[x for x in e["records"] if x["relationships"]["to_room"] not in room_ids]
assert len(missing)==5, f"Expected 5 preserved invalid destinations, got {len(missing)}"
allowed_dispositions={"invalid_destination_blocked","active_runtime"}
assert all(x.get("runtime_disposition") in allowed_dispositions for x in missing)
if all(x.get("runtime_disposition")=="active_runtime" for x in missing):
    generated=load("Data/Classic/exits.json")
    generated_by_id={x["id"]:x for x in generated["records"]}
    assert all(generated_by_id[x["id"]].get("runtime_disposition")=="active_runtime" for x in missing)
    assert all(generated_by_id[x["id"]].get("semantic_status")=="strongly_correlated_active" for x in missing)
assert all(x["relationships"]["from_room"] in room_ids for x in t["records"])
print("PASS: 26,666 rooms, 61,976 exits, 284 interactions; legacy invalid destinations are preserved or semantically repaired.")
