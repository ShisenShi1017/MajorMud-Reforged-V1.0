#!/usr/bin/env python3
from __future__ import annotations
import json, pathlib
from collections import Counter
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Reports/NativeDecode/native_host_call_abi_footprint_0009.json").read_text())
rows=[]
for s in d["symbols"]:
    clean=Counter(); none=0; pushes=Counter()
    for m in s["modules"].values():
        for k,v in m["immediate_caller_stack_cleanup_histogram"].items():
            if k=="none_observed": none+=v
            else: clean[int(k)]+=v
        for k,v in m["bounded_pre_call_push_histogram"].items(): pushes[int(k)]+=v
    if none==0 and len(clean)==1:
        cleanup=next(iter(clean)); slots=cleanup//4 if cleanup%4==0 else None
        rows.append((s["symbol"],s["total_direct_callsites"],cleanup,slots is not None and set(pushes)=={slots}))
print(json.dumps({"uniform_cleanup_symbols":len(rows),"exact_bounded_match_symbols":sum(1 for x in rows if x[3]),"symbols":rows},indent=2))
