#!/usr/bin/env python3
import json,sys
from pathlib import Path
p=Path(sys.argv[1]) if len(sys.argv)>1 else Path("Data/Classic/items.json")
r=json.loads(p.read_text())["records"]
fields=("armor_class_bonus","damage_resistance_bonus","accuracy","speed")
print(json.dumps({"item_count":len(r),"nonzero_modifier_counts":{f:sum(1 for x in r if int(x.get("combat",{}).get(f,0))!=0) for f in fields}},indent=2))
