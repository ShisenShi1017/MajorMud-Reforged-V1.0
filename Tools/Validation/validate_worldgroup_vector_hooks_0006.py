#!/usr/bin/env python3
import json, pathlib, sys
p=pathlib.Path(__file__).resolve().parents[2]/"Reports/WorldgroupNative/worldgroup_vector_hooks_0006.json"
d=json.loads(p.read_text())
assert d["array_runtime"]["_add2arr"]["va"]=="0x004381F2"
assert d["array_runtime"]["_dovecs"]["ordering"]=="registration/FIFO order"
assert len(d["hook_vectors"]["hooks"])==11
print("worldgroup vector hook validation passed")
