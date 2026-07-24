from __future__ import annotations
import json, shutil, hashlib, datetime as dt, zipfile, py_compile, subprocess, re
from pathlib import Path
from collections import Counter, defaultdict

srczip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0011-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass12'); root=work/'base12'
if work.exists(): shutil.rmtree(work)
root.mkdir(parents=True)
with zipfile.ZipFile(srczip) as z: z.extractall(root)

binroot=Path('/mnt/data/native_pass9/repo')
mods={
'wccmmud.dll':'ImportedDatabases/WCCDefaultInstall/wcc default/wccmmud.dll',
'wccmmpls.dll':'ImportedDatabases/WCCDefaultInstall/wcc default/wccmmpls.dll',
'wbbmmud.dll':'ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmud.dll',
'wbbmmpls.dll':'ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmpls.dll'}

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def disasm(p):
    txt=subprocess.check_output(['objdump','-d','-Mintel',str(p)],text=True,errors='replace')
    out=[]; rx=re.compile(r'^\s*([0-9a-fA-F]+):\s+(?:[0-9a-fA-F]{2}\s+)+\s*(.*?)\s*$')
    for line in txt.splitlines():
        m=rx.match(line)
        if m: out.append({'va':int(m.group(1),16),'text':m.group(2).strip()})
    return out

prior=json.loads((root/'Reports/NativeDecode/native_host_callsite_map_0003.json').read_text())

def classify_operand(op:str)->str:
    op=op.strip().lower()
    if re.fullmatch(r'0x0+|0',op): return 'zero_or_null_immediate'
    if re.fullmatch(r'-?(?:0x[0-9a-f]+|\d+)',op): return 'nonzero_immediate_constant'
    if re.fullmatch(r'(eax|ebx|ecx|edx|esi|edi|ebp|esp|ax|bx|cx|dx|al|ah|bl|bh|cl|ch|dl|dh)',op): return 'register_value'
    if '[' in op and ']' in op:
        if op.startswith('lea '): return 'effective_address'
        return 'memory_value_or_pointer'
    if re.match(r'^(offset\s+)?[0-9a-z_.$?@]+$',op): return 'symbol_or_address_constant'
    return 'other_expression'

def bounded_pushes(ins, idx):
    pushes=[]
    # walk backward up to 16 instructions or a control transfer / stack reset
    for k in range(idx-1,max(-1,idx-17),-1):
        t=ins[k]['text'].strip(); tl=t.lower()
        if re.match(r'^(ret|jmp|j[a-z]+|call|leave|int)\b',tl): break
        if re.match(r'^(add|sub)\s+esp\s*,',tl):
            # stack frame adjustment is a boundary unless this is a tiny local allocation directly adjacent
            break
        m=re.match(r'^push\s+(.+)$',t,re.I)
        if m:
            op=m.group(1).strip()
            pushes.append({'va':f"0x{ins[k]['va']:08x}",'instruction':t,'operand':op,'source_class':classify_operand(op)})
        elif re.match(r'^mov\s+\[esp(?:\+[^\]]+)?\]\s*,',tl):
            # explicit stack argument store; retain as separate class
            pushes.append({'va':f"0x{ins[k]['va']:08x}",'instruction':t,'operand':t.split(',',1)[1].strip(),'source_class':'explicit_stack_store'})
    pushes.reverse()
    return pushes

module_rows=[]
for name,rel in mods.items():
    p=binroot/rel; ins=disasm(p)
    pm=next(m for m in prior['modules'] if m['file']==name)
    rows=[]
    for imp in pm['imports']:
        thunk=int(imp['thunk_va'],16); calls=[]; hist=Counter(); arity=Counter(); pos=defaultdict(Counter)
        for i,x in enumerate(ins):
            m=re.match(r'^call\s+0x([0-9a-fA-F]+)\b',x['text'])
            if not m or int(m.group(1),16)!=thunk: continue
            args=bounded_pushes(ins,i)
            arity[len(args)]+=1
            for n,a in enumerate(args):
                hist[a['source_class']]+=1; pos[str(n)][a['source_class']]+=1
            if len(calls)<4:
                calls.append({'callsite_va':f"0x{x['va']:08x}",'bounded_arguments':args})
        if arity:
            rows.append({'symbol':imp['name'],'direct_callsite_count':sum(arity.values()),'bounded_argument_count_histogram':dict(sorted(arity.items())),'argument_source_histogram':dict(hist),'position_source_histograms':{k:dict(v) for k,v in sorted(pos.items(),key=lambda kv:int(kv[0]))},'examples':calls})
    module_rows.append({'file':name,'sha256':sha(p),'imports':rows,'direct_callsite_count':sum(r['direct_callsite_count'] for r in rows)})

agg={}
for m in module_rows:
    for r in m['imports']:
        a=agg.setdefault(r['symbol'],{'symbol':r['symbol'],'modules':{},'combined_argument_source_histogram':Counter(),'combined_argument_count_histogram':Counter(),'total_direct_callsites':0})
        a['modules'][m['file']]={'argument_source_histogram':r['argument_source_histogram'],'bounded_argument_count_histogram':r['bounded_argument_count_histogram'],'position_source_histograms':r['position_source_histograms']}
        a['combined_argument_source_histogram'].update(r['argument_source_histogram'])
        a['combined_argument_count_histogram'].update({int(k):v for k,v in r['bounded_argument_count_histogram'].items()})
        a['total_direct_callsites']+=r['direct_callsite_count']
syms=[]
for a in agg.values():
    a['combined_argument_source_histogram']=dict(a['combined_argument_source_histogram'])
    a['combined_argument_count_histogram']={str(k):v for k,v in sorted(a['combined_argument_count_histogram'].items())}
    pairs=[]
    for l,r in [('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]:
        if l in a['modules'] or r in a['modules']:
            pairs.append({'pair':f'{l}:{r}','matching_profile':a['modules'].get(l)==a['modules'].get(r)})
    a['distribution_parity']=pairs
    counts=[int(k) for k,v in a['combined_argument_count_histogram'].items() if v]
    a['stable_bounded_argument_count']=counts[0] if len(counts)==1 else None
    a['has_zero_or_null_argument_evidence']=a['combined_argument_source_histogram'].get('zero_or_null_immediate',0)>0
    a['has_address_or_pointer_argument_evidence']=sum(a['combined_argument_source_histogram'].get(k,0) for k in ('memory_value_or_pointer','symbol_or_address_constant','effective_address'))>0
    syms.append(a)
syms.sort(key=lambda x:(-x['total_direct_callsites'],x['symbol']))
summary={
'direct_callsites_analyzed':sum(m['direct_callsite_count'] for m in module_rows),
'host_symbols_analyzed':len(syms),
'symbols_with_stable_bounded_argument_count':sum(s['stable_bounded_argument_count'] is not None for s in syms),
'symbols_with_zero_or_null_argument_evidence':sum(s['has_zero_or_null_argument_evidence'] for s in syms),
'symbols_with_address_or_pointer_argument_evidence':sum(s['has_address_or_pointer_argument_evidence'] for s in syms),
'all_distribution_profiles_match':all(all(p['matching_profile'] for p in s['distribution_parity']) for s in syms),
'argument_source_totals':dict(sum((Counter(mr['argument_source_histogram']) for m in module_rows for mr in m['imports']),Counter()))
}
data={'schema':'majormud.native-host-argument-source.v1','update':'FinalNativeClosure0012','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),'evidence_class':'PROVEN_STATIC_BOUNDED_ARGUMENT_SOURCE','method':'For every direct call to a proven WGSERVER import thunk, walk backward through a bounded straight-line instruction window and classify observed PUSH or explicit stack-store argument sources.','interpretation_limits':['The bounded argument list is a caller-side footprint and may omit arguments prepared outside the window, register arguments, hidden arguments, or shared stack setup.','Memory and symbol operands prove pointer/address-like or loaded-value usage only; they do not recover exact pointee types.','Immediate zero is evidence of a null/zero machine word, not proof of a pointer parameter.','Exact prototypes and host implementations remain unavailable without WGSERVER binaries, symbols, headers, or dynamic traces.'],'summary':summary,'modules':module_rows,'symbols':syms}
for p in [root/'Librarium/Evidence/native_host_argument_sources_0012.json',root/'Reports/NativeDecode/native_host_argument_sources_0012.json']:
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_argument_sources_0012.py').write_text(Path(__file__).read_text())

cs=root/'Apps/Source/Reforged.Server/NativeHostArgumentSourceRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostArgumentSourceEvidence(','    string Symbol,','    int DirectCallsiteCount,','    int? StableBoundedArgumentCount,','    bool HasZeroOrNullArgumentEvidence,','    bool HasAddressOrPointerArgumentEvidence,','    string SourceKinds);','','public static class NativeHostArgumentSourceRegistry','{','    public const string EvidenceId = "FinalNativeClosure0012";','    public const string Scope = "Bounded caller-side argument-source evidence only; exact native prototypes remain fail-closed.";','','    public static readonly IReadOnlyList<NativeHostArgumentSourceEvidence> Proven = new NativeHostArgumentSourceEvidence[]','    {']
for s in syms:
    kinds=','.join(sorted(k for k,v in s['combined_argument_source_histogram'].items() if v))
    stable='null' if s['stable_bounded_argument_count'] is None else str(s['stable_bounded_argument_count'])
    lines.append(f'        new("{s["symbol"]}", {s["total_direct_callsites"]}, {stable}, {str(s["has_zero_or_null_argument_evidence"]).lower()}, {str(s["has_address_or_pointer_argument_evidence"]).lower()}, "{kinds}"),')
lines += ['    };','}']; cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_argument_sources_0012.py'
val.write_text('''#!/usr/bin/env python3\nimport json, pathlib\nr=pathlib.Path(__file__).parents[2]\nd=json.loads((r/"Librarium/Evidence/native_host_argument_sources_0012.json").read_text())\nassert d["schema"]=="majormud.native-host-argument-source.v1"\nassert len(d["modules"])==4\nassert d["summary"]["direct_callsites_analyzed"]==14056\nassert d["summary"]["host_symbols_analyzed"]==121\nassert d["summary"]["all_distribution_profiles_match"]\nassert all(all(p["matching_profile"] for p in s["distribution_parity"]) for s in d["symbols"])\nassert any(s["has_zero_or_null_argument_evidence"] for s in d["symbols"])\nassert any(s["has_address_or_pointer_argument_evidence"] for s in d["symbols"])\nprint("native host argument source validation passed")\n''')
report=root/'Reports/NativeDecode/native_host_argument_source_closure_0012.md'
report.write_text(f'''# Final Native Closure 0012 — Host Argument-Source Footprints\n\n## Proven\n\n- Analyzed all {summary['direct_callsites_analyzed']} direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.\n- Classified bounded caller-side argument sources for {summary['host_symbols_analyzed']} imported host symbols.\n- {summary['symbols_with_stable_bounded_argument_count']} symbols have one stable bounded argument count across all observed direct callsites.\n- {summary['symbols_with_zero_or_null_argument_evidence']} symbols receive at least one proven zero/null machine-word argument.\n- {summary['symbols_with_address_or_pointer_argument_evidence']} symbols receive at least one address-, symbol-, or memory-derived argument.\n- WCC/WBB counterpart argument-source profiles match exactly.\n\n## Runtime effect\n\n`NativeHostArgumentSourceRegistry` exposes only proven caller-side source classes and stable bounded counts. Exact parameter types, ownership, pointee layouts, and hidden/register arguments remain fail-closed.\n\n## Remaining boundary\n\nExact prototypes and implementations still require matching Worldgroup binaries, symbols, headers, or dynamic traces.\n''')
ud=root/'Updates/FinalNativeClosure0012'; ud.mkdir(parents=True,exist_ok=True); (ud/'README.md').write_text('FinalNativeClosure0012 classifies bounded caller-side argument sources for every direct WGSERVER import callsite.\n')
with (root/'CHANGELOG.md').open('a') as f:
    f.write('\n## FinalNativeClosure0012\n- Added bounded direct-call argument-source evidence for WGSERVER imports.\n- Added fail-closed runtime registry, analyzer, validator, Librarium evidence, report, and cumulative manifests.\n')

for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_argument_sources_0012.py'),doraise=True)
py_compile.compile(str(val),doraise=True)
subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')

manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0012.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inventory(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0012','baseline':'FinalNativeClosure0011','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0012-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
