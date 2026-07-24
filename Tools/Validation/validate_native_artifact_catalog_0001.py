#!/usr/bin/env python3
import hashlib,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'Librarium/NativeEvidence/native_artifact_catalog_0001.json'
errors=[]
try: data=json.loads(p.read_text())
except Exception as e: print(f'FAIL: {e}');sys.exit(1)
for r in data.get('records',[]):
 f=ROOT/r['path']
 if not f.is_file(): errors.append(f"missing {r['path']}");continue
 h=hashlib.sha256(f.read_bytes()).hexdigest()
 if h!=r['sha256']: errors.append(f"hash mismatch {r['path']}")
 if f.stat().st_size!=r['size']: errors.append(f"size mismatch {r['path']}")
for name in ('mmud.dll','mmpls.dll','mmutl.exe'):
 ps=[x for x in data.get('wccWbbPairs',[]) if x['canonicalName']==name]
 if len(ps)!=1 or not ps[0]['bothPresent'] or not ps[0]['sameSize']:
  errors.append(f'expected same-size WCC/WBB pair missing: {name}')
if data.get('summary',{}).get('peArtifactCount')!=6: errors.append('expected six native PE artifacts')
if errors:
 print('FAIL');print('\n'.join(errors));sys.exit(1)
print('PASS: native artifact catalog hashes, sizes, paired modules, and PE inventory validated')
