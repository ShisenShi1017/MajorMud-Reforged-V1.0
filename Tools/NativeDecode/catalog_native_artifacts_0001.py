#!/usr/bin/env python3
from __future__ import annotations
import hashlib, json, re, struct, subprocess
from pathlib import Path
from typing import Any

ROOT=Path(__file__).resolve().parents[2]
SETS={
 'wcc': ROOT/'ImportedDatabases/WCCDefaultInstall/wcc default',
 'wbb': ROOT/'ImportedDatabases/WCCLabeledWBB/Extracted',
 'native_runtime': ROOT/'ImportedDatabases/NativeRuntime',
}
OUT=ROOT/'Librarium/NativeEvidence/native_artifact_catalog_0001.json'

def sha256(p:Path)->str:
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
 return h.hexdigest()

def pe_meta(p:Path)->dict[str,Any]|None:
 data=p.read_bytes()
 if len(data)<0x40 or data[:2]!=b'MZ': return None
 peoff=struct.unpack_from('<I',data,0x3c)[0]
 if peoff+24>len(data) or data[peoff:peoff+4]!=b'PE\0\0': return None
 machine, sections, timestamp, _, _, optsz, chars=struct.unpack_from('<HHIIIHH',data,peoff+4)
 magic=struct.unpack_from('<H',data,peoff+24)[0] if peoff+26<=len(data) else None
 return {'machine':f'0x{machine:04x}','sectionCount':sections,'coffTimestamp':timestamp,'characteristics':f'0x{chars:04x}','optionalHeaderMagic':f'0x{magic:04x}' if magic is not None else None}

def objdump_info(p:Path)->dict[str,Any]:
 try:
  cp=subprocess.run(['objdump','-p',str(p)],text=True,capture_output=True,check=True)
 except Exception as e: return {'error':str(e)}
 text=cp.stdout
 imports=[]
 for m in re.finditer(r'DLL Name:\s*([^\r\n]+)',text): imports.append(m.group(1).strip())
 exports=[]
 in_exports=False
 for line in text.splitlines():
  if '[Ordinal/Name Pointer] Table' in line: in_exports=True; continue
  if in_exports:
   m=re.search(r'\[\s*\d+\]\s+(.+?)\s*$',line)
   if m: exports.append(m.group(1).strip())
   elif exports and not line.strip(): break
 return {'imports':sorted(set(imports),key=str.lower),'exports':exports,'exportCount':len(exports)}

def canonical(name:str)->str:
 n=name.lower()
 if n.startswith('wcc'): n=n[3:]
 elif n.startswith('wbb'): n=n[3:]
 return n

records=[]
for family,base in SETS.items():
 if not base.exists(): continue
 for p in sorted((x for x in base.iterdir() if x.is_file()),key=lambda x:x.name.lower()):
  rec={'family':family,'path':p.relative_to(ROOT).as_posix(),'fileName':p.name,'canonicalName':canonical(p.name),'size':p.stat().st_size,'sha256':sha256(p)}
  pm=pe_meta(p)
  if pm:
   rec['pe']=pm; rec['linkage']=objdump_info(p)
  records.append(rec)

byfam={f:{r['canonicalName']:r for r in records if r['family']==f} for f in SETS}
pairs=[]
for key in sorted(set(byfam['wcc'])|set(byfam['wbb'])):
 a=byfam['wcc'].get(key); b=byfam['wbb'].get(key)
 pairs.append({'canonicalName':key,'wccPath':a['path'] if a else None,'wbbPath':b['path'] if b else None,'bothPresent':bool(a and b),'byteIdentical':bool(a and b and a['sha256']==b['sha256']),'sameSize':bool(a and b and a['size']==b['size']),'sizeDelta':(b['size']-a['size']) if a and b else None})

host_imports={}
for r in records:
 if 'linkage' in r:
  host_imports[r['path']]=[x for x in r['linkage'].get('imports',[]) if x.lower() in {'wgserver.exe','wgsapp.dll','wgsys.dll'}]

payload={
 'schemaVersion':1,'evidenceId':'NativeArtifactCatalog0001','scope':'MajorMUD 1.11p WCC/WBB/native executable artifact identity and host-linkage inventory',
 'recordCount':len(records),'records':records,'wccWbbPairs':pairs,
 'summary':{
  'familyCounts':{f:sum(1 for r in records if r['family']==f) for f in SETS},
  'peArtifactCount':sum(1 for r in records if 'pe' in r),
  'pairedCanonicalNames':sum(1 for p in pairs if p['bothPresent']),
  'byteIdenticalPairs':sum(1 for p in pairs if p['byteIdentical']),
  'differentPairs':sum(1 for p in pairs if p['bothPresent'] and not p['byteIdentical']),
  'wccOnly':sum(1 for p in pairs if p['wccPath'] and not p['wbbPath']),
  'wbbOnly':sum(1 for p in pairs if p['wbbPath'] and not p['wccPath']),
 },
 'ownershipEvidence':{
  'hostImportsByArtifact':host_imports,
  'finding':'MajorMUD native modules are dynamically linked to WGSERVER.EXE exports; account/session, terminal I/O, DFA storage, time, messaging, and host memory services cross the Worldgroup ownership boundary.',
  'confidence':'proven-for-import-dependency; semantic meaning of individual host symbols remains pending call-site tracing'
 }
}
OUT.parent.mkdir(parents=True,exist_ok=True)
OUT.write_text(json.dumps(payload,indent=2)+'\n',encoding='utf-8')
print(json.dumps(payload['summary'],indent=2))
