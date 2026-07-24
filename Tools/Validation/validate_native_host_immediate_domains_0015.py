#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_immediate_domains_0015.json").read_text())
assert d["schema"]=="majormud.native-host-immediate-domain.v1"
assert d["summary"]["host_symbols_analyzed"]==121
assert d["summary"]["finite_immediate_argument_domains"]>0
assert d["summary"]["singleton_immediate_domains"]>0
for s in d["symbols"]:
  for p in s["positions"]:
    if p["immediate_only_domain"]:
      assert p["distinct_value_count"]==len(p["values"]) and p["distinct_value_count"]>0
      assert all(0 <= v["value_u32"] <= 0xffffffff for v in p["values"])
print("native host immediate domain validation passed")
