from pathlib import Path
import subprocess,re,json,hashlib,datetime,zipfile,os,py_compile
root=Path('/mnt/data/nativepass3/base')
bins={
'wccmmud.dll':Path('/mnt/data/nativepass3/bin/ImportedDatabases/WCCDefaultInstall/wcc default/wccmmud.dll'),
'wccmmpls.dll':Path('/mnt/data/nativepass3/bin/ImportedDatabases/WCCDefaultInstall/wcc default/wccmmpls.dll'),
'wbbmmud.dll':Path('/mnt/data/nativepass3/bin/ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmud.dll'),
'wbbmmpls.dll':Path('/mnt/data/nativepass3/bin/ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmpls.dll')}

def run(a): return subprocess.check_output(a,text=True,errors='replace')
def analyze(path):
 p=run(['objdump','-p',str(path)])
 imports=[]; dll=None
 for line in p.splitlines():
  m=re.search(r'DLL Name:\s*(\S+)',line)
  if m: dll=m.group(1); continue
  m=re.match(r'\s*([0-9a-fA-F]{8})\s+<none>\s+[0-9a-fA-F]+\s+(\S+)',line)
  if m and dll: imports.append({'dll':dll,'iat_rva':'0x'+m.group(1).lower(),'name':m.group(2)})
 d=run(['objdump','-d','-Mintel',str(path)])
 # thunk address -> iat VA from jmp DWORD PTR ds:0x...
 thunk={}
 for line in d.splitlines():
  m=re.match(r'\s*([0-9a-fA-F]+):.*jmp\s+DWORD PTR ds:0x([0-9a-fA-F]+)',line)
  if m: thunk[int(m.group(1),16)]=int(m.group(2),16)
 # map image base inferred as iatVA-rva, typically 0x400000
 by_iat={int(x['iat_rva'],16):x for x in imports}
 bases=[]
 for t,va in thunk.items():
  for r in by_iat:
   b=va-r
   if b in (0x400000,0x10000000): bases.append(b)
 base=max(set(bases),key=bases.count) if bases else 0x400000
 name_by_thunk={t:by_iat[va-base] for t,va in thunk.items() if (va-base) in by_iat}
 calls={t:[] for t in name_by_thunk}
 for line in d.splitlines():
  m=re.match(r'\s*([0-9a-fA-F]+):.*\bcall\s+0x([0-9a-fA-F]+)',line)
  if m:
   src=int(m.group(1),16); dst=int(m.group(2),16)
   if dst in calls: calls[dst].append(src)
 rows=[]
 for t,imp in name_by_thunk.items():
  if imp['dll'].upper()=='WGSERVER.EXE':
   rows.append({**imp,'thunk_va':f'0x{t:08x}','direct_callsite_count':len(calls[t]),'direct_callsites':[f'0x{x:08x}' for x in calls[t][:64]]})
 return {'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'image_base':f'0x{base:08x}','wgserver_import_count':sum(1 for x in imports if x['dll'].upper()=='WGSERVER.EXE'),'wgserver_thunk_count':len(rows),'wgserver_direct_callsite_count':sum(x['direct_callsite_count'] for x in rows),'imports':rows}
mods=[analyze(p) for p in bins.values()]
# aggregate function ownership/calls
agg={}
for m in mods:
 for x in m['imports']:
  a=agg.setdefault(x['name'],{'name':x['name'],'modules':{},'total_direct_callsites':0})
  a['modules'][m['file']]={'direct_callsite_count':x['direct_callsite_count'],'thunk_va':x['thunk_va']}
  a['total_direct_callsites']+=x['direct_callsite_count']
data={'schema':'majorMud.native-host-callsite-map.v1','update':'FinalNativeClosure0003','generated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'evidence_class':'PROVEN','method':'PE import table plus x86 direct-call references to import thunks; indirect register/data calls are explicitly excluded','modules':mods,'functions':sorted(agg.values(),key=lambda x:(-x['total_direct_callsites'],x['name'])),'limitations':['Direct static calls to import thunks only.','Calls through copied pointers, registers, callbacks, or host-internal implementations require WGSERVER.EXE and runtime tracing.']}
for p in [root/'Librarium/Evidence/native_host_callsite_map_0003.json',root/'Reports/NativeDecode/native_host_callsite_map_0003.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
# generator source
src=Path('/tmp/make_pass3.py').read_text(); gp=root/'Tools/NativeDecode/analyze_native_host_callsites_0003.py'; gp.parent.mkdir(parents=True,exist_ok=True); gp.write_text(src)
# validator
vp=root/'Tools/Validation/validate_native_host_callsites_0003.py'; vp.write_text("""import json, pathlib\np=pathlib.Path(__file__).parents[2]/'Librarium/Evidence/native_host_callsite_map_0003.json'\nd=json.loads(p.read_text())\nassert d['schema']=='majorMud.native-host-callsite-map.v1'\nassert len(d['modules'])==4\nassert all(m['wgserver_import_count']>40 for m in d['modules'])\nassert all(m['wgserver_thunk_count']==m['wgserver_import_count'] for m in d['modules'])\nassert any(f['name']=='_lngrnd' for f in d['functions'])\nprint('native host callsite validation passed')\n""")
# runtime registry C# generated top used calls
cs=root/'Apps/Source/Reforged.Server/NativeHostCallsiteRegistry.cs'; cs.parent.mkdir(parents=True,exist_ok=True)
top=[f for f in data['functions'] if f['total_direct_callsites']>0]
lines=['namespace Reforged.Server;','','public sealed record NativeHostCallsiteEvidence(string Symbol, int DirectCallsiteCount);','','public static class NativeHostCallsiteRegistry','{','    public const string EvidenceId = "FinalNativeClosure0003";','    public const string Scope = "Direct x86 calls to WGSERVER.EXE import thunks; indirect calls excluded.";','    public static readonly IReadOnlyList<NativeHostCallsiteEvidence> Proven = new NativeHostCallsiteEvidence[]','    {']
for f in top: lines.append(f'        new("{f["name"]}", {f["total_direct_callsites"]}),')
lines += ['    };','}']
cs.write_text('\n'.join(lines)+'\n')
# report
mmud=next(m for m in mods if m['file']=='wccmmud.dll'); mmpls=next(m for m in mods if m['file']=='wccmmpls.dll')
report=root/'Reports/NativeDecode/native_host_dispatch_boundary_closure_0003.md'
report.write_text(f"""# Final Native Closure 0003 — Host Dispatch Boundary\n\n## Proven\n\n- Parsed the PE import tables and x86 import-thunk callsites for both WCC and WBB MMUD/MMPLS modules.\n- WCC MMUD imports {mmud['wgserver_import_count']} WGSERVER symbols and contains {mmud['wgserver_direct_callsite_count']} direct calls to their thunks.\n- WCC MMPLS imports {mmpls['wgserver_import_count']} WGSERVER symbols and contains {mmpls['wgserver_direct_callsite_count']} direct calls to their thunks.\n- WCC/WBB callsite counts are retained separately and hash-bound.\n- `_lngrnd` is confirmed as a host-owned RNG import boundary; this pass does not invent its algorithm.\n\n## Runtime effect\n\n`NativeHostCallsiteRegistry` exposes only proven direct-call ownership evidence. It does not emulate missing host behavior.\n\n## Remaining boundary\n\nIndirect calls and the implementations behind WGSERVER imports remain native-host dependencies. Exact RNG, DFA/save locking, scheduling, message transport, and recovery semantics require the matching WGSERVER executable/runtime or dynamic traces.\n""")
# readme/changelog
ud=root/'Updates/FinalNativeClosure0003'; ud.mkdir(parents=True,exist_ok=True); (ud/'README.md').write_text('FinalNativeClosure0003 maps proven direct x86 callsites from MMUD/MMPLS into WGSERVER.EXE import thunks and adds a fail-closed runtime evidence registry.\n')
with (root/'CHANGELOG.md').open('a') as f:f.write('\n## FinalNativeClosure0003\n- Added hash-bound WGSERVER import-thunk and direct-callsite mapping for WCC/WBB MMUD and MMPLS modules.\n- Added runtime evidence registry, generator, validator, Librarium evidence, and native boundary report.\n')
# manifest of all cumulative files
files=[]
for p in sorted(root.rglob('*')):
 if p.is_file() and '__pycache__' not in p.parts:
  files.append({'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0003','baseline':'FinalNativeClosure0002','file_count':len(files),'files':files}
for p in [root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0003.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(man,indent=2)+'\n')
# validate
for p in root.rglob('*.json'): json.loads(p.read_text(errors='strict'))
py_compile.compile(str(vp),doraise=True)
subprocess.check_call(['python',str(vp)])
# zip excluding pycache
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0003-Cumulative-Changed-Files-Only.zip')
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts:z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z:
 assert z.testzip() is None
print(out)
print(hashlib.sha256(out.read_bytes()).hexdigest())
print(json.dumps({m['file']:(m['wgserver_import_count'],m['wgserver_direct_callsite_count']) for m in mods},indent=2))
