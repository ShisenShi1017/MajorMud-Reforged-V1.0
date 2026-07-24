#!/usr/bin/env python3
import json, pathlib, sys
p=pathlib.Path(__file__).resolve().parents[2]/"Reports/WorldgroupNative/worldgroup_callback_dispatch_0005.json"
d=json.loads(p.read_text())
errors=[]
if len(d["hook_dispatch"]["hooks"]) != 8: errors.append("hook-count")
heads=[x["head"] for x in d["hook_dispatch"]["hooks"]]
if len(set(heads)) != len(heads): errors.append("duplicate-head")
for r in d["registries"]:
    if int(r["rva"],16) == 0 or r["entry_size"] <= 0: errors.append("invalid-registry:"+r["name"])
if errors:
    print("FAILED", *errors, sep="\n"); sys.exit(1)
print("PASS worldgroup callback dispatch 0005")
