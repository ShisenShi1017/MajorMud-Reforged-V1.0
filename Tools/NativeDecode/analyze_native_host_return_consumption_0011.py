from __future__ import annotations
import json, shutil, hashlib, datetime as dt, zipfile, py_compile, subprocess, re
from pathlib import Path
from collections import Counter,defaultdict
srczip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0010-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass11'); root=work/'base11'
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

def classify_post(ins,idx):
 j=idx+1; window=[]
 # skip immediate caller cleanup but retain it
 if j<len(ins) and re.match(r'^add\s+esp\s*,',ins[j]['text'].lower()):
  window.append(ins[j]); j+=1
 category='no_local_eax_consumption_observed'; evidence=None
 for k in range(j,min(len(ins),j+5)):
  t=ins[k]['text'].lower(); window.append(ins[k])
  if re.match(r'^(ret|jmp|j[a-z]+|call|leave|int)\b',t): break
  # Reads of eax before a write to eax
  if re.match(r'^(test)\s+eax\s*,\s*eax\b',t): category='eax_tested_for_zero_or_flags'; evidence=ins[k]; break
  if re.match(r'^cmp\s+eax\s*,',t) or re.match(r'^cmp\s+[^,]+\s*,\s*eax\b',t): category='eax_compared'; evidence=ins[k]; break
  if re.match(r'^push\s+eax\b',t): category='eax_passed_as_argument'; evidence=ins[k]; break
  if re.match(r'^mov\s+[^,]+\s*,\s*eax\b',t) or re.match(r'^lea\s+[^,]+\s*,\s*\[eax',t): category='eax_value_copied_or_addressed'; evidence=ins[k]; break
  if re.match(r'^(and|or|xor|add|sub|imul|shl|shr|sar)\s+eax\s*,',t): category='eax_value_transformed'; evidence=ins[k]; break
  if re.match(r'^(inc|dec|neg|not)\s+eax\b',t): category='eax_value_transformed'; evidence=ins[k]; break
  # first clear write destroys return before read
  if re.match(r'^(mov|lea|pop|xor|sub)\s+eax\s*,',t): category='eax_overwritten_before_observed_use'; evidence=ins[k]; break
 return category, evidence, [{'va':f"0x{x['va']:08x}",'instruction':x['text']} for x in window]
module_rows=[]
for name,rel in mods.items():
 p=binroot/rel; ins=disasm(p)
 pm=next(m for m in prior['modules'] if m['file']==name)
 rows=[]
 for imp in pm['imports']:
  thunk=int(imp['thunk_va'],16); cats=Counter(); examples={}
  for i,x in enumerate(ins):
   m=re.match(r'^call\s+0x([0-9a-fA-F]+)\b',x['text'])
   if not m or int(m.group(1),16)!=thunk: continue
   c,e,w=classify_post(ins,i); cats[c]+=1
   examples.setdefault(c,{'callsite_va':f"0x{x['va']:08x}",'evidence_instruction':None if e is None else {'va':f"0x{e['va']:08x}",'instruction':e['text']},'post_call_window':w})
  if cats: rows.append({'symbol':imp['name'],'direct_callsite_count':sum(cats.values()),'return_consumption_histogram':dict(cats),'examples':examples})
 module_rows.append({'file':name,'sha256':sha(p),'imports':rows,'direct_callsite_count':sum(r['direct_callsite_count'] for r in rows)})
agg={}
for m in module_rows:
 for r in m['imports']:
  a=agg.setdefault(r['symbol'],{'symbol':r['symbol'],'modules':{},'combined_histogram':Counter(),'total_direct_callsites':0})
  a['modules'][m['file']]=r['return_consumption_histogram']; a['combined_histogram'].update(r['return_consumption_histogram']); a['total_direct_callsites']+=r['direct_callsite_count']
syms=[]
for a in agg.values():
 a['combined_histogram']=dict(a['combined_histogram'])
 pairs=[]
 for l,r in [('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]:
  if l in a['modules'] or r in a['modules']: pairs.append({'pair':f'{l}:{r}','matching_histogram':a['modules'].get(l)==a['modules'].get(r)})
 a['distribution_parity']=pairs
 # Proven return relevance only when every callsite consumes EAX before overwrite/control boundary
 consumed=sum(v for k,v in a['combined_histogram'].items() if k not in ('no_local_eax_consumption_observed','eax_overwritten_before_observed_use'))
 a['all_observed_direct_callsites_consume_eax']=consumed==a['total_direct_callsites']
 a['any_observed_direct_callsite_consumes_eax']=consumed>0
 syms.append(a)
syms.sort(key=lambda x:(-x['total_direct_callsites'],x['symbol']))
summary={
 'direct_callsites_analyzed':sum(m['direct_callsite_count'] for m in module_rows),
 'host_symbols_analyzed':len(syms),
 'symbols_with_any_local_eax_consumption':sum(s['any_observed_direct_callsite_consumes_eax'] for s in syms),
 'symbols_with_eax_consumed_at_every_observed_direct_callsite':sum(s['all_observed_direct_callsites_consume_eax'] for s in syms),
 'symbols_with_mixed_or_no_local_eax_consumption':sum(not s['all_observed_direct_callsites_consume_eax'] for s in syms)
}
data={'schema':'majormud.native-host-return-consumption.v1','update':'FinalNativeClosure0011','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),'evidence_class':'PROVEN_STATIC_POST_CALL_EAX_USAGE','method':'For every direct call to a proven WGSERVER import thunk, inspect a bounded post-call instruction window after optional immediate ADD ESP cleanup and classify the first observed EAX read or overwrite.','interpretation_limits':['EAX consumption proves the caller uses a machine-word return value or returned pointer-like value; it does not recover the exact C return type.','No local EAX consumption does not prove void because use may occur beyond the bounded window or through flags/other conventions.','Floating-point, EDX:EAX, hidden structure returns, indirect calls, and host-internal behavior remain outside scope.'],'summary':summary,'modules':module_rows,'symbols':syms}
for p in [root/'Librarium/Evidence/native_host_return_consumption_0011.json',root/'Reports/NativeDecode/native_host_return_consumption_0011.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
# generator copy
(root/'Tools/NativeDecode/analyze_native_host_return_consumption_0011.py').write_text(Path(__file__).read_text())
# registry
cs=root/'Apps/Source/Reforged.Server/NativeHostReturnConsumptionRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostReturnConsumptionEvidence(','    string Symbol,','    int DirectCallsiteCount,','    bool AnyObservedEaxConsumption,','    bool EveryObservedCallsiteConsumesEax,','    string ConsumptionKinds);','','public static class NativeHostReturnConsumptionRegistry','{','    public const string EvidenceId = "FinalNativeClosure0011";','    public const string Scope = "Bounded post-call EAX usage only; exact native return types remain fail-closed.";','','    public static readonly IReadOnlyList<NativeHostReturnConsumptionEvidence> Proven = new NativeHostReturnConsumptionEvidence[]','    {']
for s in syms:
 kinds=','.join(sorted(k for k,v in s['combined_histogram'].items() if v))
 lines.append(f'        new("{s["symbol"]}", {s["total_direct_callsites"]}, {str(s["any_observed_direct_callsite_consumes_eax"]).lower()}, {str(s["all_observed_direct_callsites_consume_eax"]).lower()}, "{kinds}"),')
lines += ['    };','}']; cs.write_text('\n'.join(lines)+'\n')
# validator
val=root/'Tools/Validation/validate_native_host_return_consumption_0011.py'
val.write_text('''#!/usr/bin/env python3\nimport json, pathlib\nr=pathlib.Path(__file__).parents[2]\nd=json.loads((r/"Librarium/Evidence/native_host_return_consumption_0011.json").read_text())\nassert d["schema"]=="majormud.native-host-return-consumption.v1"\nassert len(d["modules"])==4\nby={m["file"]:m for m in d["modules"]}\nassert by["wccmmud.dll"]["direct_callsite_count"]==by["wbbmmud.dll"]["direct_callsite_count"]\nassert by["wccmmpls.dll"]["direct_callsite_count"]==by["wbbmmpls.dll"]["direct_callsite_count"]\nassert all(all(p["matching_histogram"] for p in s["distribution_parity"]) for s in d["symbols"])\nassert d["summary"]["direct_callsites_analyzed"]==14056\nassert any(s["symbol"]=="_lngrnd" and s["any_observed_direct_callsite_consumes_eax"] for s in d["symbols"])\nprint("native host return consumption validation passed")\n''')
report=root/'Reports/NativeDecode/native_host_return_consumption_closure_0011.md'
report.write_text(f'''# Final Native Closure 0011 — Host Return-Value Consumption\n\n## Proven\n\n- Analyzed all {summary['direct_callsites_analyzed']} direct WCC/WBB MMUD/MMPLS calls into Worldgroup host imports.\n- {summary['symbols_with_any_local_eax_consumption']} imported symbols have at least one direct callsite where the caller consumes EAX before an observed overwrite or control-flow boundary.\n- {summary['symbols_with_eax_consumed_at_every_observed_direct_callsite']} symbols have EAX consumed at every observed direct callsite.\n- WCC/WBB counterpart return-consumption histograms match for every imported symbol.\n- The evidence distinguishes zero/flag tests, comparisons, argument forwarding, copies/address use, transformations, overwrites, and no bounded local consumption.\n\n## Runtime effect\n\n`NativeHostReturnConsumptionRegistry` exposes only whether a machine-word return is observably consumed. Exact C return types remain fail-closed.\n\n## Remaining boundary\n\nExact scalar, pointer, handle, boolean, signedness, floating-point, and structure-return declarations still require Worldgroup binaries, symbols, headers, or dynamic traces.\n''')
ud=root/'Updates/FinalNativeClosure0011'; ud.mkdir(parents=True,exist_ok=True); (ud/'README.md').write_text('FinalNativeClosure0011 classifies bounded post-call EAX return consumption for every direct WGSERVER import callsite.\n')
with (root/'CHANGELOG.md').open('a') as f: f.write('\n## FinalNativeClosure0011\n- Added complete bounded post-call EAX return-consumption evidence for direct WGSERVER calls.\n- Added fail-closed runtime registry, analyzer, validator, Librarium evidence, report, and cumulative manifests.\n')
# validate
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_return_consumption_0011.py'),doraise=True); py_compile.compile(str(val),doraise=True); subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')
# manifests
manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0011.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inventory(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0011','baseline':'FinalNativeClosure0010','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0011-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
