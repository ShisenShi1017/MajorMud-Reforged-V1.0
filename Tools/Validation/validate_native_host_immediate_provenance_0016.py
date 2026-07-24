#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_immediate_provenance_0016.json").read_text())
assert d["schema"]=="majormud.native-host-immediate-provenance.v1"
assert d["summary"]["distinct_immediate_records"]>0
assert d["summary"]["module_address_occurrences"]>0
assert d["summary"]["ascii_address_records"]>0
assert all(x["matching_classification_profile"] for x in d["distribution_parity"])
for x in d["records"]:
 assert x["occurrences"]>0
 if x["classification"].startswith("module_"): assert x["module_section"] is not None
 if x["ascii_text"] is not None: assert x["classification"]=="module_ascii_address"
print("native host immediate provenance validation passed")
