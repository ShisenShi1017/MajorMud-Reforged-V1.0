#!/usr/bin/env python3
import json, pathlib, re
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_text_template_contract_0017.json").read_text())
assert d["schema"]=="majormud.native-host-text-template-contract.v1"
s=d["summary"]
assert s["ascii_records"]==len(d["records"])>0
assert s["printf_template_records"]+s["literal_records"]==s["ascii_records"]
assert s["observed_ascii_argument_occurrences"]==sum(x["occurrences"] for x in d["records"])
assert s["format_conversion_occurrences"]==sum(x["conversion_count"]*x["occurrences"] for x in d["records"])
assert all(x["matching_syntactic_contract"] for x in d["distribution_parity"])
for x in d["records"]:
 assert x["occurrences"]>0 and x["byte_length"]==len(x["text"].encode("ascii"))
 assert x["conversion_count"]==len(x["format_directives"])==len(x["conversion_codes"])
 assert ("printf_conversion_template" in x["features"]) == (x["conversion_count"]>0)
print("native host text template contract validation passed")
