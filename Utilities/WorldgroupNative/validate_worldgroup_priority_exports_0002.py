#!/usr/bin/env python3
import json,sys
from pathlib import Path
p=Path(sys.argv[1] if len(sys.argv)>1 else "Reports/WorldgroupNative/worldgroup_priority_export_implementations_0002.json")
d=json.loads(p.read_text())
assert d["schema"]=="majorMud.worldgroup.exportImplementation.v1"
assert len(d["exports"])==6
assert len({x["symbol"] for x in d["exports"]})==6
assert all(x["confidence"]=="proven" for x in d["exports"])
print("validated 6 priority Worldgroup export implementations")
