#!/usr/bin/env python3
from pathlib import Path
import json
p=Path("Librarium/Evidence/native_support_payload_contracts_0025.json")
d=json.loads(p.read_text())
print(f"BIN={len(d['bin_screens'])} ADDON={d['wccaddon']['line_count']} DMA={d['wccdma']['line_count']}")
