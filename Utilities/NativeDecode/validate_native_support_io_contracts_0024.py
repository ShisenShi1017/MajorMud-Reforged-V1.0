#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_support_io_contracts_0024.json").read_text())
assert d['evidence_id']=='native-support-io-contracts-0024'
assert len(d['contracts'])==4
assets={c['asset'] for c in d['contracts']}
assert {'wccknows.sys','WCCDMA.SYS','WCCBOX.BIN','wccaedit.bin'} <= assets
for c in d['contracts']: assert c['evidence_class']=='PROVEN'
print('native support I/O contracts valid')
