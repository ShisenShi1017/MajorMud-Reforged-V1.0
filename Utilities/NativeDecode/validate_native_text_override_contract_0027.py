#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_text_override_contract_0027.json").read_text())
assert d["summaries"]["wcc_hse"]["record_count"]==47
assert d["summaries"]["wbb_hse"]["record_count"]==133
assert d["summaries"]["wcc_sto"]["record_count"]==7
assert d["summaries"]["wbb_sto"]["record_count"]==7
assert all(r["encoding"]=="7-bit ASCII" and r["cr_count"]==0 for r in d["records"])
assert all(r["filename_suffix"]=="15" for r in d["records"] if r["kind"]=="HSE")
assert all(r["logical_line_count"]==4 and r["identity_marker_matches_canonical_wcc_name"] for r in d["records"] if r["kind"]=="STO")
assert d["runtime_policy"]["normalize_line_endings"] is False
print("native text override contract: PASS")
