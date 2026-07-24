#!/usr/bin/env python3
import json,sys
from pathlib import Path
p=Path(sys.argv[1] if len(sys.argv)>1 else "Librarium/Evidence/native_support_catalog_0020.json")
d=json.loads(p.read_text())
s=d["summary"]
assert d["evidence_class"]=="PROVEN"
assert s["header_files"]>=4 and s["message_symbol_definitions"]>100
assert s["module_descriptors"]>=2 and s["uic_owned_files"]>0
assert all(h["define_count"]==len(h["defines"]) for h in d["headers"])
print(f"PASS native support catalog: {s}")
