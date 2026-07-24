#!/usr/bin/env python3
import json,sys
from pathlib import Path
p=Path(sys.argv[1] if len(sys.argv)>1 else "Librarium/Evidence/native_support_binary_layouts_0021.json")
d=json.loads(p.read_text());s=d["summary"]
assert d["evidence_class"]=="PROVEN"
assert s["mcv_containers"]==s["mcv_valid_layouts"]==4
assert s["mcv_index_entries"]>0 and s["msg_source_catalogs"]==4
assert all(x["layout_valid"] for x in d["mcv_containers"])
assert all(x["screen_layout_proven"] and x["cell_count"]==2000 for x in d["bin_screen_images"])
assert all(x["ascii_text_count"]==x["file_count"] for x in d["text_support_families"])
print("PASS native support binary layouts",s)
