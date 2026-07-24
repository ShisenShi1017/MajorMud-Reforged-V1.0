#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_return_consumption_0011.json").read_text())
assert d["schema"]=="majormud.native-host-return-consumption.v1"
assert len(d["modules"])==4
by={m["file"]:m for m in d["modules"]}
assert by["wccmmud.dll"]["direct_callsite_count"]==by["wbbmmud.dll"]["direct_callsite_count"]
assert by["wccmmpls.dll"]["direct_callsite_count"]==by["wbbmmpls.dll"]["direct_callsite_count"]
assert all(all(p["matching_histogram"] for p in s["distribution_parity"]) for s in d["symbols"])
assert d["summary"]["direct_callsites_analyzed"]==14056
assert any(s["symbol"]=="_lngrnd" and s["any_observed_direct_callsite_consumes_eax"] for s in d["symbols"])
print("native host return consumption validation passed")
