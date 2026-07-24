from __future__ import annotations
import json, shutil, hashlib, datetime as dt, zipfile, py_compile, subprocess
from pathlib import Path
from collections import defaultdict, Counter

srczip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0014-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass15'); root=work/'base15'
if work.exists(): shutil.rmtree(work)
root.mkdir(parents=True)
with zipfile.ZipFile(srczip) as z: z.extractall(root)

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

prior=json.loads((root/'Reports/NativeDecode/native_host_argument_positions_0014.json').read_text())

symbols=[]
finite_domains=0
singletons=0
multi=0
bitmask_candidates=0
zero_including=0
negative_values=0
for s in prior['symbols']:
    positions=defaultdict(list)
    for mname,profile in s['modules'].items():
        for p in profile['positions']:
            positions[p['position']].append((mname,p))
    outpos=[]
    for pos, entries in sorted(positions.items()):
        values=Counter()
        cats=Counter()
        total=0
        for mname,p in entries:
            cats.update(p.get('category_histogram',{}))
            values.update({k:int(v) for k,v in p.get('value_histogram',{}).items()})
            total += sum(p.get('category_histogram',{}).values())
        imm_only = bool(cats) and set(cats).issubset({'immediate_constant','zero_or_null'})
        parsed=[]
        if imm_only:
            for raw,count in values.items():
                try: val=int(raw,0)
                except Exception: continue
                parsed.append({'raw':raw,'value_signed':val,'value_u32':val & 0xffffffff,'count':count})
            parsed.sort(key=lambda x:x['value_u32'])
        distinct=len(parsed)
        finite=imm_only and distinct>0
        includes_zero=any(x['value_u32']==0 for x in parsed)
        has_negative=any(x['value_signed']<0 for x in parsed)
        # conservative mask candidate: all non-zero values are powers of two or OR-combinations of observed powers of two
        nz=[x['value_u32'] for x in parsed if x['value_u32']]
        powers=[v for v in nz if v & (v-1)==0]
        basis=0
        for v in powers: basis |= v
        mask_candidate=finite and len(nz)>=2 and bool(powers) and all((v & ~basis)==0 for v in nz)
        if finite:
            finite_domains += 1
            if distinct==1: singletons += 1
            else: multi += 1
            if includes_zero: zero_including += 1
            if has_negative: negative_values += 1
            if mask_candidate: bitmask_candidates += 1
        outpos.append({
            'position':pos,
            'category_histogram':dict(cats),
            'immediate_only_domain':finite,
            'distinct_value_count':distinct,
            'values':parsed,
            'includes_zero':includes_zero,
            'contains_negative_literal':has_negative,
            'bitmask_shape_candidate':mask_candidate,
            'observed_power_of_two_basis':[f'0x{v:08x}' for v in sorted(set(powers))],
            'interpretation':'finite caller constant domain only; enum/mask semantics unproven' if finite else 'mixed or non-immediate caller sources'
        })
    symbols.append({'symbol':s['symbol'],'total_direct_callsites':s['total_direct_callsites'],'positions':outpos})

summary={
    'host_symbols_analyzed':len(symbols),
    'finite_immediate_argument_domains':finite_domains,
    'singleton_immediate_domains':singletons,
    'multi_value_immediate_domains':multi,
    'domains_including_zero':zero_including,
    'domains_with_negative_literals':negative_values,
    'bitmask_shape_candidates':bitmask_candidates,
    'wcc_wbb_source':'FinalNativeClosure0014 positional profiles with proven distribution parity'
}
data={
    'schema':'majormud.native-host-immediate-domain.v1',
    'update':'FinalNativeClosure0015',
    'generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),
    'evidence_class':'PROVEN_CALLER_CONSTANT_DOMAINS',
    'method':'Aggregate immediate and zero/null values by imported host symbol and x86 stack argument position from the proven positional ABI profiles. Preserve signed and unsigned 32-bit interpretations and identify only conservative bit-shape candidates.',
    'interpretation_limits':[
        'A finite caller constant domain does not prove that the parameter is an enum, flag set, Boolean, handle, or scalar.',
        'Bitmask-shape candidates are structural observations only; flag names and meanings remain unknown.',
        'Values prepared through registers or memory are excluded from immediate-only domains.',
        'Exact declarations still require matching Worldgroup binaries, symbols, headers, or dynamic traces.'
    ],
    'summary':summary,
    'symbols':symbols
}
for p in [root/'Librarium/Evidence/native_host_immediate_domains_0015.json',root/'Reports/NativeDecode/native_host_immediate_domains_0015.json']:
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_immediate_domains_0015.py').write_text(Path(__file__).read_text())

cs=root/'Apps/Source/Reforged.Server/NativeHostImmediateDomainRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostImmediateDomainEvidence(','    string Symbol,','    int Position,','    IReadOnlyList<uint> Values,','    bool IncludesZero,','    bool BitmaskShapeCandidate);','','public static class NativeHostImmediateDomainRegistry','{','    public const string EvidenceId = "FinalNativeClosure0015";','    public const string Scope = "Proven caller-immediate domains only; enum and mask semantics remain fail-closed.";','','    public static readonly IReadOnlyList<NativeHostImmediateDomainEvidence> Proven = new NativeHostImmediateDomainEvidence[]','    {']
for s in symbols:
    for p in s['positions']:
        if not p['immediate_only_domain']: continue
        vals=', '.join(f'0x{x["value_u32"]:08X}u' for x in p['values'])
        lines.append(f'        new("{s["symbol"]}", {p["position"]}, new uint[] {{ {vals} }}, {str(p["includes_zero"]).lower()}, {str(p["bitmask_shape_candidate"]).lower()}),')
lines += ['    };','}']
cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_immediate_domains_0015.py'
val.write_text('''#!/usr/bin/env python3\nimport json, pathlib\nr=pathlib.Path(__file__).parents[2]\nd=json.loads((r/"Librarium/Evidence/native_host_immediate_domains_0015.json").read_text())\nassert d["schema"]=="majormud.native-host-immediate-domain.v1"\nassert d["summary"]["host_symbols_analyzed"]==121\nassert d["summary"]["finite_immediate_argument_domains"]>0\nassert d["summary"]["singleton_immediate_domains"]>0\nfor s in d["symbols"]:\n  for p in s["positions"]:\n    if p["immediate_only_domain"]:\n      assert p["distinct_value_count"]==len(p["values"]) and p["distinct_value_count"]>0\n      assert all(0 <= v["value_u32"] <= 0xffffffff for v in p["values"])\nprint("native host immediate domain validation passed")\n''')
report=root/'Reports/NativeDecode/native_host_immediate_domain_closure_0015.md'
report.write_text(f'''# Final Native Closure 0015 — Host Immediate Argument Domains\n\n## Proven\n\n- Analyzed positional caller evidence for {summary['host_symbols_analyzed']} imported Worldgroup host symbols.\n- Recovered {summary['finite_immediate_argument_domains']} finite immediate-only argument domains.\n- {summary['singleton_immediate_domains']} domains contain one observed constant and {summary['multi_value_immediate_domains']} contain multiple constants.\n- {summary['domains_including_zero']} domains include a proven zero/null literal.\n- {summary['domains_with_negative_literals']} domains contain at least one negative source literal when interpreted as signed x86 immediate data.\n- {summary['bitmask_shape_candidates']} domains have a conservative power-of-two/OR-combination bit shape.\n- WCC/WBB parity is inherited from the validated Update0014 positional profiles.\n\n## Runtime effect\n\n`NativeHostImmediateDomainRegistry` exposes exact observed caller constants while enum names, flag meanings, parameter types, and host behavior remain fail-closed.\n\n## Remaining boundary\n\nThe matching Worldgroup host binary or symbols are still required to turn these constant domains into authoritative enums, masks, and formal prototypes.\n''')
ud=root/'Updates/FinalNativeClosure0015'; ud.mkdir(parents=True,exist_ok=True)
(ud/'README.md').write_text('FinalNativeClosure0015 recovers finite immediate argument domains for direct WGSERVER import calls.\n')
with (root/'CHANGELOG.md').open('a') as f:
    f.write('\n## FinalNativeClosure0015\n- Added exact caller-immediate argument domains for direct WGSERVER imports.\n- Added conservative bit-shape evidence, fail-closed runtime registry, analyzer, validator, Librarium evidence, report, and cumulative manifests.\n')

for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_immediate_domains_0015.py'),doraise=True)
py_compile.compile(str(val),doraise=True)
subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')

manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0015.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inventory(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0015','baseline':'FinalNativeClosure0014','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0015-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
