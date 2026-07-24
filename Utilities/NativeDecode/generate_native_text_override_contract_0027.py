#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_text_override_contract_0027.json").read_text())
for k,v in d["summaries"].items(): print(k, v["record_count"], v["line_count_histogram"], v["max_line_bytes"])
