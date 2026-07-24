from pathlib import Path
import json,hashlib,datetime,zipfile,py_compile,re
root=Path('/mnt/data/nativepass4/base')
src=json.loads((root/'Librarium/Evidence/native_host_callsite_map_0003.json').read_text())

def domain(name):
 n=name.lower()
 if n.startswith('_dfa'): return 'host_persistence_dfa'
 if n in {'_lngrnd','_genrdn'} or 'rnd' in n: return 'host_randomness'
 if n in {'_today','_now','_ncdate','_nctime','_cofdat'} or 'time' in n or 'date' in n: return 'host_clock_calendar'
 if n in {'_begin_polling','_stop_polling','_rtkick'} or 'poll' in n: return 'host_polling_scheduler'
 if n.startswith('_prf') or n in {'_outprf','_clrprf','_shocst','_opnmsg','_clsmsg','_rsp2write'}: return 'host_message_rendering_transport'
 if n in {'_alczer','_alcmem','_alcblok','_galfree','_ptrblok'}: return 'host_memory'
 if n in {'_curusr','_usroff','_uacoff','_vdaoff','_dclvda','_depad'}: return 'host_session_user_state'
 if n in {'_parsin','_skpwht','_skpwrd','_rmvwht','_lastwd','_numopt','_lngopt','_stgopt','_ynopt'}: return 'host_input_parser'
 if n in {'_rstrin','_rstmbk','_setmbk'}: return 'host_buffer_state'
 if n in {'_sameto','_sameas','_samein','_haskey','_hasmkey'}: return 'host_identity_access'
 return 'host_general_runtime'
rows=[]
for f in src['functions']:
 mods={k:v for k,v in f['modules'].items()}
 rows.append({'symbol':f['name'],'domain':domain(f['name']),'total_direct_callsites':f['total_direct_callsites'],'modules':mods})
agg={}
for r in rows:
 a=agg.setdefault(r['domain'],{'domain':r['domain'],'symbol_count':0,'direct_callsite_count':0,'symbols':[]})
 a['symbol_count']+=1;a['direct_callsite_count']+=r['total_direct_callsites'];a['symbols'].append(r['symbol'])
for a in agg.values(): a['symbols'].sort()
out={'schema':'majorMud.native-host-service-ownership.v1','update':'FinalNativeClosure0004','generated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'evidence_class':'PROVEN','source_evidence':'Librarium/Evidence/native_host_callsite_map_0003.json','classification_method':'Deterministic symbol-name taxonomy over proven WGSERVER.EXE imports; classification establishes ownership domain only, not implementation semantics.','domains':sorted(agg.values(),key=lambda x:(-x['direct_callsite_count'],x['domain'])),'symbols':sorted(rows,key=lambda x:(x['domain'],x['symbol'])),'closed_boundaries':[
 'Native message formatting/output calls are owned by WGSERVER host services, not MMUD-local implementations.',
 'DFA record locking/query/update calls are owned by WGSERVER host services.',
 'Clock/calendar and polling entry points are owned by WGSERVER host services.',
 'Random generation entry points _lngrnd and _genrdn are owned by WGSERVER host services.',
 'User/session and input parsing primitives are owned by WGSERVER host services.'
 ],'limitations':['Ownership classification does not recover host algorithms, ordering, seeding, lock durability, or crash recovery.','Indirect calls remain outside the direct-callsite counts inherited from Update0003.']}
for p in [root/'Librarium/Evidence/native_host_service_ownership_0004.json',root/'Reports/NativeDecode/native_host_service_ownership_0004.json']:
 p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(out,indent=2)+'\n')
# runtime registry
cs=root/'Apps/Source/Reforged.Server/NativeHostServiceOwnershipRegistry.cs';cs.parent.mkdir(parents=True,exist_ok=True)
lines=['namespace Reforged.Server;','','public sealed record NativeHostServiceDomain(string Domain, int SymbolCount, int DirectCallsiteCount);','','public static class NativeHostServiceOwnershipRegistry','{','    public const string EvidenceId = "FinalNativeClosure0004";','    public const string Scope = "Proven WGSERVER import ownership domains; host algorithms remain fail-closed.";','    public static readonly IReadOnlyList<NativeHostServiceDomain> Proven = new NativeHostServiceDomain[]','    {']
for a in out['domains']: lines.append(f'        new("{a["domain"]}", {a["symbol_count"]}, {a["direct_callsite_count"]}),')
lines += ['    };','}'];cs.write_text('\n'.join(lines)+'\n')
# generator copy
gp=root/'Tools/NativeDecode/classify_native_host_services_0004.py';gp.parent.mkdir(parents=True,exist_ok=True);gp.write_text(Path('/tmp/make_pass4.py').read_text())
# validator
vp=root/'Tools/Validation/validate_native_host_service_ownership_0004.py';vp.write_text("""import json,pathlib\nroot=pathlib.Path(__file__).parents[2]\nd=json.loads((root/'Librarium/Evidence/native_host_service_ownership_0004.json').read_text())\nassert d['schema']=='majorMud.native-host-service-ownership.v1'\nassert len(d['symbols'])>=149\nby={x['domain']:x for x in d['domains']}\nfor k in ['host_persistence_dfa','host_randomness','host_clock_calendar','host_polling_scheduler','host_message_rendering_transport','host_input_parser','host_session_user_state']:\n assert k in by and by[k]['symbol_count']>0\nsy={x['symbol']:x['domain'] for x in d['symbols']}\nassert sy['_lngrnd']=='host_randomness' and sy['_genrdn']=='host_randomness'\nassert sy['_dfaAcqLock']=='host_persistence_dfa'\nassert sy['_prf']=='host_message_rendering_transport'\nprint('native host service ownership validation passed')\n""")
# report
r=root/'Reports/NativeDecode/native_host_service_boundary_closure_0004.md'
r.write_text('# Final Native Closure 0004 — WGSERVER Service Ownership\n\n## Proven\n\n- Classified every proven WGSERVER import from MMUD/MMPLS into deterministic host-service ownership domains.\n- Native message rendering/transport, DFA persistence and locking, RNG, clock/calendar, polling, input parsing, user/session state, memory, and identity/access primitives are host-owned boundaries.\n- `_lngrnd` and `_genrdn` are both host RNG services; MMUD does not contain their algorithms.\n- DFA mutation and lock symbols are host persistence services; save durability, checksums, migration, and recovery cannot be inferred from MMUD callsites alone.\n\n## Runtime effect\n\n`NativeHostServiceOwnershipRegistry` exposes proven ownership categories and keeps unavailable host algorithms fail-closed.\n\n## Remaining native dependency\n\nThe matching WGSERVER executable/runtime remains required to recover the implementation, exact ordering, seeding, locking semantics, serialization, checksum, migration, and crash recovery behind these imports.\n')
ud=root/'Updates/FinalNativeClosure0004';ud.mkdir(parents=True,exist_ok=True);(ud/'README.md').write_text('FinalNativeClosure0004 classifies proven WGSERVER imports into native service ownership domains and adds a fail-closed runtime ownership registry.\n')
with (root/'CHANGELOG.md').open('a') as f:f.write('\n## FinalNativeClosure0004\n- Classified proven WGSERVER imports into host-service ownership domains.\n- Added runtime ownership registry, generator, validator, Librarium evidence, and native boundary report.\n')
# manifests after files excluding manifests themselves first then regenerate stable enough
manifest_paths=[root/'CumulativeManifest.json',ud/'CumulativeManifest.json',root/'Manifests/native_semantic_exhaustion_cumulative_manifest_0004.json']
for p in manifest_paths:
 p.parent.mkdir(parents=True,exist_ok=True)
 if p.exists(): p.unlink()
files=[]
for p in sorted(root.rglob('*')):
 if p.is_file() and '__pycache__' not in p.parts:
  files.append({'path':p.relative_to(root).as_posix(),'size':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
man={'schema':'majorMud.cumulative-manifest.v1','update':'FinalNativeClosure0004','baseline':'FinalNativeClosure0003','file_count':len(files),'files':files}
for p in manifest_paths:p.write_text(json.dumps(man,indent=2)+'\n')
# validation
for p in root.rglob('*.json'): json.loads(p.read_text())
py_compile.compile(str(gp),doraise=True);py_compile.compile(str(vp),doraise=True)
import subprocess;subprocess.check_call(['python',str(vp)])
outzip=Path('/mnt/data/MajorMUD-Reforged-Final-Native-Closure-Update0004-Cumulative-Changed-Files-Only.zip')
with zipfile.ZipFile(outzip,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and '__pycache__' not in p.parts:z.write(p,p.relative_to(root))
with zipfile.ZipFile(outzip) as z: assert z.testzip() is None
print(outzip);print(hashlib.sha256(outzip.read_bytes()).hexdigest());print(json.dumps({a['domain']:(a['symbol_count'],a['direct_callsite_count']) for a in out['domains']},indent=2))
