#!/usr/bin/env python3
"""Inspect the checked-in Update0024 native support I/O evidence."""
from pathlib import Path
import json
p=Path("Librarium/Evidence/native_support_io_contracts_0024.json")
d=json.loads(p.read_text())
print(f"contracts={len(d['contracts'])} closures={len(d['closures'])}")
