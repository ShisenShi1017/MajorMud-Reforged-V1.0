#!/usr/bin/env python3
import json, pathlib
p=pathlib.Path(__file__).resolve().parents[2]/"Librarium/Evidence/worldgroup_final_ownership_audit_0036.json"
d=json.loads(p.read_text())
assert d["summary"]["total_symbols"] == 156
assert d["summary"]["matched_exports"] == 156
assert d["summary"]["unmatched_exports"] == 0
assert len(d["symbols"]) == 156
assert all(x["ownership"] == "WGSERVER.EXE" for x in d["symbols"])
assert d["token_boundary"]["observed_symbol"] == "_tokopt"
print("Worldgroup Update0036 ownership audit validated")
