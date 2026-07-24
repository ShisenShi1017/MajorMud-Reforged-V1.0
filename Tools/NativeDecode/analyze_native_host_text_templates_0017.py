from __future__ import annotations
import json, hashlib, datetime as dt, zipfile, py_compile, re, shutil
from pathlib import Path
from collections import Counter, defaultdict

src=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0016-Cumulative-Changed-Files-Only.zip')
work=Path('/mnt/data/native_pass17'); root=work/'base'
if work.exists(): shutil.rmtree(work)
root.mkdir(parents=True)
with zipfile.ZipFile(src) as z: z.extractall(root)

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
prior=json.loads((root/'Reports/NativeDecode/native_host_immediate_provenance_0016.json').read_text())
# Conservative C printf conversion scanner. %% is an escaped percent and not an argument conversion.
fmt_re=re.compile(r'%(?:\d+\$)?[-+ #0\']*(?:\*|\d+)?(?:\.(?:\*|\d+))?(?:hh|h|ll|l|L|j|z|t|I32|I64)?([diuoxXfFeEgGaAcspn])')
records=[]
feature_occ=Counter(); directive_occ=Counter(); symbol_strings=Counter(); distinct_text=set(); shared=defaultdict(set)
for r in prior['records']:
    text=r.get('ascii_text')
    if text is None: continue
    directives=[m.group(0) for m in fmt_re.finditer(text.replace('%%',''))]
    conv=[m.group(1) for m in fmt_re.finditer(text.replace('%%',''))]
    features=[]
    if directives: features.append('printf_conversion_template')
    else: features.append('literal_ascii')
    if '%%' in text: features.append('escaped_percent')
    if '\r' in text: features.append('carriage_return')
    if '\n' in text: features.append('line_feed')
    if '\t' in text: features.append('horizontal_tab')
    if text.startswith(' ') or text.endswith(' '): features.append('edge_whitespace')
    if not text.strip(): features.append('whitespace_only')
    rec={
      'symbol':r['symbol'],'module':r['module'],'position':r['position'],'address_u32':r['value_u32'],
      'occurrences':r['occurrences'],'section':r['module_section'],'text':text,
      'features':features,'format_directives':directives,'conversion_codes':conv,
      'conversion_count':len(directives),'byte_length':len(text.encode('ascii'))
    }
    records.append(rec); distinct_text.add(text); symbol_strings[r['symbol']]+=1
    shared[text].add((r['module'],r['symbol'],r['position']))
    for f in features: feature_occ[f]+=r['occurrences']
    for c in conv: directive_occ[c]+=r['occurrences']

pairs=[('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]
parity=[]
for a,b in pairs:
    def sig(mod):
        c=Counter()
        for r in records:
            if r['module']==mod:
                # text bytes can contain distribution labels; syntactic contract is the proven parity target.
                c[(r['symbol'],r['position'],tuple(r['features']),tuple(r['conversion_codes']),r['byte_length'])]+=r['occurrences']
        return c
    ca,cb=sig(a),sig(b)
    parity.append({'pair':f'{a}:{b}','matching_syntactic_contract':ca==cb,
                   'wcc_record_occurrences':sum(ca.values()),'wbb_record_occurrences':sum(cb.values())})

summary={
 'ascii_records':len(records),'distinct_ascii_texts':len(distinct_text),
 'observed_ascii_argument_occurrences':sum(r['occurrences'] for r in records),
 'printf_template_records':sum(1 for r in records if r['conversion_count']>0),
 'literal_records':sum(1 for r in records if r['conversion_count']==0),
 'format_conversion_occurrences':sum(len(r['format_directives'])*r['occurrences'] for r in records),
 'feature_occurrences':dict(sorted(feature_occ.items())),
 'conversion_code_occurrences':dict(sorted(directive_occ.items())),
 'symbols_with_ascii_arguments':len(symbol_strings),
 'texts_referenced_from_multiple_sites':sum(1 for v in shared.values() if len(v)>1),
 'wcc_wbb_pairs_matching':sum(x['matching_syntactic_contract'] for x in parity)
}
data={
 'schema':'majormud.native-host-text-template-contract.v1','update':'FinalNativeClosure0017',
 'generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),
 'evidence_class':'PROVEN_CALLER_TEXT_TEMPLATE_SYNTAX',
 'method':'Derive only syntactic properties from printable ASCII addresses proven in FinalNativeClosure0016: literal versus C printf-style conversion template, exact conversion directives, CR/LF/TAB controls, escaped percent markers, byte lengths, host symbol, argument position, and occurrence counts.',
 'interpretation_limits':[
   'A printf-shaped directive proves caller-supplied template syntax, not the exact Worldgroup formatter implementation.',
   'Literal text may still be interpreted by a host service according to semantics unavailable without WGSERVER.EXE.',
   'Directive syntax does not by itself prove the native type of the corresponding variadic argument.',
   'Only immediate in-module printable ASCII pointers are represented; runtime-built and indirect strings remain outside this pass.'
 ],
 'source_evidence':'Librarium/Evidence/native_host_immediate_provenance_0016.json',
 'summary':summary,'distribution_parity':parity,'symbol_record_counts':dict(sorted(symbol_strings.items())),
 'records':records
}
for p in [root/'Librarium/Evidence/native_host_text_template_contract_0017.json',root/'Reports/NativeDecode/native_host_text_template_contract_0017.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')

an=root/'Tools/NativeDecode/analyze_native_host_text_templates_0017.py'; an.parent.mkdir(parents=True,exist_ok=True)
an.write_text(Path(__file__).read_text())

# Runtime registry intentionally exposes syntax only and performs no rendering.
def esc(s): return s.replace('\\','\\\\').replace('"','\\"').replace('\r','\\r').replace('\n','\\n').replace('\t','\\t')
cs=root/'Apps/Source/Reforged.Server/NativeHostTextTemplateRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostTextTemplateEvidence(','    string Symbol, string Module, int Position, uint Address, string Text,','    IReadOnlyList<string> FormatDirectives, IReadOnlyList<string> Features);','','public static class NativeHostTextTemplateRegistry','{','    public const string EvidenceId = "FinalNativeClosure0017";','    public const string Scope = "Proven caller text/template syntax only; Worldgroup rendering remains fail-closed.";','    public static readonly IReadOnlyList<NativeHostTextTemplateEvidence> Proven = new NativeHostTextTemplateEvidence[]','    {']
for r in records:
    ds=', '.join('"'+esc(x)+'"' for x in r['format_directives'])
    fs=', '.join('"'+esc(x)+'"' for x in r['features'])
    lines.append(f'        new("{esc(r["symbol"])}", "{r["module"]}", {r["position"]}, 0x{r["address_u32"]:08X}u, "{esc(r["text"])}", new string[] {{ {ds} }}, new string[] {{ {fs} }}),')
lines+=['    };','','    public static bool RequiresNativeFormatter(NativeHostTextTemplateEvidence evidence) =>','        evidence.FormatDirectives.Count != 0;','}']
cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_text_templates_0017.py'; val.parent.mkdir(parents=True,exist_ok=True)
val.write_text('''#!/usr/bin/env python3
import json, pathlib, re
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_text_template_contract_0017.json").read_text())
assert d["schema"]=="majormud.native-host-text-template-contract.v1"
s=d["summary"]
assert s["ascii_records"]==len(d["records"])>0
assert s["printf_template_records"]+s["literal_records"]==s["ascii_records"]
assert s["observed_ascii_argument_occurrences"]==sum(x["occurrences"] for x in d["records"])
assert s["format_conversion_occurrences"]==sum(x["conversion_count"]*x["occurrences"] for x in d["records"])
assert all(x["matching_syntactic_contract"] for x in d["distribution_parity"])
for x in d["records"]:
 assert x["occurrences"]>0 and x["byte_length"]==len(x["text"].encode("ascii"))
 assert x["conversion_count"]==len(x["format_directives"])==len(x["conversion_codes"])
 assert ("printf_conversion_template" in x["features"]) == (x["conversion_count"]>0)
print("native host text template contract validation passed")
''')
report=root/'Reports/NativeDecode/native_host_text_template_closure_0017.md'
report.write_text(f'''# Final Native Closure 0017 — Host Text Template Contract

## Proven

- Classified {summary['ascii_records']} immediate host-facing ASCII records covering {summary['observed_ascii_argument_occurrences']} observed argument occurrences.
- Separated {summary['printf_template_records']} printf-style template records from {summary['literal_records']} literal records.
- Recovered {summary['format_conversion_occurrences']} conversion occurrences and their exact caller-side directive syntax.
- Recorded CR, LF, TAB, escaped-percent, edge-whitespace, byte-length, symbol, argument-position, and address evidence.
- Confirmed WCC/WBB syntactic-contract parity for both MMUD and MMPLS module pairs.

## Runtime effect

`NativeHostTextTemplateRegistry` exposes proven templates and marks templates requiring a native-compatible formatter. It intentionally does not render them or assign unsupported variadic parameter types.

## Remaining boundary

The exact Worldgroup formatter, runtime-built strings, variadic argument types, message routing, terminal translation, and output ordering still require `WGSERVER.EXE`, symbols, headers, or dynamic traces.
''')
ud=root/'Updates/FinalNativeClosure0017'; ud.mkdir(parents=True,exist_ok=True)
(ud/'README.md').write_text('FinalNativeClosure0017 recovers the proven caller-side host text/template syntax contract without emulating unavailable Worldgroup formatting behavior.\n')
with (root/'CHANGELOG.md').open('a') as f:
 f.write('\n## FinalNativeClosure0017\n- Added host-facing ASCII literal and printf-template syntax classification.\n- Added exact conversion, control-character, parity, runtime registry, analyzer, validator, report, and cumulative manifests.\n')

# validate everything before manifests
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(an),doraise=True); py_compile.compile(str(val),doraise=True)
import subprocess
subprocess.check_call(['python',str(val)])
assert cs.read_text().count('{')==cs.read_text().count('}')

manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0017.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inv(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inv(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0017','baseline':'FinalNativeClosure0016','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0017-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
