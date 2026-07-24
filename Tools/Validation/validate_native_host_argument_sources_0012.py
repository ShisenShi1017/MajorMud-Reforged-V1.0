#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_argument_sources_0012.json").read_text())
assert d["schema"]=="majormud.native-host-argument-source.v1"
assert len(d["modules"])==4
assert d["summary"]["direct_callsites_analyzed"]==14056
assert d["summary"]["host_symbols_analyzed"]==121
assert d["summary"]["all_distribution_profiles_match"]
assert all(all(p["matching_profile"] for p in s["distribution_parity"]) for s in d["symbols"])
assert any(s["has_zero_or_null_argument_evidence"] for s in d["symbols"])
assert any(s["has_address_or_pointer_argument_evidence"] for s in d["symbols"])
print("native host argument source validation passed")
