#!/usr/bin/env python3
from pathlib import Path
import hashlib, json, struct
ROOT=Path(__file__).resolve().parents[2]
EXE=Path('/mnt/data/native_src/wccmmutl.exe')
E=ROOT/'Librarium/Evidence/native_bin_console_rendering_0031.json'
d=json.loads(E.read_text())
assert hashlib.sha256(EXE.read_bytes()).hexdigest()==d['source']['sha256']
# Exact machine-code anchors for mapper and ordered API thunks.
b=EXE.read_bytes()
# PE RVA 0xE9B5 maps to file offset 0xCBB5 for this image.
assert bytes.fromhex('55 8B EC 8A 45 08 66 83 3D 3C 70 41 00 00') in b
assert bytes.fromhex('E8 CB 3A 00 00') in b  # WriteConsoleOutputCharacterA thunk call
assert bytes.fromhex('E8 B9 3A 00 00') in b  # WriteConsoleOutputAttribute thunk call
print('native_bin_console_rendering_0031: canonical')
