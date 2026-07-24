from __future__ import annotations
import json, shutil, hashlib, datetime as dt, zipfile, py_compile, subprocess, struct, re
from pathlib import Path
from collections import Counter

srczip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0015-Cumulative-Changed-Files-Only.zip')
repozip=Path('/mnt/data/MajorMud-Reforged Github(59).zip')
work=Path('/mnt/data/native_pass16'); root=work/'base16'; bins=work/'bins'
if work.exists(): shutil.rmtree(work)
root.mkdir(parents=True); bins.mkdir(parents=True)
with zipfile.ZipFile(srczip) as z: z.extractall(root)

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

names={
'wccmmud.dll':'ImportedDatabases/WCCDefaultInstall/wcc default/wccmmud.dll',
'wccmmpls.dll':'ImportedDatabases/WCCDefaultInstall/wcc default/wccmmpls.dll',
'wbbmmud.dll':'ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmud.dll',
'wbbmmpls.dll':'ImportedDatabases/WCCLabeledWBB/Extracted/wbbmmpls.dll'}
with zipfile.ZipFile(repozip) as z:
    for n,arc in names.items():
        (bins/n).write_bytes(z.read(arc))

class PE:
    def __init__(self,p):
        self.data=p.read_bytes(); d=self.data
        pe=struct.unpack_from('<I',d,0x3c)[0]; assert d[pe:pe+4]==b'PE\0\0'
        coff=pe+4; nsec=struct.unpack_from('<H',d,coff+2)[0]; optsz=struct.unpack_from('<H',d,coff+16)[0]
        opt=coff+20; self.image_base=struct.unpack_from('<I',d,opt+28)[0]; self.size_image=struct.unpack_from('<I',d,opt+56)[0]
        sec=opt+optsz; self.sections=[]
        for i in range(nsec):
            o=sec+i*40; name=d[o:o+8].split(b'\0',1)[0].decode('ascii','replace')
            vs,va,rs,rp=struct.unpack_from('<IIII',d,o+8)
            self.sections.append({'name':name,'va':va,'vsize':vs,'raw_size':rs,'raw_ptr':rp})
    def locate(self,va):
        rva=va-self.image_base
        for s in self.sections:
            if s['va'] <= rva < s['va']+max(s['vsize'],s['raw_size']):
                off=s['raw_ptr']+(rva-s['va'])
                if 0 <= off < len(self.data): return s,off
        return None,None
    def ascii(self,va,maxlen=240):
        s,o=self.locate(va)
        if s is None: return None
        b=self.data[o:o+maxlen]; end=b.find(b'\0')
        if end<0: return None
        b=b[:end]
        if len(b)<2 or any(x not in b'\t\r\n' and not (32<=x<127) for x in b): return None
        return b.decode('ascii','replace')

pes={n:PE(bins/n) for n in names}
prior=json.loads((root/'Reports/NativeDecode/native_host_argument_positions_0014.json').read_text())
records=[]; counts=Counter(); strings=[]
for sym in prior['symbols']:
  for mod,prof in sym['modules'].items():
    pe=pes[mod]
    for pos in prof['positions']:
      for raw,cnt in pos.get('value_histogram',{}).items():
        if not (raw=='0' or raw.startswith('0x')): continue
        try: val=int(raw,0)&0xffffffff
        except: continue
        signed=val if val<0x80000000 else val-0x100000000
        section,off=pe.locate(val)
        text=pe.ascii(val) if section else None
        if val==0: cls='null_literal'
        elif val==0xffffffff or signed<0: cls='signed_sentinel_or_large_u32'
        elif section:
            cls='module_ascii_address' if text is not None else 'module_data_or_code_address'
        elif val<=0xffff: cls='small_scalar'
        else: cls='external_or_large_scalar'
        counts[cls]+=cnt
        rec={'symbol':sym['symbol'],'module':mod,'position':pos['position'],'raw':raw,'value_u32':val,'value_signed':signed,'occurrences':cnt,'classification':cls,'module_section':section['name'] if section else None,'module_file_offset':off,'ascii_text':text}
        records.append(rec)
        if text is not None: strings.append(rec)

# exact WCC/WBB parity by counterpart normalized classification, allowing embedded distribution labels to differ in text
pairs=[('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]
parity=[]
for a,b in pairs:
    def sig(mod):
      c=Counter()
      for r in records:
       if r['module']==mod:
        c[(r['symbol'],r['position'],r['classification'],r['module_section'])] += r['occurrences']
      return sorted(c.items(),key=repr)
    parity.append({'pair':f'{a}:{b}','matching_classification_profile':sig(a)==sig(b)})

summary={'distinct_immediate_records':len(records),'observed_immediate_occurrences':sum(r['occurrences'] for r in records),'module_address_occurrences':counts['module_ascii_address']+counts['module_data_or_code_address'],'ascii_address_records':len(strings),'classification_occurrences':dict(counts),'wcc_wbb_pairs_matching':sum(x['matching_classification_profile'] for x in parity)}
data={'schema':'majormud.native-host-immediate-provenance.v1','update':'FinalNativeClosure0016','generated_utc':dt.datetime.now(dt.timezone.utc).isoformat(),'evidence_class':'PROVEN_CALLER_IMMEDIATE_PROVENANCE','method':'Map immediate caller argument values from Update0014 positional evidence into the exact supplied PE images. Classify nulls, signed sentinels, in-module addresses, small scalars, and external/large values. Decode only NUL-terminated printable ASCII at proven mapped addresses.','interpretation_limits':['An in-module address proves pointer provenance, not pointee type or parameter declaration.','Printable ASCII proves string bytes at that address, not host ownership or format semantics.','External or large values may be scalars or addresses outside the module and remain untyped.','Exact host prototypes still require Worldgroup binaries, symbols, headers, or dynamic traces.'],'modules':{n:{'sha256':sha(bins/n),'image_base':pes[n].image_base,'size_of_image':pes[n].size_image,'sections':pes[n].sections} for n in names},'summary':summary,'distribution_parity':parity,'records':records}
for p in [root/'Librarium/Evidence/native_host_immediate_provenance_0016.json',root/'Reports/NativeDecode/native_host_immediate_provenance_0016.json']:
 p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(data,indent=2)+'\n')
(root/'Tools/NativeDecode/analyze_native_host_immediate_provenance_0016.py').write_text(Path(__file__).read_text())

cs=root/'Apps/Source/Reforged.Server/NativeHostImmediateProvenanceRegistry.cs'
lines=['namespace Reforged.Server;','','public sealed record NativeHostImmediateProvenanceEvidence(','    string Symbol, string Module, int Position, uint Value, string Classification, string? Section, string? AsciiText);','','public static class NativeHostImmediateProvenanceRegistry','{','    public const string EvidenceId = "FinalNativeClosure0016";','    public const string Scope = "Proven immediate provenance only; native parameter types remain fail-closed.";','    public static readonly IReadOnlyList<NativeHostImmediateProvenanceEvidence> Proven = new NativeHostImmediateProvenanceEvidence[]','    {']
for r in records:
 if r['classification'] not in ('module_ascii_address','module_data_or_code_address','null_literal','signed_sentinel_or_large_u32'): continue
 txt='null' if r['ascii_text'] is None else '"'+r['ascii_text'].replace('\\','\\\\').replace('"','\\"').replace('\r','\\r').replace('\n','\\n').replace('\t','\\t')+'"'
 sec='null' if r['module_section'] is None else '"'+r['module_section']+'"'
 lines.append(f'        new("{r["symbol"]}", "{r["module"]}", {r["position"]}, 0x{r["value_u32"]:08X}u, "{r["classification"]}", {sec}, {txt}),')
lines += ['    };','}']; cs.write_text('\n'.join(lines)+'\n')

val=root/'Tools/Validation/validate_native_host_immediate_provenance_0016.py'
val.write_text('''#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).parents[2]
d=json.loads((r/"Librarium/Evidence/native_host_immediate_provenance_0016.json").read_text())
assert d["schema"]=="majormud.native-host-immediate-provenance.v1"
assert d["summary"]["distinct_immediate_records"]>0
assert d["summary"]["module_address_occurrences"]>0
assert d["summary"]["ascii_address_records"]>0
assert all(x["matching_classification_profile"] for x in d["distribution_parity"])
for x in d["records"]:
 assert x["occurrences"]>0
 if x["classification"].startswith("module_"): assert x["module_section"] is not None
 if x["ascii_text"] is not None: assert x["classification"]=="module_ascii_address"
print("native host immediate provenance validation passed")
''')
report=root/'Reports/NativeDecode/native_host_immediate_provenance_closure_0016.md'
report.write_text(f'''# Final Native Closure 0016 — Host Immediate Provenance\n\n## Proven\n\n- Classified {summary['distinct_immediate_records']} distinct immediate argument records covering {summary['observed_immediate_occurrences']} observed callsite occurrences.\n- Proved {summary['module_address_occurrences']} occurrences are addresses inside the exact supplied MMUD/MMPLS PE images.\n- Recovered {summary['ascii_address_records']} distinct mapped printable ASCII argument records.\n- Separated null literals, signed-sentinel/large-u32 values, in-module pointers, small scalars, and unresolved external/large values.\n- WCC/WBB immediate classification profiles match for both MMUD and MMPLS pairs.\n\n## Runtime effect\n\n`NativeHostImmediateProvenanceRegistry` exposes only proven pointer/string provenance, nulls, and sentinels. It does not assign unsupported parameter types or host semantics.\n\n## Remaining boundary\n\nExact parameter declarations, external pointer ownership, and host-side interpretation still require the matching Worldgroup host binary, symbols, headers, or runtime traces.\n''')
ud=root/'Updates/FinalNativeClosure0016'; ud.mkdir(parents=True,exist_ok=True); (ud/'README.md').write_text('FinalNativeClosure0016 maps immediate host-call arguments into exact PE address provenance and printable ASCII evidence.\n')
with (root/'CHANGELOG.md').open('a') as f: f.write('\n## FinalNativeClosure0016\n- Added exact PE provenance classification for immediate WGSERVER call arguments.\n- Added mapped ASCII evidence, fail-closed runtime registry, analyzer, validator, report, and cumulative manifests.\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(root/'Tools/NativeDecode/analyze_native_host_immediate_provenance_0016.py'),doraise=True); py_compile.compile(str(val),doraise=True)
subprocess.check_call(['python',str(val)]); assert cs.read_text().count('{')==cs.read_text().count('}')
manpaths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0016.json']
for p in manpaths: p.parent.mkdir(parents=True,exist_ok=True); p.write_text('{}\n')
def inventory(): return [{'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':sha(p)} for p in sorted(root.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
files=inventory(); man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0016','baseline':'FinalNativeClosure0015','file_count':len(files),'files':files}
for p in manpaths: p.write_text(json.dumps(man,indent=2)+'\n')
for p in root.rglob('*.json'): json.loads(p.read_text())
out=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0016-Cumulative-Changed-Files-Only.zip')
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts: z.write(p,p.relative_to(root))
with zipfile.ZipFile(out) as z: assert z.testzip() is None
print(json.dumps({'out':str(out),'sha256':sha(out),'size':out.stat().st_size,'summary':summary},indent=2))
