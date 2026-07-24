#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_caller_cleanup_cohort_0010.json").read_text())
assert d["schema"]=="majormud.native-host-caller-cleanup-cohort.v1"
assert d["summary"]["symbols_with_uniform_immediate_caller_cleanup"]==42
assert d["summary"]["symbols_with_exact_bounded_push_cleanup_match"]==28
assert d["summary"]["symbols_with_uniform_cleanup_but_nonexact_bounded_pushes"]==14
assert all(x["immediate_caller_cleanup_bytes"]%4==0 for x in d["symbols"])
assert all(x["all_observed_callsites_have_immediate_cleanup"] for x in d["symbols"])
by={x["symbol"]:x for x in d["symbols"]}
assert by["_sameas"]["immediate_caller_cleanup_bytes"]==8 and by["_sameas"]["all_observed_bounded_push_counts_match_cleanup_slots"]
assert by["_lngrnd"]["immediate_caller_cleanup_bytes"]==8 and not by["_lngrnd"]["all_observed_bounded_push_counts_match_cleanup_slots"]
print("native host caller cleanup validation passed")
