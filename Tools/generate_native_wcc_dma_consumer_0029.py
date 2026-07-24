#!/usr/bin/env python3
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'Librarium/Evidence/native_wcc_dma_consumer_0029.json'
d=json.loads(p.read_text(encoding='utf-8'))
assert d['lookup_contract']['identifier']['copy_bytes']==8
assert d['lookup_contract']['token']['forced_nul_offset']==49
print('native_wcc_dma_consumer_0029: generated evidence is canonical')
