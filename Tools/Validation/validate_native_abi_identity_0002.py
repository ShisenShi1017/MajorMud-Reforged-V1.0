#!/usr/bin/env python3
import hashlib,json,re,sys
from pathlib import Path
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')
p=root/'Librarium/NativeEvidence/native_abi_identity_closure_0002.json'
d=json.loads(p.read_text())
assert d['schema']=='majormud.native.abi-identity.v1'
assert len(d['modules'])==3
for m in d['modules']:
 assert m['abi_equal'] is True
 assert m['wcc']['size']==m['wbb']['size']
 assert m['wcc']['export_count']==m['wbb']['export_count']
 assert m['difference_range_count']==len(m['difference_ranges'])
 assert m['differing_byte_count']>0
cs=(root/'Apps/Source/Reforged.Server/NativeModuleAbiRegistry.cs').read_text()
for token in ['NativeModuleAbiRegistry','ValidateArtifact','NATIVE-ABI-0002-01']:
 assert token in cs
print('native abi identity 0002 validation: PASS')
