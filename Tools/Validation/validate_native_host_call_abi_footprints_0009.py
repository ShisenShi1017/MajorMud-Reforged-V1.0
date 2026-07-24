#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
p=r/"Librarium/Evidence/native_host_call_abi_footprint_0009.json"
d=json.loads(p.read_text())
assert d["schema"]=="majormud.native-host-call-abi-footprint.v1"
assert len(d["modules"])==4
by={m["file"]:m for m in d["modules"]}
assert by["wccmmud.dll"]["direct_host_callsite_count"]==by["wbbmmud.dll"]["direct_host_callsite_count"]
assert by["wccmmpls.dll"]["direct_host_callsite_count"]==by["wbbmmpls.dll"]["direct_host_callsite_count"]
assert all(all(p["matching_footprint_histograms"] for p in s["distribution_parity"]) for s in d["symbols"])
assert any(s["symbol"]=="_lngrnd" for s in d["symbols"])
assert all(sum(i["direct_callsite_count"] for i in m["imports"])==m["direct_host_callsite_count"] for m in d["modules"])
print("native host call ABI footprint validation passed")
