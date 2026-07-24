#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_argument_positions_0014.json").read_text())
assert d["schema"]=="majormud.native-host-argument-position.v1"
assert len(d["modules"])==4
assert d["summary"]["direct_callsites_analyzed"]==4630
assert d["summary"]["host_symbols_analyzed"]==121
assert d["summary"]["all_distribution_profiles_match"]
assert d["summary"]["symbols_with_stable_argument_count_across_modules"] > 0
assert d["summary"]["symbols_with_at_least_one_stable_argument_position"] > 0
assert all(all(p["matching_profile"] for p in s["distribution_parity"]) for s in d["symbols"])
print("native host argument position validation passed")
