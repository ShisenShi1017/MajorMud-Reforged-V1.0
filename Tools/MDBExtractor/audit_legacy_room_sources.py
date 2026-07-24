#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

def sha(p):
 h=hashlib.sha256();
 with p.open('rb') as f:
  for c in iter(lambda:f.read(1<<20),b''): h.update(c)
 return h.hexdigest()

def norm(s): return ' '.join((s or '').split()).strip()
def variants(s):
 s=norm(s)
 if not s:return []
 out=[]
 for enc in ('cp1252','latin1','utf-8'):
  try: out.append(s.encode(enc))
  except: pass
 return out

def present(blob,s): return any(v in blob for v in variants(s))

def main():
 ap=argparse.ArgumentParser(); ap.add_argument('--repo',default='.'); ap.add_argument('--out-dir',default='Reports/DataDecode'); a=ap.parse_args()
 r=Path(a.repo).resolve(); out=r/a.out_dir; out.mkdir(parents=True,exist_ok=True)
 files={
 'legacy_wccmp001':r/'ImportedDatabases/NativeRuntime/WCCMP001.DAT',
 'current_wccmp002':r/'ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat',
 'wbbmp002':r/'ImportedDatabases/WCCLabeledWBB/Extracted/WBBMP002.DAT'}

 def string_index(path):
  data=path.read_bytes(); found=set(); cur=bytearray()
  for ch in data:
   if 32 <= ch <= 126 or ch >= 160: cur.append(ch)
   else:
    if len(cur)>=8:
     try: found.add(norm(cur.decode('cp1252')))
     except: pass
    cur.clear()
  if len(cur)>=8:
   try: found.add(norm(cur.decode('cp1252')))
   except: pass
  return found
 indexes={k:string_index(p) for k,p in files.items()}
 omni=sorted((r/'ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/rooms').glob('*.json'))
 rows=[]
 for p in omni:
  x=json.loads(p.read_text(encoding='utf-8'))
  desc=' '.join(norm(x.get(f'desc{i}')) for i in range(1,8) if norm(x.get(f'desc{i}')))
  name=norm(x.get('name'))
  hits={k:{'name':name in idx,'description':desc in idx, 'description_lines':sum(norm(x.get(f'desc{i}')) in idx for i in range(1,8) if norm(x.get(f'desc{i}')))} for k,idx in indexes.items()}
  cls='not_binary_correlated'
  if hits['legacy_wccmp001']['description'] and not hits['current_wccmp002']['description']: cls='legacy_description_only'
  elif hits['current_wccmp002']['description'] and not hits['legacy_wccmp001']['description']: cls='current_description_only'
  elif hits['current_wccmp002']['description'] and hits['legacy_wccmp001']['description']: cls='description_in_both'
  elif hits['legacy_wccmp001']['description_lines'] and not hits['current_wccmp002']['description_lines']: cls='legacy_partial_description_only'
  elif hits['current_wccmp002']['description_lines'] and hits['legacy_wccmp001']['description_lines']: cls='partial_description_in_both'
  rows.append({'id':f"room.map{x.get('map_id')}.{x.get('room_number')}",'map_number':x.get('map_id'),'room_number':x.get('room_number'),'name':name,'description':desc,'classification':cls,'binary_hits':hits,'omniverse_source':str(p.relative_to(r))})
 from collections import Counter
 c=Counter(z['classification'] for z in rows)
 report={'audit':'room legacy/current native-source cross-check 0180','policy':{'WCCMP002':'current 1.11p primary','WCCMP001':'legacy supplemental; legacy-only records preserved when corroborated','WBBMP002':'secondary native comparison','MMExplorer':'authoritative structured room graph','OmniVerse':'corroborating room text and native field reference','Redux':'required corroborating source but no Redux artifact was found in this repository'},'files':{k:{'path':str(p.relative_to(r)),'size':p.stat().st_size,'sha256':sha(p)} for k,p in files.items()},'omniverse_room_files':len(omni),'classification_counts':dict(c),'legacy_candidates':[z for z in rows if z['classification'].startswith('legacy_')],'all_room_correlations':rows}
 (out/'room_legacy_source_audit_0180.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 md=['# Room Legacy Source Audit 0180','','## Authority','', '- `WCCMP002.DAT` remains the current 1.11p primary native map database.','- `WCCMP001.DAT` is now decoded as a legacy supplemental source; legacy-only evidence is retained rather than discarded.','- `WBBMP002.DAT` is a secondary native comparison source.','- MMExplorer remains the authoritative structured room graph.','- OmniVerse is used as corroborating text/native-field evidence.','- No Redux artifact was present in the supplied repository, so Redux verification remains an explicit source blocker.','', '## Results','',f"- OmniVerse room records checked: **{len(omni):,}**"]
 for k,v in sorted(c.items()): md.append(f'- {k}: **{v:,}**')
 md += ['','## Runtime disposition','','This pass does not overwrite current WCCMP002 room records. Legacy-only candidates are emitted with provenance for promotion only when corroborated by MMExplorer, OmniVerse, Redux, WBB, or another native source.']
 (out/'room_legacy_source_audit_0180.md').write_text('\n'.join(md)+'\n',encoding='utf-8')
 print(json.dumps({'rooms':len(rows),'counts':dict(c),'legacy_candidates':len(report['legacy_candidates'])},indent=2))
if __name__=='__main__': main()
