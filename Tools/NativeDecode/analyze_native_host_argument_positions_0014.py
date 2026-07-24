from __future__ import annotations
import json, shutil, hashlib, datetime as dt, zipfile, py_compile, subprocess, re
from pathlib import Path
from collections import Counter, defaultdict

srczip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0013-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass14'); root=work/'base14'
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

def classify_operand(op:str):
    o=op.strip().lower()
    if re.fullmatch(r'0x0+|0',o): return ('zero_or_null','0')
    if re.fullmatch(r'-?(?:0x[0-9a-f]+|\d+)',o): return ('immediate_constant',o)
    if re.fullmatch(r'(e?[abcd]x|e?[sd]i|e?[sb]p|esp|[abcd][lh])',o): return ('register_value',o)
    if o.startswith('offset '): return ('address_or_symbol',o)
    if '[' in o and ']' in o:
        if o.startswith('lea '): return ('address_expression',o)
        return ('memory_value',o)
    if re.search(r'\b(ds|cs|ss|es|fs|gs):',o): return ('memory_or_symbol','')
    return ('other','')

def extract_pushes(ins, idx, max_back=18):
    pushes=[]
    for k in range(idx-1,max(-1,idx-max_back-1),-1):
        t=ins[k]['text'].strip(); tl=t.lower()
        if re.match(r'^(call|ret|jmp|j[a-z]+|leave|enter|int)\b',tl): break
        m=re.match(r'^push\s+(.+)$',tl)
        if m:
            cat,val=classify_operand(m.group(1)); pushes.append({'va':ins[k]['va'],'operand':m.group(1),'category':cat,'value':val})
            continue
        # stop at stack-frame reset or explicit esp mutation, but allow ordinary setup instructions
        if re.match(r'^(add|sub|mov|lea)\s+esp\b',tl): break
    # reverse chronological pushes into callee argument order: nearest push is arg1 on x86 cdecl
    return pushes

module_rows=[]
for name,rel in mods.items():
    p=binroot/rel; ins=disasm(p); index={x['va']:i for i,x in enumerate(ins)}
    mod=next(m for m in prior['modules'] if m['file']==name)
    imports=[]
    for imp in mod['imports']:
        rows=[]
        for c in imp.get('direct_callsites',[]):
            callva=c if isinstance(c,str) else c['callsite_va']; va=int(callva,16); idx=index.get(va)
            if idx is None: continue
            pushes=extract_pushes(ins,idx)
            args=[]
            for pos,push in enumerate(pushes,1): args.append({'position':pos,**push})
            rows.append({'callsite_va':callva,'argument_count':len(args),'arguments':args})
        if rows:
            count_hist=Counter(r['argument_count'] for r in rows)
            pos=defaultdict(lambda:{'categories':Counter(),'values':Counter(),'callsites':0})
            for r in rows:
                for a in r['arguments']:
                    q=pos[a['position']]; q['categories'][a['category']]+=1; q['callsites']+=1
                    if a['value']: q['values'][a['value']]+=1
            positions=[]
            for k in sorted(pos):
                q=pos[k]; cats=dict(q['categories']); vals=dict(q['values'])
                positions.append({'position':k,'observed_callsites':q['callsites'],'category_histogram':cats,'value_histogram':vals,'stable_category':next(iter(cats)) if len(cats)==1 else None,'stable_value':next(iter(vals)) if len(vals)==1 and sum(vals.values())==q['callsites'] else None})
            imports.append({'symbol':imp['name'],'direct_callsite_count':len(rows),'argument_count_histogram':dict(count_hist),'stable_argument_count':next(iter(count_hist)) if len(count_hist)==1 else None,'positions':positions,'examples':rows[:5]})
    module_rows.append({'file':name,'sha256':sha(p),'imports':imports,'direct_callsite_count':sum(i['direct_callsite_count'] for i in imports)})

agg={}
for m in module_rows:
    for r in m['imports']:
        a=agg.setdefault(r['symbol'],{'symbol':r['symbol'],'modules':{},'total_direct_callsites':0})
        profile={'argument_count_histogram':r['argument_count_histogram'],'stable_argument_count':r['stable_argument_count'],'positions':[{'position':p['position'],'category_histogram':p['category_histogram'],'value_histogram':p['value_histogram'],'stable_category':p['stable_category'],'stable_value':p['stable_value']} for p in r['positions']]}
        a['modules'][m['file']]=profile; a['total_direct_callsites']+=r['direct_callsite_count']
syms=[]
for a in agg.values():
    pairs=[]
    for l,r in [('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]:
        if l in a['modules'] or r in a['modules']:
            pairs.append({'pair':f'{l}:{r}','matching_profile':a['modules'].get(l)==a['modules'].get(r)})
    a['distribution_parity']=pairs
    profiles=list(a['modules'].values())
    a['stable_argument_count_across_modules']=profiles[0]['stable_argument_count'] if profiles and all(p['stable_argument_count']==profiles[0]['stable_argument_count'] for p in profiles) else None
    maxpos=max((len(p['positions']) for p in profiles),default=0)
    stable_positions=[]
    for i in range(maxpos):
        vals=[]
        for p in profiles:
            if i < len(p['positions']): vals.append((p['positions'][i]['stable_category'],p['positions'][i]['stable_value']))
        if vals and all(v==vals[0] for v in vals) and vals[0][0] is not None:
            stable_positions.append({'position':i+1,'stable_category':vals[0][0],'stable_value':vals[0][1]})
    a['stable_positions_across_modules']=stable_positions
    a['all_observed_positions_stable']=bool(profiles) and all(all(x['stable_category'] is not None for x in p['positions']) for p in profiles)
    syms.append(a)
syms.sort(key=lambda x:(-x['total_direct_callsites'],x['symbol']))
summary={
'direct_callsites_analyzed':sum(m['direct_callsite_count'] for m in module_rows),
'host_symbols_analyzed':len(syms),
'symbols_with_stable_argument_count_across_modules':sum(s['stable_argument_count_across_modules'] is not None for s in syms),
'symbols_with_at_least_one_stable_argument_position':sum(bool(s['stable_positions_across_modules']) for s in syms),
'symbols_with_all_observed_positions_stable':sum(s['all_observed_positions_stable'] for s in syms),
'zero_or_null_stable_positions':sum(1 for s in syms for p in s['stable_positions_across_modules'] if p['stable_category']=='zero_or_null'),
'immediate_constant_stable_positions':sum(1 for s in syms for p in s['stable_positions_across_modules'] if p['stable_category']=='immediate_constant'),
'address_or_memory_stable_positions':sum(1 for s in syms for p in s['stable_positions_across_modules'] if p['stable_category'] in ('address_or_symbol','address_expression','memory_value','memory_or_symbol')),
'all_distribution_profiles_match':all(all(p['matching_profile'] for p in s['distribution_parity']) for s in syms)
}
data={'schema':'majormud.native-host-argument-position.v1','update':'FinalNativeClosure0014','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),'evidence_class':'PROVEN_STATIC_ARGUMENT_POSITION_SHAPES','method':'For every direct call to a proven WGSERVER import thunk, walk backward through the bounded local setup window, collect x86 PUSH operands in nearest-first callee argument order, classify each argument source, and aggregate stable position profiles across WCC/WBB counterparts.','interpretation_limits':['Nearest-first PUSH order is valid for ordinary x86 stack arguments but does not recover register arguments, hidden arguments, or arguments prepared outside the bounded window.','A stable source category does not prove an exact C type, signedness, pointee type, or ownership lifetime.','Stable immediate values identify caller constants only; they do not recover enum names or semantic meaning.','Exact prototypes still require matching Worldgroup binaries, symbols, headers, or dynamic traces.'],'summary':summary,'modules':module_rows,'symbols':syms}
for p in [root/'Librarium/Evidence/native_host_argument_positions_0014.json',root/'Reports/NativeDecode/native_host_argument_positions_0014.json']:
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_argument_positions_0014.py').write_text(Path(__file__).read_text())

cs=root/'Apps/Source/Reforged.Server/NativeHostArgumentPositionRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostArgumentPositionEvidence(','    string Symbol,','    int DirectCallsiteCount,','    int? StableArgumentCount,','    string StablePositions);','','public static class NativeHostArgumentPositionRegistry','{','    public const string EvidenceId = "FinalNativeClosure0014";','    public const string Scope = "Bounded caller-side stack argument position evidence only; exact native prototypes remain fail-closed.";','','    public static readonly IReadOnlyList<NativeHostArgumentPositionEvidence> Proven = new NativeHostArgumentPositionEvidence[]','    {']
for s in syms:
    ps=';'.join(f"{p['position']}:{p['stable_category']}:{p['stable_value'] or ''}" for p in s['stable_positions_across_modules']).replace('"','\\"')
    ac='null' if s['stable_argument_count_across_modules'] is None else str(s['stable_argument_count_across_modules'])
    lines.append(f'        new("{s["symbol"]}", {s["total_direct_callsites"]}, {ac}, "{ps}"),')
lines += ['    };','}']; cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_argument_positions_0014.py'
val.write_text('''#!/usr/bin/env python3\nimport json, pathlib\nr=pathlib.Path(__file__).parents[2]\nd=json.loads((r/"Librarium/Evidence/native_host_argument_positions_0014.json").read_text())\nassert d["schema"]=="majormud.native-host-argument-position.v1"\nassert len(d["modules"])==4\nassert d["summary"]["direct_callsites_analyzed"]==4630\nassert d["summary"]["host_symbols_analyzed"]==121\nassert d["summary"]["all_distribution_profiles_match"]\nassert d["summary"]["symbols_with_stable_argument_count_across_modules"] > 0\nassert d["summary"]["symbols_with_at_least_one_stable_argument_position"] > 0\nassert all(all(p["matching_profile"] for p in s["distribution_parity"]) for s in d["symbols"])\nprint("native host argument position validation passed")\n''')
report=root/'Reports/NativeDecode/native_host_argument_position_closure_0014.md'
report.write_text(f'''# Final Native Closure 0014 — Host Argument Positions\n\n## Proven\n\n- Analyzed all {summary['direct_callsites_analyzed']} direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.\n- Recovered bounded stack-argument position profiles for {summary['host_symbols_analyzed']} imported host symbols.\n- {summary['symbols_with_stable_argument_count_across_modules']} symbols have one stable bounded argument count across all observed modules.\n- {summary['symbols_with_at_least_one_stable_argument_position']} symbols have at least one argument position with a stable caller-source category.\n- {summary['symbols_with_all_observed_positions_stable']} symbols have stable categories at every observed bounded argument position.\n- Stable positions include {summary['zero_or_null_stable_positions']} zero/null positions, {summary['immediate_constant_stable_positions']} immediate-constant positions, and {summary['address_or_memory_stable_positions']} address/memory-derived positions.\n- WCC/WBB counterpart profiles match exactly.\n\n## Runtime effect\n\n`NativeHostArgumentPositionRegistry` exposes proven caller-side positional ABI evidence while exact parameter types, pointee layouts, enum names, and ownership remain fail-closed.\n\n## Remaining boundary\n\nExact prototypes and host semantics still require matching Worldgroup binaries, symbols, headers, or dynamic traces.\n''')
ud=root/'Updates/FinalNativeClosure0014'; ud.mkdir(parents=True,exist_ok=True); (ud/'README.md').write_text('FinalNativeClosure0014 recovers stable caller-side stack argument positions for direct WGSERVER import calls.\n')
with (root/'CHANGELOG.md').open('a') as f:
    f.write('\n## FinalNativeClosure0014\n- Added stable caller-side argument-position evidence for direct WGSERVER imports.\n- Added fail-closed runtime registry, analyzer, validator, Librarium evidence, report, and cumulative manifests.\n')

for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_argument_positions_0014.py'),doraise=True)
py_compile.compile(str(val),doraise=True)
subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')

manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0014.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inventory(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0014','baseline':'FinalNativeClosure0013','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0014-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
