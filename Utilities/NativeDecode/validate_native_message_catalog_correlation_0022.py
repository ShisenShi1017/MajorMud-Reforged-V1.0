#!/usr/bin/env python3
import json,sys
from pathlib import Path
p=Path(sys.argv[1]);d=json.loads(p.read_text());s=d["summary"]
assert d["evidence_class"]=="PROVEN"
assert s["catalog_count"]==4
assert s["total_mcv_slots"]==423
assert s["total_mcv_slots"]==s["defined_symbol_slots"]+s["undefined_or_reserved_slots"]
for c in d["catalogs"]:
 assert len(c["entries"])==c["entry_count"]
 for e in c["entries"]: assert e["message_number"]>=0 and e["offset_end"]>=e["offset_start"]
print("PASS native message catalog correlation",s)
