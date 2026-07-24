from pathlib import Path
import subprocess,re,json,hashlib,datetime,zipfile,py_compile,shutil
root=Path('/mnt/data/nativepass5')
bins={
'wccmmud.dll':root/'bin/ImportedDatabases/WCCDefaultInstall/wcc default/wccmmud.dll',
'wccmmpls.dll':root/'bin/ImportedDatabases/WCCDefaultInstall/wcc default/wccmmpls.dll',
'wbbmmud.dll':root/'bin/ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmud.dll',
'wbbmmpls.dll':root/'bin/ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmpls.dll'}
prior=json.loads((root/'Librarium/Evidence/native_host_callsite_map_0003.json').read_text())

def run(a): return subprocess.check_output(a,text=True,errors='replace')
def analyze(path, prior_mod):
    dis=run(['objdump','-d','-Mintel',str(path)])
    lines=dis.splitlines()
    base=int(prior_mod['image_base'],16)
    out=[]
    for imp in prior_mod['imports']:
        iat=base+int(imp['iat_rva'],16); thunk=int(imp['thunk_va'],16)
        refs=[]
        needle=f'{iat:x}'
        for line in lines:
            m=re.match(r'\s*([0-9a-fA-F]+):',line)
            if not m or needle not in line.lower(): continue
            va=int(m.group(1),16)
            # exclude canonical import thunk jmp itself
            if va==thunk and re.search(r'\bjmp\s+DWORD PTR ds:0x'+needle+r'\b',line,re.I): continue
            refs.append({'va':f'0x{va:08x}','instruction':line.split('\t')[-1].strip()})
        direct=imp['direct_callsite_count']
        if direct>0: kind='direct_callable_import'
        elif refs: kind='iat_data_or_indirect_reference'
        else: kind='imported_no_static_code_reference'
        out.append({'symbol':imp['name'],'iat_va':f'0x{iat:08x}','thunk_va':imp['thunk_va'],'direct_callsite_count':direct,'non_thunk_iat_reference_count':len(refs),'non_thunk_iat_references':refs[:64],'reference_kind':kind})
    return {'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'imports':out,
            'counts':{k:sum(1 for x in out if x['reference_kind']==k) for k in ['direct_callable_import','iat_data_or_indirect_reference','imported_no_static_code_reference']}}
mods=[]
for name,path in bins.items():
    pm=next(x for x in prior['modules'] if x['file']==name)
    mods.append(analyze(path,pm))
# aggregate stable classification across distributions
agg={}
for m in mods:
    for x in m['imports']:
        a=agg.setdefault(x['symbol'],{'symbol':x['symbol'],'modules':{},'kinds':set()})
        a['modules'][m['file']]={'reference_kind':x['reference_kind'],'direct_callsite_count':x['direct_callsite_count'],'non_thunk_iat_reference_count':x['non_thunk_iat_reference_count']}
        a['kinds'].add(x['reference_kind'])
rows=[]
for a in agg.values():
    kinds=sorted(a.pop('kinds'))
    a['cross_distribution_classification']=kinds[0] if len(kinds)==1 else 'module_specific_mixed_reference'
    rows.append(a)
data={'schema':'majorMud.native-host-import-reference.v1','update':'FinalNativeClosure0005','generated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'evidence_class':'PROVEN','method':'Static x86 scan of non-thunk references to WGSERVER IAT slots, combined with proven direct import-thunk callsites. Classification proves reference form only, not host implementation semantics.','modules':mods,'symbols':sorted(rows,key=lambda x:x['symbol']),'closed_boundaries':['Distinguishes directly called host functions from host imports consumed through IAT data/indirect-reference paths.','Identifies imports present in the PE table with no static code reference in the supplied module, preventing false claims that every import is executed.','Maintains WCC and WBB evidence independently and hash-bound.'],'limitations':['A non-thunk IAT reference may be a data import, address load, or indirect callable pointer; exact type requires symbols, host binary, or dynamic tracing.','No-static-reference imports may be used by loader initialization, exception metadata, runtime-generated code, or unreachable/dead code.']}
for p in [root/'Librarium/Evidence/native_host_import_reference_0005.json',root/'Reports/NativeDecode/native_host_import_reference_0005.json']:
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data,indent=2)+'\n')
# runtime registry
cs=root/'Apps/Source/Reforged.Server/NativeHostImportReferenceRegistry.cs'
counts={}
for x in rows: counts[x['cross_distribution_classification']]=counts.get(x['cross_distribution_classification'],0)+1
cs.write_text('''namespace Reforged.Server;\n\npublic sealed record NativeHostImportReferenceClass(string Classification, int SymbolCount);\n\npublic static class NativeHostImportReferenceRegistry\n{\n    public const string EvidenceId = "FinalNativeClosure0005";\n    public const string Scope = "Proven static reference form for WGSERVER imports; host implementation remains fail-closed.";\n    public static readonly IReadOnlyList<NativeHostImportReferenceClass> Proven = new NativeHostImportReferenceClass[]\n    {\n'''+''.join(f'        new("{k}", {v}),\n' for k,v in sorted(counts.items()))+'    };\n}\n')
# generator copy
(root/'Tools/NativeDecode/analyze_native_host_import_references_0005.py').write_text(Path('/tmp/make_pass5.py').read_text())
vp=root/'Tools/Validation/validate_native_host_import_references_0005.py'
vp.write_text("""import json,pathlib\nroot=pathlib.Path(__file__).parents[2]\nd=json.loads((root/'Librarium/Evidence/native_host_import_reference_0005.json').read_text())\nassert d['schema']=='majorMud.native-host-import-reference.v1'\nassert len(d['modules'])==4\nfor m in d['modules']:\n assert sum(m['counts'].values())==len(m['imports'])\n assert m['counts']['direct_callable_import']>0\nassert any(x['cross_distribution_classification']=='iat_data_or_indirect_reference' for x in d['symbols'])\nassert any(x['cross_distribution_classification']=='imported_no_static_code_reference' for x in d['symbols'])\nprint('native host import reference validation passed')\n""")
report=root/'Reports/NativeDecode/native_host_import_reference_closure_0005.md'
report.write_text('# Final Native Closure 0005 — Host Import Reference Forms\n\n## Proven\n\n- Classified every WGSERVER import in the supplied MMUD/MMPLS modules by static reference form.\n- Separated direct callable imports from IAT data/indirect references and imports with no static code reference.\n- Prevented the prior direct-call map from being misread as complete execution coverage.\n- Preserved WCC and WBB module hashes and classifications independently.\n\n## Runtime effect\n\n`NativeHostImportReferenceRegistry` exposes only proven reference classes. Indirect/data semantics remain fail-closed.\n\n## Remaining native dependency\n\nThe matching WGSERVER executable, symbols, or runtime traces are required to distinguish host globals from function pointers and to recover implementations and callback behavior.\n')
ud=root/'Updates/FinalNativeClosure0005';ud.mkdir(parents=True,exist_ok=True)
(ud/'README.md').write_text('FinalNativeClosure0005 classifies WGSERVER imports by direct-call, IAT data/indirect, or no-static-reference usage and adds fail-closed runtime evidence.\n')
with (root/'CHANGELOG.md').open('a') as f:f.write('\n## FinalNativeClosure0005\n- Classified all supplied MMUD/MMPLS WGSERVER imports by static reference form.\n- Added runtime registry, generator, validator, Librarium evidence, and native closure report.\n')
# manifests, exclude prior manifest targets and pycache
manifest_paths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0005.json']
for p in manifest_paths:
    if p.exists(): p.unlink()
files=[]
for p in sorted(root.rglob('*')):
    if p.is_file() and '__pycache__' not in p.parts and 'bin' not in p.parts:
        files.append({'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0005','baseline':'FinalNativeClosure0004','file_count':len(files),'files':files}
for p in manifest_paths:p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_import_references_0005.py'),doraise=True)
py_compile.compile(str(vp),doraise=True)
subprocess.check_call(['python',str(vp)])
outzip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0005-Cumulative-Changed-Files-Only.zip')
with zipfile.ZipFile(outzip,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts and 'bin' not in p.parts:z.write(p,p.relative_to(root))
with zipfile.ZipFile(outzip) as z: assert z.testzip() is None
print(outzip)
print(hashlib.sha256(outzip.read_bytes()).hexdigest())
print(json.dumps({m['file']:m['counts'] for m in mods},indent=2))
print(json.dumps(counts,indent=2))
