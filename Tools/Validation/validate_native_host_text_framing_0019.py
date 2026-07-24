#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_text_framing_0019.json").read_text())
assert d["schema"]=="majormud.native-host-text-framing.v1"
s=d["summary"]
assert s["text_records"]==len(d["records"])>0
assert s["observed_occurrences"]==sum(x["occurrences"] for x in d["records"])
assert s["records_ending_cr"]==sum(x["ends_with_cr"] for x in d["records"])
assert s["records_ending_lf"]==sum(x["ends_with_lf"] for x in d["records"])
assert s["records_ending_crlf"]==sum(x["ends_with_crlf"] for x in d["records"])
assert s["records_with_embedded_nul"]==0
for x in d["records"]:
 assert x["ends_with_crlf"]==(x["text"].endswith("\r\n"))
 assert x["carriage_return_count"]==x["text"].count("\r")
 assert x["line_feed_count"]==x["text"].count("\n")
print("native host text framing validation passed")
