#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_return_domains_0013.json").read_text())
assert d["schema"]=="majormud.native-host-return-domain.v1"
assert len(d["modules"])==4
assert d["summary"]["direct_callsites_analyzed"]==14056
assert d["summary"]["host_symbols_analyzed"]==121
assert d["summary"]["all_distribution_profiles_match"]
assert all(all(p["matching_profile"] for p in s["distribution_parity"]) for s in d["symbols"])
assert d["summary"]["symbols_with_any_bounded_domain_evidence"] > 0
assert any(s["has_zero_nonzero_test"] for s in d["symbols"])
assert any(s["has_constant_comparison"] for s in d["symbols"])
print("native host return domain validation passed")
