#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_text_binding_0018.json").read_text())
assert d["schema"]=="majormud.native-host-text-binding.v1"
s=d["summary"]
assert s["host_symbols_with_ascii_arguments"]==len(d["records"])>0
assert s["host_symbols_with_templates"]+s["host_symbols_literal_only"]==len(d["records"])
assert s["template_binding_observations"]==sum(len(x["binding_capacity_observations"]) for x in d["records"])
for x in d["records"]:
 assert x["total_occurrences"]==x["template_occurrences"]+x["literal_occurrences"]
 assert x["template_position_stable"]==(len(x["template_positions"])<=1)
 for y in x["binding_capacity_observations"]:
  assert y["capacity_sufficient"]==(y["observed_later_positions"]>=y["conversion_count"])
print("native host text binding validation passed")
