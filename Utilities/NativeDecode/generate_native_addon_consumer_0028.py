#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_addon_consumer_0028.json").read_text())
print(d["loader"])
print(d["lookup"])
