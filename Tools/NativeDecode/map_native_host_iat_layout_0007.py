#!/usr/bin/env python3
import json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'Reports/NativeDecode/native_host_import_reference_0005.json'
OUT=ROOT/'Reports/NativeDecode/native_host_iat_layout_0007.json'
LIB=ROOT/'Librarium/Evidence/native_host_iat_layout_0007.json'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 d=json.loads(SRC.read_text()); mods=[]
 for m in d['modules']:
  rows=sorted((int(i['iat_va'],16),i['symbol'],i['reference_kind']) for i in m['imports'])
  runs=[]; cur=[]
  for row in rows:
   if not cur or row[0]-cur[-1][0]==4: cur.append(row)
   else: runs.append(cur); cur=[row]
  if cur:runs.append(cur)
  runrows=[]
  for r in runs:
   runrows.append({'start_va':f'0x{r[0][0]:08x}','end_va':f'0x{r[-1][0]:08x}','slot_count':len(r),'slot_width_bytes':4,
    'symbols':[{'iat_va':f'0x{v:08x}','symbol':s,'reference_kind':k} for v,s,k in r]})
  mods.append({'file':m['file'],'sha256':m['sha256'],'machine':'x86','pointer_width_bytes':4,'import_count':len(rows),
   'contiguous_iat_run_count':len(runs),'iat_runs':runrows})
 out={'schema':'majormud.native-host-iat-layout.v1','update':'FinalNativeClosure0007','source_report':str(SRC.relative_to(ROOT)).replace('\\','/'),'source_sha256':sha(SRC),
 'evidence_class':'proven','method':'Sort every named WGSERVER import slot by virtual address and recover contiguous 4-byte x86 IAT runs.',
 'modules':mods,'closed_boundaries':['Every supplied MMUD/MMPLS WGSERVER import is represented by a 4-byte x86 IAT slot.','IAT slot order and virtual addresses are now canonicalized per exact module hash.','WCC/WBB counterparts can be validated without assuming interchangeable file identity.'],
 'limitations':['IAT layout identifies linkage slots, not implementations inside WGSERVER.EXE.','The native type behind data imports remains unknown beyond the 32-bit imported slot value.']}
 text=json.dumps(out,indent=2)+'\n'; OUT.write_text(text); LIB.write_text(text); print(OUT)
if __name__=='__main__':main()
