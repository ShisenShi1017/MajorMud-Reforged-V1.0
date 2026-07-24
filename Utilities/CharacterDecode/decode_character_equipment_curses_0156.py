#!/usr/bin/env python3
import json, sys
from pathlib import Path

def main(root: Path) -> int:
    records=json.loads((root/"Data/Classic/items.json").read_text())["records"]
    out={82:[],83:[]}
    for record in records:
        abilities=(((record.get("public_reference_0139") or {}).get("fields") or {}).get("abilities_named") or [])
        ids={int(a["ability_id"]) for a in abilities if str(a.get("ability_id","")).isdigit()}
        for ability_id in out:
            if ability_id in ids: out[ability_id].append(record["id"])
    print(json.dumps({"item_count":len(records),"ability_82":out[82],"ability_83":out[83]},indent=2))
    return 0

if __name__ == "__main__":
    raise SystemExit(main(Path(sys.argv[1] if len(sys.argv)>1 else ".")))
