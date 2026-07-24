from __future__ import annotations
import json, hashlib, datetime as dt, zipfile, py_compile, shutil, subprocess
from pathlib import Path
from collections import defaultdict, Counter
src=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0018-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass19'); root=work/'base'
if work.exists(): shutil.rmtree(work)
root.mkdir(parents=True)
with zipfile.ZipFile(src) as z: z.extractall(root)
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
text=json.loads((root/'Reports/NativeDecode/native_host_text_template_contract_0017.json').read_text())
records=[]
for r in text['records']:
    t=r['text']
    records.append({
      'symbol':r['symbol'],'module':r['module'],'position':r['position'],'address_u32':r['address_u32'],
      'occurrences':r['occurrences'],'text':t,'byte_length':r['byte_length'],
      'is_template':r['conversion_count']>0,'conversion_count':r['conversion_count'],
      'starts_with_whitespace':bool(t[:1].isspace()),'ends_with_whitespace':bool(t[-1:].isspace()),
      'carriage_return_count':t.count('\r'),'line_feed_count':t.count('\n'),'tab_count':t.count('\t'),
      'ends_with_cr':t.endswith('\r'),'ends_with_lf':t.endswith('\n'),'ends_with_crlf':t.endswith('\r\n'),
      'contains_cr_without_lf':('\r' in t and '\n' not in t),'contains_lf_without_cr':('\n' in t and '\r' not in t),
      'contains_embedded_nul':'\x00' in t,
    })
summary={
 'text_records':len(records),
 'observed_occurrences':sum(r['occurrences'] for r in records),
 'records_with_carriage_return':sum(r['carriage_return_count']>0 for r in records),
 'records_with_line_feed':sum(r['line_feed_count']>0 for r in records),
 'records_with_tab':sum(r['tab_count']>0 for r in records),
 'records_ending_cr':sum(r['ends_with_cr'] for r in records),
 'records_ending_lf':sum(r['ends_with_lf'] for r in records),
 'records_ending_crlf':sum(r['ends_with_crlf'] for r in records),
 'records_with_cr_without_lf':sum(r['contains_cr_without_lf'] for r in records),
 'records_with_lf_without_cr':sum(r['contains_lf_without_cr'] for r in records),
 'records_starting_whitespace':sum(r['starts_with_whitespace'] for r in records),
 'records_ending_whitespace':sum(r['ends_with_whitespace'] for r in records),
 'records_with_embedded_nul':sum(r['contains_embedded_nul'] for r in records),
}
# per-symbol framing profile
by=defaultdict(list)
for r in records: by[r['symbol']].append(r)
profiles=[]
for sym, rs in sorted(by.items()):
    profiles.append({
      'symbol':sym,'modules':sorted(set(r['module'] for r in rs)),'record_count':len(rs),
      'occurrences':sum(r['occurrences'] for r in rs),
      'records_ending_cr':sum(r['ends_with_cr'] for r in rs),'records_ending_lf':sum(r['ends_with_lf'] for r in rs),
      'records_ending_crlf':sum(r['ends_with_crlf'] for r in rs),'records_with_tab':sum(r['tab_count']>0 for r in rs),
      'records_starting_whitespace':sum(r['starts_with_whitespace'] for r in rs),
      'records_ending_whitespace':sum(r['ends_with_whitespace'] for r in rs),
      'line_termination_forms':sorted(set('crlf' if r['ends_with_crlf'] else 'cr' if r['ends_with_cr'] else 'lf' if r['ends_with_lf'] else 'none' for r in rs)),
    })
data={
 'schema':'majormud.native-host-text-framing.v1','update':'FinalNativeClosure0019','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),
 'evidence_class':'PROVEN_CALLER_TEXT_FRAMING',
 'method':'Derive exact caller-owned whitespace and control-character framing from every proven host-facing ASCII record recovered in FinalNativeClosure0017. Count CR, LF, CRLF, tab, leading/trailing whitespace, and embedded NUL properties without assigning unsupported Worldgroup terminal-translation behavior.',
 'interpretation_limits':['The observed bytes prove the MajorMUD caller-side text contract only.','Worldgroup may translate, buffer, coalesce, or reorder output after receiving these bytes.','Absence of CR/LF in a literal does not prove the host will not append framing.','Runtime-built and indirect strings remain outside this static evidence set.'],
 'source_evidence':['Librarium/Evidence/native_host_text_template_contract_0017.json'],
 'summary':summary,'symbol_profiles':profiles,'records':records}
for p in [root/'Librarium/Evidence/native_host_text_framing_0019.json',root/'Reports/NativeDecode/native_host_text_framing_0019.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_text_framing_0019.py').write_text(Path(__file__).read_text())
cs=root/'Apps/Source/Reforged.Server/NativeHostTextFramingRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostTextFramingEvidence(','    string Symbol, IReadOnlyList<string> LineTerminationForms, int RecordsEndingCr,','    int RecordsEndingLf, int RecordsEndingCrLf, int RecordsWithTabs);','','public static class NativeHostTextFramingRegistry','{','    public const string EvidenceId = "FinalNativeClosure0019";','    public const string Scope = "Proven caller-owned text framing only; host translation and output ordering remain fail-closed.";','    public static readonly IReadOnlyList<NativeHostTextFramingEvidence> Proven = new NativeHostTextFramingEvidence[]','    {']
for p in profiles:
 forms=', '.join('"'+x+'"' for x in p['line_termination_forms'])
 lines.append(f'        new("{p["symbol"]}", new string[] {{ {forms} }}, {p["records_ending_cr"]}, {p["records_ending_lf"]}, {p["records_ending_crlf"]}, {p["records_with_tab"]}),')
lines+=['    };','}']; cs.write_text('\n'.join(lines)+'\n')
val=root/'Tools/Validation/validate_native_host_text_framing_0019.py'; val.parent.mkdir(parents=True,exist_ok=True)
val.write_text('''#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_text_framing_0019.json").read_text())
assert d["schema"]=="majormud.native-host-text-framing.v1"
s=d["summary"]
assert s["text_records"]==len(d["records"])>0
assert s["observed_occurrences"]==sum(x["occurrences"] for x in d["records"])
assert s["records_ending_cr"]==sum(x["ends_with_cr"] for x in d["records"])
assert s["records_ending_lf"]==sum(x["ends_with_lf"] for x in d["records"])
assert s["records_ending_crlf"]==sum(x["ends_with_crlf"] for x in d["records"])
assert s["records_with_embedded_nul"]==0
for x in d["records"]:
 assert x["ends_with_crlf"]==(x["text"].endswith("\\r\\n"))
 assert x["carriage_return_count"]==x["text"].count("\\r")
 assert x["line_feed_count"]==x["text"].count("\\n")
print("native host text framing validation passed")
''')
report=root/'Reports/NativeDecode/native_host_text_framing_closure_0019.md'
report.write_text(f'''# Final Native Closure 0019 — Host Text Framing Contract\n\n## Proven\n\n- Classified exact caller-owned framing for {summary['text_records']} proven host-facing text records covering {summary['observed_occurrences']} observed argument occurrences.\n- Identified {summary['records_with_carriage_return']} records containing carriage returns, {summary['records_with_line_feed']} containing line feeds, and {summary['records_with_tab']} containing tabs.\n- Identified {summary['records_ending_cr']} records ending in CR, {summary['records_ending_lf']} ending in LF, and {summary['records_ending_crlf']} ending in CRLF.\n- Identified {summary['records_with_cr_without_lf']} records with CR but no LF and {summary['records_with_lf_without_cr']} records with LF but no CR.\n- Confirmed no recovered static ASCII record contains an embedded NUL.\n\n## Runtime effect\n\n`NativeHostTextFramingRegistry` exposes the exact proven caller framing contract while refusing to emulate unsupported Worldgroup terminal translation, buffering, or output ordering.\n\n## Remaining boundary\n\nHost-side line translation, buffering, routing, coalescing, and final terminal byte ordering still require `WGSERVER.EXE`, symbols, headers, or dynamic traces.\n''')
ud=root/'Updates/FinalNativeClosure0019'; ud.mkdir(parents=True,exist_ok=True)
(ud/'README.md').write_text('FinalNativeClosure0019 recovers the exact caller-owned CR/LF/tab and whitespace framing contract for proven host-facing text.\n')
with (root/'CHANGELOG.md').open('a') as f:
 f.write('\n## FinalNativeClosure0019\n- Added exact caller-owned host text framing evidence for CR, LF, CRLF, tabs, and edge whitespace.\n- Added fail-closed runtime registry, analyzer, validator, report, and cumulative manifests.\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_text_framing_0019.py'),doraise=True); py_compile.compile(str(val),doraise=True)
subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')
manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0019.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inv(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inv(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0019','baseline':'FinalNativeClosure0018','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0019-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
