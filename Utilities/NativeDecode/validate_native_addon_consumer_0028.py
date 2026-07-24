#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_addon_consumer_0028.json").read_text())
assert d["loader"]["mode"]=="rt"
assert d["loader"]["line_buffer_bytes"]==100
assert d["loader"]["allocation_bytes_per_node"]==12
assert d["loader"]["delimiter"]==":"
assert d["lookup"]["lazy_initialization"] is True
assert d["cross_distribution"]["consumer_instruction_range_byte_identical"] is True
print("native addon consumer: PASS")
