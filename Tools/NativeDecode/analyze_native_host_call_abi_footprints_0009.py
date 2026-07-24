#!/usr/bin/env python3
from __future__ import annotations
import datetime as dt, hashlib, json, re, shutil, subprocess, zipfile, py_compile
from pathlib import Path

work=Path('/mnt/data/native_pass9')
root=work/'base9'
if root.exists(): shutil.rmtree(root)
shutil.copytree(work/'update8', root)
binroot=work/'repo'
mods={
'wccmmud.dll':'ImportedDatabases/WCCDefaultInstall/wcc default/wccmmud.dll',
'wccmmpls.dll':'ImportedDatabases/WCCDefaultInstall/wcc default/wccmmpls.dll',
'wbbmmud.dll':'ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmud.dll',
'wbbmmpls.dll':'ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmpls.dll'}

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def disasm(p):
    txt=subprocess.check_output(['objdump','-d','-Mintel',str(p)],text=True,errors='replace')
    out=[]
    rx=re.compile(r'^\s*([0-9a-fA-F]+):\s+(?:[0-9a-fA-F]{2}\s+)+\s*(.*?)\s*$')
    for line in txt.splitlines():
        m=rx.match(line)
        if m: out.append({'va':int(m.group(1),16),'text':m.group(2).strip()})
    return out

def footprint(ins, idx):
    setup=[]
    pushes=0
    # Scan bounded basic-block tail preceding call. Preserve raw sequence.
    for j in range(idx-1,max(-1,idx-17),-1):
        t=ins[j]['text'].lower()
        if re.match(r'^(?:call|ret|jmp|j[a-z]+|loop|leave|int)\b',t): break
        if re.match(r'^push\b',t): pushes += 1
        setup.append({'va':f"0x{ins[j]['va']:08x}",'instruction':ins[j]['text']})
        # A prior stack cleanup or frame boundary is a conservative stop.
        if re.match(r'^(?:add|sub)\s+esp\b',t) or re.match(r'^mov\s+esp\s*,',t): break
    setup.reverse()
    post=[]; cleanup=None
    for j in range(idx+1,min(len(ins),idx+5)):
        t=ins[j]['text'].lower(); post.append({'va':f"0x{ins[j]['va']:08x}",'instruction':ins[j]['text']})
        m=re.match(r'^add\s+esp\s*,\s*(0x[0-9a-f]+|\d+)',t)
        if m: cleanup=int(m.group(1),0); break
        if re.match(r'^(?:call|ret|jmp|j[a-z]+|leave|int)\b',t): break
    return pushes,cleanup,setup,post

prior=json.loads((root/'Librarium/Evidence/native_host_callsite_map_0003.json').read_text())
module_rows=[]
for name,rel in mods.items():
    p=binroot/rel; ins=disasm(p); byva={x['va']:i for i,x in enumerate(ins)}
    pm=next(m for m in prior['modules'] if m['file']==name)
    imports=[]
    for imp in pm['imports']:
        calls=[]
        # prior truncates callsite listing at 64, so recover from disassembly using thunk target.
        thunk=int(imp['thunk_va'],16)
        for i,x in enumerate(ins):
            m=re.match(r'^call\s+0x([0-9a-fA-F]+)\b',x['text'])
            if not m or int(m.group(1),16)!=thunk: continue
            pushes,cleanup,setup,post=footprint(ins,i)
            calls.append({'callsite_va':f"0x{x['va']:08x}",'bounded_pre_call_push_count':pushes,
                          'immediate_caller_stack_cleanup_bytes':cleanup,
                          'setup_window':setup,'post_call_window':post})
        if calls:
            pc={}; cc={}
            for c in calls:
                pc[str(c['bounded_pre_call_push_count'])]=pc.get(str(c['bounded_pre_call_push_count']),0)+1
                k='none_observed' if c['immediate_caller_stack_cleanup_bytes'] is None else str(c['immediate_caller_stack_cleanup_bytes'])
                cc[k]=cc.get(k,0)+1
            imports.append({'symbol':imp['name'],'thunk_va':imp['thunk_va'],'direct_callsite_count':len(calls),
                            'bounded_pre_call_push_histogram':dict(sorted(pc.items(),key=lambda z:int(z[0]))),
                            'immediate_caller_stack_cleanup_histogram':cc,
                            'signature_status':'argument_count_and_calling_convention_not_asserted_from_bounded_static_footprint',
                            'callsites':calls})
    module_rows.append({'file':name,'sha256':sha(p),'direct_host_symbols_with_calls':len(imports),
                        'direct_host_callsite_count':sum(x['direct_callsite_count'] for x in imports),'imports':imports})

# aggregate and parity
agg={}
for m in module_rows:
    for x in m['imports']:
        a=agg.setdefault(x['symbol'],{'symbol':x['symbol'],'modules':{},'observed_push_counts':set(),'observed_cleanup_bytes':set(),'total_direct_callsites':0})
        a['modules'][m['file']]={'direct_callsite_count':x['direct_callsite_count'],'bounded_pre_call_push_histogram':x['bounded_pre_call_push_histogram'],'immediate_caller_stack_cleanup_histogram':x['immediate_caller_stack_cleanup_histogram']}
        a['total_direct_callsites']+=x['direct_callsite_count']
        a['observed_push_counts'].update(map(int,x['bounded_pre_call_push_histogram']))
        for k in x['immediate_caller_stack_cleanup_histogram']:
            if k!='none_observed': a['observed_cleanup_bytes'].add(int(k))
syms=[]
for a in agg.values():
    a['observed_push_counts']=sorted(a['observed_push_counts']); a['observed_cleanup_bytes']=sorted(a['observed_cleanup_bytes'])
    # counterpart parity only compares histograms, not VAs
    pairs=[]
    for left,right in [('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]:
        if left in a['modules'] or right in a['modules']:
            pairs.append({'pair':f'{left}:{right}','matching_footprint_histograms':a['modules'].get(left)==a['modules'].get(right)})
    a['distribution_parity']=pairs; syms.append(a)
syms.sort(key=lambda x:(-x['total_direct_callsites'],x['symbol']))

data={'schema':'majormud.native-host-call-abi-footprint.v1','update':'FinalNativeClosure0009','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),
      'evidence_class':'PROVEN_RAW_INSTRUCTIONS_WITH_CONSERVATIVE_INTERPRETATION',
      'method':'Static x86 direct-call analysis. For every call to a proven WGSERVER import thunk, record a bounded same-block pre-call instruction window, push count, and immediate post-call ADD ESP cleanup when present.',
      'interpretation_limits':['A bounded push count is not asserted to equal the formal parameter count.','No immediate ADD ESP does not by itself prove stdcall because cleanup can be delayed or shared.','Register arguments, hidden arguments, tail calls, indirect calls, and host-internal behavior remain outside this evidence.'],
      'modules':module_rows,'symbols':syms}
for p in [root/'Librarium/Evidence/native_host_call_abi_footprint_0009.json',root/'Reports/NativeDecode/native_host_call_abi_footprint_0009.json']:
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')

# reusable generator
src=Path(__file__).read_text(); gp=root/'Tools/NativeDecode/analyze_native_host_call_abi_footprints_0009.py'; gp.parent.mkdir(parents=True,exist_ok=True); gp.write_text(src)

# C# registry summarizes recovered client-side ABI footprint
cs=root/'Apps/Source/Reforged.Server/NativeHostCallAbiFootprintRegistry.cs'; cs.parent.mkdir(parents=True,exist_ok=True)
lines=['namespace Reforged.Server;','','public sealed record NativeHostCallAbiFootprint(','    string Symbol,','    int DirectCallsiteCount,','    string ObservedBoundedPushCounts,','    string ObservedImmediateCallerCleanupBytes);','','public static class NativeHostCallAbiFootprintRegistry','{','    public const string EvidenceId = "FinalNativeClosure0009";','    public const string Scope = "Proven call-adjacent x86 stack footprints only; formal signatures and calling conventions remain fail-closed.";','','    public static readonly IReadOnlyList<NativeHostCallAbiFootprint> Proven = new NativeHostCallAbiFootprint[]','    {']
for s in syms:
    pushes=','.join(map(str,s['observed_push_counts'])); clean=','.join(map(str,s['observed_cleanup_bytes']))
    lines.append(f'        new("{s["symbol"]}", {s["total_direct_callsites"]}, "{pushes}", "{clean}"),')
lines += ['    };','}']; cs.write_text('\n'.join(lines)+'\n')

# validator
vp=root/'Tools/Validation/validate_native_host_call_abi_footprints_0009.py'
vp.write_text('''#!/usr/bin/env python3\nimport json, pathlib\nr=pathlib.Path(__file__).parents[2]\np=r/"Librarium/Evidence/native_host_call_abi_footprint_0009.json"\nd=json.loads(p.read_text())\nassert d["schema"]=="majormud.native-host-call-abi-footprint.v1"\nassert len(d["modules"])==4\nby={m["file"]:m for m in d["modules"]}\nassert by["wccmmud.dll"]["direct_host_callsite_count"]==by["wbbmmud.dll"]["direct_host_callsite_count"]\nassert by["wccmmpls.dll"]["direct_host_callsite_count"]==by["wbbmmpls.dll"]["direct_host_callsite_count"]\nassert all(all(p["matching_footprint_histograms"] for p in s["distribution_parity"]) for s in d["symbols"])\nassert any(s["symbol"]=="_lngrnd" for s in d["symbols"])\nassert all(sum(i["direct_callsite_count"] for i in m["imports"])==m["direct_host_callsite_count"] for m in d["modules"])\nprint("native host call ABI footprint validation passed")\n''')

report=root/'Reports/NativeDecode/native_host_call_abi_footprint_closure_0009.md'
mmud=next(m for m in module_rows if m['file']=='wccmmud.dll'); mmpls=next(m for m in module_rows if m['file']=='wccmmpls.dll')
cleanup_calls=sum(sum(v for k,v in i['immediate_caller_stack_cleanup_histogram'].items() if k!='none_observed') for m in module_rows for i in m['imports'])
report.write_text(f'''# Final Native Closure 0009 — Host Call ABI Footprints\n\n## Proven\n\n- Recovered bounded pre-call stack-setup windows for every direct WGSERVER import-thunk call in supplied WCC/WBB MMUD and MMPLS modules.\n- WCC MMUD contains {mmud['direct_host_callsite_count']} analyzed direct host callsites; WCC MMPLS contains {mmpls['direct_host_callsite_count']}.\n- WCC/WBB counterpart footprint histograms match for every shared imported symbol.\n- Immediate caller `ADD ESP` cleanup observations are retained per callsite and symbol.\n- Raw instruction windows are preserved so future Worldgroup symbols or binaries can promote these footprints into exact native prototypes.\n\n## Runtime effect\n\n`NativeHostCallAbiFootprintRegistry` exposes the proven MajorMUD-side ABI observations but refuses to claim formal parameter counts or calling conventions.\n\n## Remaining boundary\n\nExact prototypes, register arguments, hidden parameters, delayed cleanup, callback signatures, and host implementations require the matching Worldgroup executable, symbols, or dynamic traces.\n''')

ud=root/'Updates/FinalNativeClosure0009'; ud.mkdir(parents=True,exist_ok=True)
(ud/'README.md').write_text('FinalNativeClosure0009 records the proven call-adjacent x86 stack setup and cleanup footprints for every direct MajorMUD call into WGSERVER imports.\n')
with (root/'CHANGELOG.md').open('a') as f:
    f.write('\n## FinalNativeClosure0009\n- Added complete direct WGSERVER call-adjacent ABI footprint evidence for WCC/WBB MMUD and MMPLS.\n- Added fail-closed runtime registry, generator, validator, Librarium evidence, and closure report.\n')

# validate before manifest
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(vp),doraise=True)
subprocess.check_call(['python',str(vp)])
# simple C# structural checks
assert cs.read_text().count('{')==cs.read_text().count('}')

# cumulative manifests include themselves by two-pass stable convention (prior manifests already follow project convention; generate file inventory excluding manifests then write)
def inventory():
    out=[]
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts:
            out.append({'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)})
    return out
man_paths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0009.json']
for mp in man_paths:
    mp.parent.mkdir(parents=True,exist_ok=True)
    mp.write_text(json.dumps({'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0009','baseline':'FinalNativeClosure0008','file_count':0,'files':[]},indent=2)+'\n')
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0009','baseline':'FinalNativeClosure0008','file_count':len(files),'files':files}
for mp in man_paths: mp.write_text(json.dumps(man,indent=2)+'\n')
# JSON validate again
for p in root.rglob('*.json'): json.loads(p.read_text())

out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0009-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'modules':{m['file']:m['direct_host_callsite_count'] for m in module_rows},'symbols':len(syms),'cleanup_observations':cleanup_calls},indent=2))
