from __future__ import annotations
import json, shutil, hashlib, datetime as dt, zipfile, py_compile, subprocess, re
from pathlib import Path
from collections import Counter, defaultdict

srczip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0012-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass13'); root=work/'base13'
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

EAX_WRITES = re.compile(r'^(mov|lea|pop|xor|sub|add|and|or|imul|idiv|div|neg|not|inc|dec|shl|shr|sar|rol|ror)\s+eax\b',re.I)
CONTROL = re.compile(r'^(ret|jmp|j[a-z]+|call|leave|int)\b',re.I)

def classify_use(text:str):
    t=text.lower().strip()
    # exact return-domain patterns
    m=re.match(r'^(test)\s+eax\s*,\s*eax$',t)
    if m: return ('zero_nonzero_test',None)
    m=re.match(r'^cmp\s+eax\s*,\s*(-?(?:0x[0-9a-f]+|\d+))$',t)
    if m: return ('compare_eax_immediate',m.group(1))
    m=re.match(r'^cmp\s+(-?(?:0x[0-9a-f]+|\d+))\s*,\s*eax$',t)
    if m: return ('compare_immediate_eax',m.group(1))
    m=re.match(r'^(and|or|xor)\s+eax\s*,\s*(-?(?:0x[0-9a-f]+|\d+))$',t)
    if m: return ('bitmask_or_transform',m.group(2))
    m=re.match(r'^(inc|dec|neg|not)\s+eax$',t)
    if m: return ('unary_transform',None)
    m=re.match(r'^(mov|push)\s+[^,]+,?\s*eax$',t)
    if m: return ('value_forward_or_copy',None)
    if re.match(r'^push\s+eax$',t): return ('value_forward_or_copy',None)
    if '[eax' in t or re.search(r'\beax\s*\+',t): return ('address_or_index_use',None)
    if re.search(r'\beax\b',t): return ('other_eax_use',None)
    return (None,None)

def first_domain_use(ins, idx, limit=10):
    seq=[]
    for k in range(idx+1,min(len(ins),idx+1+limit)):
        t=ins[k]['text'].strip(); tl=t.lower()
        seq.append({'va':f"0x{ins[k]['va']:08x}",'instruction':t})
        cls,const=classify_use(t)
        if cls:
            return {'classification':cls,'constant':const,'use_va':f"0x{ins[k]['va']:08x}",'instruction':t,'window':seq}
        if EAX_WRITES.match(tl):
            return {'classification':'overwritten_before_domain_use','constant':None,'use_va':f"0x{ins[k]['va']:08x}",'instruction':t,'window':seq}
        if CONTROL.match(tl):
            return {'classification':'control_boundary_before_domain_use','constant':None,'use_va':f"0x{ins[k]['va']:08x}",'instruction':t,'window':seq}
    return {'classification':'no_bounded_domain_use','constant':None,'use_va':None,'instruction':None,'window':seq}

module_rows=[]
for name,rel in mods.items():
    p=binroot/rel; ins=disasm(p)
    pm=next(m for m in prior['modules'] if m['file']==name)
    rows=[]
    for imp in pm['imports']:
        thunk=int(imp['thunk_va'],16); hist=Counter(); constants=Counter(); examples=[]
        for i,x in enumerate(ins):
            m=re.match(r'^call\s+0x([0-9a-fA-F]+)\b',x['text'])
            if not m or int(m.group(1),16)!=thunk: continue
            use=first_domain_use(ins,i)
            hist[use['classification']]+=1
            if use['constant'] is not None: constants[use['constant']]+=1
            if len(examples)<5 and use['classification'] not in ('no_bounded_domain_use','overwritten_before_domain_use'):
                examples.append({'callsite_va':f"0x{x['va']:08x}",**use})
        if hist:
            rows.append({'symbol':imp['name'],'direct_callsite_count':sum(hist.values()),'domain_use_histogram':dict(hist),'observed_constants':dict(constants),'examples':examples})
    module_rows.append({'file':name,'sha256':sha(p),'imports':rows,'direct_callsite_count':sum(r['direct_callsite_count'] for r in rows)})

agg={}
for m in module_rows:
    for r in m['imports']:
        a=agg.setdefault(r['symbol'],{'symbol':r['symbol'],'modules':{},'combined_domain_use_histogram':Counter(),'combined_observed_constants':Counter(),'total_direct_callsites':0})
        a['modules'][m['file']]={'domain_use_histogram':r['domain_use_histogram'],'observed_constants':r['observed_constants']}
        a['combined_domain_use_histogram'].update(r['domain_use_histogram'])
        a['combined_observed_constants'].update(r['observed_constants'])
        a['total_direct_callsites'] += r['direct_callsite_count']
syms=[]
for a in agg.values():
    a['combined_domain_use_histogram']=dict(a['combined_domain_use_histogram'])
    a['combined_observed_constants']=dict(a['combined_observed_constants'])
    pairs=[]
    for l,r in [('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]:
        if l in a['modules'] or r in a['modules']:
            pairs.append({'pair':f'{l}:{r}','matching_profile':a['modules'].get(l)==a['modules'].get(r)})
    a['distribution_parity']=pairs
    h=a['combined_domain_use_histogram']; c=a['combined_observed_constants']
    a['has_zero_nonzero_test']=h.get('zero_nonzero_test',0)>0
    a['has_constant_comparison']=h.get('compare_eax_immediate',0)+h.get('compare_immediate_eax',0)>0
    a['has_bitmask_evidence']=h.get('bitmask_or_transform',0)>0
    a['observed_constant_set']=sorted(c.keys(), key=lambda s:int(s,0)) if c else []
    # conservative semantic candidates
    a['bounded_domain_inference']='none'
    if a['has_zero_nonzero_test'] and not a['has_constant_comparison'] and not a['has_bitmask_evidence']:
        a['bounded_domain_inference']='zero_nonzero_branchable_machine_word'
    elif a['has_bitmask_evidence']:
        a['bounded_domain_inference']='bitfield_or_maskable_machine_word'
    elif a['has_constant_comparison']:
        a['bounded_domain_inference']='integer_or_enum_like_machine_word'
    syms.append(a)
syms.sort(key=lambda x:(-x['total_direct_callsites'],x['symbol']))
summary={
'direct_callsites_analyzed':sum(m['direct_callsite_count'] for m in module_rows),
'host_symbols_analyzed':len(syms),
'symbols_with_zero_nonzero_tests':sum(s['has_zero_nonzero_test'] for s in syms),
'symbols_with_constant_comparisons':sum(s['has_constant_comparison'] for s in syms),
'symbols_with_bitmask_evidence':sum(s['has_bitmask_evidence'] for s in syms),
'symbols_with_any_bounded_domain_evidence':sum(s['bounded_domain_inference']!='none' for s in syms),
'all_distribution_profiles_match':all(all(p['matching_profile'] for p in s['distribution_parity']) for s in syms),
'domain_use_totals':dict(sum((Counter(r['domain_use_histogram']) for m in module_rows for r in m['imports']),Counter()))
}
data={'schema':'majormud.native-host-return-domain.v1','update':'FinalNativeClosure0013','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),'evidence_class':'PROVEN_STATIC_BOUNDED_RETURN_DOMAIN','method':'For every direct call to a proven WGSERVER import thunk, inspect the first bounded EAX use and classify zero tests, immediate comparisons, bit masks/transforms, address/index use, forwarding, overwrite, or control-boundary termination.','interpretation_limits':['A zero/nonzero test proves only branchable machine-word semantics, not a native Boolean declaration.','Immediate comparisons support integer- or enum-like use but do not recover signedness or the complete value domain.','Bitmask operations support maskable machine-word use but do not recover flag ownership or names.','The bounded window may miss delayed uses, values copied through another register, or interprocedural consumers.','Exact return types and host implementations remain unavailable without matching Worldgroup binaries, symbols, headers, or dynamic traces.'],'summary':summary,'modules':module_rows,'symbols':syms}
for p in [root/'Librarium/Evidence/native_host_return_domains_0013.json',root/'Reports/NativeDecode/native_host_return_domains_0013.json']:
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_return_domains_0013.py').write_text(Path(__file__).read_text())

cs=root/'Apps/Source/Reforged.Server/NativeHostReturnDomainRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostReturnDomainEvidence(','    string Symbol,','    int DirectCallsiteCount,','    bool HasZeroNonzeroTest,','    bool HasConstantComparison,','    bool HasBitmaskEvidence,','    string BoundedDomainInference,','    string ObservedConstants);','','public static class NativeHostReturnDomainRegistry','{','    public const string EvidenceId = "FinalNativeClosure0013";','    public const string Scope = "Bounded caller-side EAX domain evidence only; exact native return types remain fail-closed.";','','    public static readonly IReadOnlyList<NativeHostReturnDomainEvidence> Proven = new NativeHostReturnDomainEvidence[]','    {']
for s in syms:
    consts=','.join(s['observed_constant_set']).replace('"','\\"')
    lines.append(f'        new("{s["symbol"]}", {s["total_direct_callsites"]}, {str(s["has_zero_nonzero_test"]).lower()}, {str(s["has_constant_comparison"]).lower()}, {str(s["has_bitmask_evidence"]).lower()}, "{s["bounded_domain_inference"]}", "{consts}"),')
lines += ['    };','}']; cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_return_domains_0013.py'
val.write_text('''#!/usr/bin/env python3\nimport json, pathlib\nr=pathlib.Path(__file__).parents[2]\nd=json.loads((r/"Librarium/Evidence/native_host_return_domains_0013.json").read_text())\nassert d["schema"]=="majormud.native-host-return-domain.v1"\nassert len(d["modules"])==4\nassert d["summary"]["direct_callsites_analyzed"]==14056\nassert d["summary"]["host_symbols_analyzed"]==121\nassert d["summary"]["all_distribution_profiles_match"]\nassert all(all(p["matching_profile"] for p in s["distribution_parity"]) for s in d["symbols"])\nassert d["summary"]["symbols_with_any_bounded_domain_evidence"] > 0\nassert any(s["has_zero_nonzero_test"] for s in d["symbols"])\nassert any(s["has_constant_comparison"] for s in d["symbols"])\nprint("native host return domain validation passed")\n''')
report=root/'Reports/NativeDecode/native_host_return_domain_closure_0013.md'
report.write_text(f'''# Final Native Closure 0013 — Host Return-Value Domains\n\n## Proven\n\n- Analyzed all {summary['direct_callsites_analyzed']} direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.\n- Classified bounded first-use return domains for {summary['host_symbols_analyzed']} imported host symbols.\n- {summary['symbols_with_zero_nonzero_tests']} symbols are tested for zero/nonzero at one or more direct callsites.\n- {summary['symbols_with_constant_comparisons']} symbols are compared against immediate constants.\n- {summary['symbols_with_bitmask_evidence']} symbols are consumed by bit masks or bitwise transforms.\n- {summary['symbols_with_any_bounded_domain_evidence']} symbols now have a conservative machine-word domain classification.\n- WCC/WBB counterpart profiles match exactly.\n\n## Runtime effect\n\n`NativeHostReturnDomainRegistry` exposes proven caller-side domain evidence while exact native return declarations, signedness, handles, pointers, enums, and flag names remain fail-closed.\n\n## Remaining boundary\n\nExact return types and implementations still require matching Worldgroup binaries, symbols, headers, or dynamic traces.\n''')
ud=root/'Updates/FinalNativeClosure0013'; ud.mkdir(parents=True,exist_ok=True); (ud/'README.md').write_text('FinalNativeClosure0013 classifies bounded caller-side return-value domains for every direct WGSERVER import callsite.\n')
with (root/'CHANGELOG.md').open('a') as f:
    f.write('\n## FinalNativeClosure0013\n- Added bounded return-value domain evidence for direct WGSERVER imports.\n- Added fail-closed runtime registry, analyzer, validator, Librarium evidence, report, and cumulative manifests.\n')

for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_return_domains_0013.py'),doraise=True)
py_compile.compile(str(val),doraise=True)
subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')

manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0013.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inventory(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0013','baseline':'FinalNativeClosure0012','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0013-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
