#!/usr/bin/env python3
import json, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'Reports/NativeDecode/native_host_data_import_classification_0006.json'
d=json.loads(p.read_text())
assert d['schema']=='majormud.native-host-data-import-classification.v1'
assert len(d['modules'])==4
for m in d['modules']:
    assert m['counts']['control_transfer_candidates']==0, m['file']
    for e in m['imports']:
        assert not e['control_transfer_through_iat_observed'], (m['file'],e['symbol'])
        assert e['exact_native_type']=='unknown_without_host_symbols_or_runtime'
assert any(s['symbol']=='_register_agent' for s in d['symbols'])
lib=json.loads((ROOT/'Librarium/Evidence/native_host_data_import_classification_0006.json').read_text())
assert lib==d
cs=(ROOT/'Apps/Source/Reforged.Server/NativeHostDataImportRegistry.cs').read_text()
assert 'RequireExactNativeType' in cs and 'StaticControlTransferObserved' in cs
print('PASS native host data import classification 0006')
