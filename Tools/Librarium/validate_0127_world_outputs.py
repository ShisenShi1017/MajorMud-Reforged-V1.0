#!/usr/bin/env python3
import json, sys, hashlib
from pathlib import Path
root = Path(__file__).resolve().parents[2]
worlds = [root/"Data/Classic", root/"Data/Worlds/CustomTemplate", root/"Data/Worlds/AIFromClassic"]
errors=[]
for w in worlds:
    if not w.exists(): errors.append(f"missing world output: {w}"); continue
    for name in ["compile_manifest.json","world_manifest.json","client_projection.json"]:
        if not (w/name).exists(): errors.append(f"missing {name} in {w}")
    if (w/"compile_manifest.json").exists():
        m=json.loads((w/"compile_manifest.json").read_text())
        if m.get("code_update") != "0127": errors.append(f"bad code_update in {w}")
        if m.get("object_families_compiled",0) < 10: errors.append(f"too few families in {w}")
if not (root/"ImportedDatabases").exists(): errors.append("ImportedDatabases missing")
if errors:
    print("FAIL")
    print("\n".join(errors))
    sys.exit(1)
print("PASS CodeUpdate0127 world outputs validated")
