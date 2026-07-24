#!/usr/bin/env python3
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
d=json.loads((ROOT/'Librarium/Evidence/native_bin_screen_normalization_0030.json').read_text(encoding='utf-8'))
assert d['screen_geometry']['cell_count']*d['screen_geometry']['bytes_per_cell']==d['load_contract']['cell_buffer_bytes']
assert d['normalization']['iteration_count']==2000
print('native_bin_screen_normalization_0030: canonical')
