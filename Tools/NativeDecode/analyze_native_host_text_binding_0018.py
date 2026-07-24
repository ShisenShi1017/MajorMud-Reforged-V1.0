from __future__ import annotations
import json, hashlib, datetime as dt, zipfile, py_compile, shutil
from pathlib import Path
from collections import Counter, defaultdict

src=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0017-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass18'); root=work/'base'
if work.exists(): shutil.rmtree(work)
root.mkdir(parents=True)
with zipfile.ZipFile(src) as z: z.extractall(root)
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
text=json.loads((root/'Reports/NativeDecode/native_host_text_template_contract_0017.json').read_text())
pos=json.loads((root/'Reports/NativeDecode/native_host_argument_positions_0014.json').read_text())
# index observed positional counts by symbol/module
posidx={}
for s in pos['symbols']:
    for mod,p in s['modules'].items():
        posidx[(s['symbol'],mod)]={x['position']:x for x in p['positions']}

by=defaultdict(list)
for r in text['records']: by[r['symbol']].append(r)
records=[]
for sym, rs in sorted(by.items()):
    template=[r for r in rs if r['conversion_count']>0]
    literal=[r for r in rs if r['conversion_count']==0]
    positions=sorted(set(r['position'] for r in rs))
    template_positions=sorted(set(r['position'] for r in template))
    arities=sorted(set(r['conversion_count'] for r in template))
    conv=Counter(); modules=sorted(set(r['module'] for r in rs))
    occurrence_total=sum(r['occurrences'] for r in rs)
    template_occ=sum(r['occurrences'] for r in template)
    literal_occ=sum(r['occurrences'] for r in literal)
    for r in template:
        for c in r['conversion_codes']: conv[c]+=r['occurrences']
    # Conservative binding capacity: number of observed caller argument positions after template position.
    capacity=[]
    for r in template:
        pmap=posidx.get((sym,r['module']),{})
        later=len([p for p in pmap if p>r['position']])
        capacity.append({'module':r['module'],'template_position':r['position'],'conversion_count':r['conversion_count'],'observed_later_positions':later,'capacity_sufficient':later>=r['conversion_count'],'occurrences':r['occurrences']})
    records.append({
      'symbol':sym,'modules':modules,'ascii_argument_positions':positions,'template_positions':template_positions,
      'template_position_stable':len(template_positions)<=1,'template_record_count':len(template),'literal_record_count':len(literal),
      'template_occurrences':template_occ,'literal_occurrences':literal_occ,'total_occurrences':occurrence_total,
      'observed_conversion_arities':arities,'conversion_codes':dict(sorted(conv.items())),
      'binding_capacity_observations':capacity,
      'all_bounded_capacities_sufficient':all(x['capacity_sufficient'] for x in capacity) if capacity else None
    })
summary={
 'host_symbols_with_ascii_arguments':len(records),
 'host_symbols_with_templates':sum(r['template_record_count']>0 for r in records),
 'host_symbols_literal_only':sum(r['template_record_count']==0 for r in records),
 'template_symbols_with_stable_template_position':sum(r['template_record_count']>0 and r['template_position_stable'] for r in records),
 'template_symbols_with_sufficient_bounded_argument_capacity':sum(r['template_record_count']>0 and r['all_bounded_capacities_sufficient'] for r in records),
 'template_binding_observations':sum(len(r['binding_capacity_observations']) for r in records),
 'template_occurrences':sum(r['template_occurrences'] for r in records),
 'literal_occurrences':sum(r['literal_occurrences'] for r in records)
}
data={
 'schema':'majormud.native-host-text-binding.v1','update':'FinalNativeClosure0018','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),
 'evidence_class':'PROVEN_CALLER_TEXT_BINDING_SHAPE',
 'method':'Join the proven host-facing ASCII/template records from Update0017 with positional caller-argument evidence from Update0014. Recover per-host-symbol template positions, conversion arities, directive domains, and whether the bounded caller argument positions after the template are sufficient to supply the observed conversions.',
 'interpretation_limits':['Sufficient bounded caller argument capacity does not prove exact variadic parameter types or formatter implementation.','Arguments may be consumed by fixed parameters as well as formatting conversions; this pass records capacity, not an exact formal prototype.','Runtime-built and indirect templates remain outside this evidence set.','Worldgroup message routing and terminal rendering still require host implementation evidence.'],
 'source_evidence':['Librarium/Evidence/native_host_text_template_contract_0017.json','Librarium/Evidence/native_host_argument_positions_0014.json'],
 'summary':summary,'records':records
}
for p in [root/'Librarium/Evidence/native_host_text_binding_0018.json',root/'Reports/NativeDecode/native_host_text_binding_0018.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_text_binding_0018.py').write_text(Path(__file__).read_text())

cs=root/'Apps/Source/Reforged.Server/NativeHostTextBindingRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostTextBindingEvidence(','    string Symbol, IReadOnlyList<int> TemplatePositions, IReadOnlyList<int> ConversionArities,','    bool TemplatePositionStable, bool? BoundedArgumentCapacitySufficient);','','public static class NativeHostTextBindingRegistry','{','    public const string EvidenceId = "FinalNativeClosure0018";','    public const string Scope = "Proven caller text-binding shape only; native formatting and exact variadic types remain fail-closed.";','    public static readonly IReadOnlyList<NativeHostTextBindingEvidence> Proven = new NativeHostTextBindingEvidence[]','    {']
for r in records:
 if not r['template_record_count']: continue
 ps=', '.join(str(x) for x in r['template_positions']); ar=', '.join(str(x) for x in r['observed_conversion_arities'])
 cap='null' if r['all_bounded_capacities_sufficient'] is None else str(r['all_bounded_capacities_sufficient']).lower()
 lines.append(f'        new("{r["symbol"]}", new int[] {{ {ps} }}, new int[] {{ {ar} }}, {str(r["template_position_stable"]).lower()}, {cap}),')
lines+=['    };','}']; cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_text_binding_0018.py'; val.parent.mkdir(parents=True,exist_ok=True)
val.write_text('''#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_text_binding_0018.json").read_text())
assert d["schema"]=="majormud.native-host-text-binding.v1"
s=d["summary"]
assert s["host_symbols_with_ascii_arguments"]==len(d["records"])>0
assert s["host_symbols_with_templates"]+s["host_symbols_literal_only"]==len(d["records"])
assert s["template_binding_observations"]==sum(len(x["binding_capacity_observations"]) for x in d["records"])
for x in d["records"]:
 assert x["total_occurrences"]==x["template_occurrences"]+x["literal_occurrences"]
 assert x["template_position_stable"]==(len(x["template_positions"])<=1)
 for y in x["binding_capacity_observations"]:
  assert y["capacity_sufficient"]==(y["observed_later_positions"]>=y["conversion_count"])
print("native host text binding validation passed")
''')
report=root/'Reports/NativeDecode/native_host_text_binding_closure_0018.md'
report.write_text(f'''# Final Native Closure 0018 — Host Text Binding Shape\n\n## Proven\n\n- Classified {summary['host_symbols_with_ascii_arguments']} Worldgroup host symbols receiving proven ASCII arguments.\n- Identified {summary['host_symbols_with_templates']} symbols receiving printf-style templates and {summary['host_symbols_literal_only']} literal-only symbols.\n- Proved {summary['template_symbols_with_stable_template_position']} template-bearing symbols use one stable template argument position.\n- Evaluated {summary['template_binding_observations']} template binding observations against bounded caller argument positions.\n- {summary['template_symbols_with_sufficient_bounded_argument_capacity']} template-bearing symbols have sufficient bounded post-template argument capacity for every observed conversion count.\n\n## Runtime effect\n\n`NativeHostTextBindingRegistry` exposes only the proven template position and conversion-arity contract. It does not invoke a formatter or assign unsupported variadic types.\n\n## Remaining boundary\n\nExact fixed/variadic prototypes, argument types, formatter behavior, message routing, terminal translation, and output ordering still require `WGSERVER.EXE`, symbols, headers, or dynamic traces.\n''')
ud=root/'Updates/FinalNativeClosure0018'; ud.mkdir(parents=True,exist_ok=True)
(ud/'README.md').write_text('FinalNativeClosure0018 joins proven text templates to caller argument positions and recovers conservative host text-binding shapes.\n')
with (root/'CHANGELOG.md').open('a') as f:
 f.write('\n## FinalNativeClosure0018\n- Added host text-template argument-position and conversion-arity binding evidence.\n- Added fail-closed runtime registry, analyzer, validator, report, and cumulative manifests.\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_text_binding_0018.py'),doraise=True); py_compile.compile(str(val),doraise=True)
import subprocess; subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')
manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0018.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inv(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inv(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0018','baseline':'FinalNativeClosure0017','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0018-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
