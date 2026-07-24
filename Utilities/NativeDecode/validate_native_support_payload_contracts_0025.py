#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_support_payload_contracts_0025.json").read_text())
assert len(d['bin_screens'])==3
assert all(x['bytes']==4000 and x['cells']==2000 for x in d['bin_screens'])
assert d['wccaddon']['slots']==list(range(1,10))
assert d['wccaddon']['line_count']==9
assert d['wccdma']['line_count']==152 and d['wccdma']['unique_addresses']==152 and d['wccdma']['unique_tokens']==152
assert d['wccdma']['unique_identifiers']==140
print('native support payload contracts: PASS')
