#!/usr/bin/env python3
import hashlib, json, re
from collections import Counter
from pathlib import Path

root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append({'check':name,'detail':detail})
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))
def sha(rel): return hashlib.sha256((root/rel).read_bytes()).hexdigest()

evidence=js('Librarium/Parity/majormud_native_rng_callsite_boundary_evidence_0306.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0306.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0306.json')
callmap=js('Librarium/Evidence/native_host_callsite_map_0003.json')
argpos=js('Librarium/Evidence/native_host_argument_positions_0014.json')
ownership=js('Librarium/Evidence/native_host_service_ownership_0004.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
catalog=js('Librarium/RemainingSystems/global_random_catalog.json')
code=text('Apps/Source/Reforged.Server/WorldgroupRngDirectCallsiteRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass35ClassicWorldgroupRngCallsiteBoundarySmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
rawrng=text('Apps/Source/Reforged.Server/RuntimeClassicWorldgroupPrng.cs')

# Exact retained native versions and direct callsite map.
mods={m['file'].lower():m for m in callmap['modules']}
req(callmap.get('evidence_class')=='PROVEN','callmap-proven')
req('indirect register/data calls are explicitly excluded' in callmap.get('method',''),'callmap-indirect-excluded')
expected_hashes={
 'wccmmud.dll':'5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077',
 'wbbmmud.dll':'88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091'}
for mod,expected_hash in expected_hashes.items():
    req(mod in mods,'module-present:'+mod)
    req(mods[mod]['sha256']==expected_hash,'module-hash:'+mod)
    imports={x['name']:x for x in mods[mod]['imports']}
    req(imports['_genrdn']['direct_callsite_count']==146,'genrdn-count:'+mod)
    req(imports['_genrdn']['thunk_va'].lower()=='0x00478cb2','genrdn-thunk:'+mod)
    req(len(imports['_genrdn']['direct_callsites'])==64,'genrdn-retained-address-count:'+mod)
    req(imports['_lngrnd']['direct_callsite_count']==42,'lngrnd-count:'+mod)
    req(imports['_lngrnd']['thunk_va'].lower()=='0x004789e2','lngrnd-thunk:'+mod)
    req(len(imports['_lngrnd']['direct_callsites'])==42,'lngrnd-list-count:'+mod)

funcs={x['name']:x for x in callmap['functions']}
req(funcs['_genrdn']['total_direct_callsites']==292,'combined-genrdn-292')
req(funcs['_lngrnd']['total_direct_callsites']==84,'combined-lngrnd-84')
for mod in expected_hashes:
    req(funcs['_genrdn']['modules'][mod]['direct_callsite_count']==146,'function-genrdn:'+mod)
    req(funcs['_lngrnd']['modules'][mod]['direct_callsite_count']==42,'function-lngrnd:'+mod)

# Host service ownership matches callsite evidence.
syms={x['symbol']:x for x in ownership['symbols']}
for sym,total in [('_genrdn',292),('_lngrnd',84)]:
    req(syms[sym]['domain']=='host_randomness','ownership-domain:'+sym)
    req(syms[sym]['total_direct_callsites']==total,'ownership-total:'+sym)

# Bounded positional analyzer coverage is retained as bounded, not promoted.
for module in argpos['modules']:
    name=module['file'].lower()
    if name not in expected_hashes: continue
    rows={x['symbol']:x for x in module['imports']}
    req(rows['_genrdn']['direct_callsite_count']==64,'argpos-genrdn-bounded:'+name)
    req(rows['_lngrnd']['direct_callsite_count']==42,'argpos-lngrnd-bounded:'+name)
    req(rows['_genrdn']['direct_callsite_count']<146,'argpos-genrdn-explicitly-incomplete:'+name)

# Pass35 evidence restates only the exact direct boundary.
req(evidence.get('schema')=='majormud.classic-worldgroup-rng-direct-callsite-boundary.v1','evidence-schema')
req(evidence.get('update')=='0306' and evidence.get('pass')==35,'evidence-pass35')
req(evidence.get('evidenceClass')=='PROVEN_STATIC_DIRECT_BOUNDARY','evidence-class')
req(evidence['combined']['_genrdn']['directCallsiteCount']==292 and evidence['combined']['_genrdn']['retainedDirectAddressCount']==128 and evidence['combined']['_genrdn']['boundedArgumentAnalyzerCallsiteCount']==128,'evidence-genrdn-combined')
req(evidence['combined']['_lngrnd']['directCallsiteCount']==84 and evidence['combined']['_lngrnd']['retainedDirectAddressCount']==84 and evidence['combined']['_lngrnd']['boundedArgumentAnalyzerCallsiteCount']==84,'evidence-lngrnd-combined')
req(evidence['runtimeActivation']['gameplayRandomCallerRewrites']==0,'zero-gameplay-rng-rewrites')
req(evidence['runtimeActivation']['globalClassicStreamInvented'] is False,'no-global-classic-stream')
req(evidence['runtimeActivation']['boundedRangeLoopActivated'] is False,'range-loop-not-activated')
req(len(evidence['stillUnknown'])==5,'five-rng-integration-unknowns')
req('Do not treat the 64 observed _genrdn rows as a complete direct-call argument map.'==evidence['argumentAnalyzerBoundary']['note'],'bounded-argument-warning')
req(evidence['directCallsiteMapBoundary']['genrdnRetainedAddressCountPerModule']==64 and evidence['directCallsiteMapBoundary']['genrdnCensusCountPerModule']==146,'retained-genrdn-address-boundary')

# Executable registry is version-pinned and cannot silently promote unresolved integration.
for token in [
 'WorldgroupRngDirectCallsiteRegistry','5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077',
 '88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091',
 '0x00478CB2u, 146, 64, 64','0x004789E2u, 42, 42, 42',
 'IndirectCallsitesResolved = false','GameplaySemanticMappingResolved = false',
 'ExecutionThreadCrtMappingResolved = false','GameplayDrawOrderingResolved = false',
 'InstructionEquivalentRangeLoopPortResolved = false',
 'NativeHostCallsiteRegistry.Proven','NativeHostArgumentPositionRegistry.Proven']:
    req(token in code,'registry-token:'+token[:34])
req('RuntimeClassicWorldgroupPrng' not in code,'registry-does-not-own-prng-state')
req('Rand()' not in code,'registry-does-not-draw')
req('NextInclusive' not in code,'registry-does-not-invent-inclusive-range')

# Smoke checks boundary only; it never draws or mutates gameplay RNG state.
for token in ['genrdn-combined-direct-call-count-292','lngrnd-combined-direct-call-count-84',
              'genrdn-retained-address-subset-128','lngrnd-retained-address-list-84',
              'genrdn-bounded-argument-coverage-128','lngrnd-bounded-argument-coverage-84',
              'indirect-calls-stay-unresolved','gameplay-callsite-semantics-stay-unresolved',
              'execution-thread-crt-mapping-stays-unresolved','gameplay-draw-order-stays-unresolved',
              'range-loop-port-stays-unresolved']:
    req(token in smoke,'smoke:'+token)
req('RuntimeClassicWorldgroupPrng' not in smoke and '.Rand()' not in smoke,'smoke-no-prng-draw')
req('--smoke-pass35-classic-worldgroup-rng-callsite-boundary' in program,'pass35-smoke-cli')
req('RuntimePass35ClassicWorldgroupRngCallsiteBoundarySmokeReport.Run()' in program,'pass35-smoke-run')
req(program.count('--smoke-pass35-classic-worldgroup-rng-callsite-boundary')==1,'pass35-smoke-cli-single')

# Pass34 raw host generator remains untouched in semantics.
for token in ['WorldgroupPrngRuntimeRegistry.AdvanceSeed(previous)','WorldgroupPrngRuntimeRegistry.ProjectRand15(next)','_seed = seed;','_sequence = 0;']:
    req(token in rawrng,'pass34-raw-prng-preserved:'+token[:25])
req('NextInclusive' not in rawrng,'pass34-no-range-invention-preserved')

# Ledger/scorecard: exactly one new narrow PASS; broad RNG integration remains UNKNOWN.
byid={a['id']:a for a in ledger['assertions']}; counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0306','ledger-update-0306')
req(len(ledger['assertions'])==200,'ledger-total-200')
req(counts==Counter({'PASS':97,'STRONG':76,'UNKNOWN':27}),'ledger-counts',str(counts))
req(byid.get('native.global-rng.direct-callsite-boundary',{}).get('state')=='PASS','ledger-direct-boundary-pass')
req(byid.get('native.global-rng.native-contract',{}).get('state')=='UNKNOWN','ledger-broad-rng-unknown')
unknown=byid['native.global-rng.native-contract']['description']
for token in ['gameplay PRNG draw ordering','direct/indirect','execution-thread CRT state','instruction-equivalent _genrdn/_lngrnd']:
    req(token in unknown,'ledger-unknown:'+token[:25])
for aid in ['native.global-rng.borland-crt-core','native.global-rng.startup-packed-time-seed','native.global-rng.host-range-endpoint-contract']:
    req(byid.get(aid,{}).get('state')=='PASS','prior-rng-pass:'+aid)
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,76,27,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict-48.5')
req(abs(score['EvidenceBackedPercent']-86.5)<0.01,'score-evidence-86.5')
cat={c['Category']:c for c in score['Categories']}
req((cat['Host / Persistence / Worldgroup']['Pass'],cat['Host / Persistence / Worldgroup']['Strong'],cat['Host / Persistence / Worldgroup']['Unknown'],cat['Host / Persistence / Worldgroup']['Total'])==(4,1,4,9),'score-host-category')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-current-ledger')
req('majormud_parity_assertions_0305.json' not in audit,'runtime-audit-old-ledger-retired')

# Remaining-system policy keeps all unsafe activation gates closed.
req(catalog.get('pass')=='Update0306','catalog-pass0306')
req('direct randomness import census' in catalog.get('classification',''),'catalog-direct-boundary')
req('classicDirectCallBoundary' in catalog.get('ownership',{}),'catalog-boundary-owner')
req(any('64-per-module _genrdn' in x for x in catalog.get('safeFallback',[])),'catalog-bounded-genrdn-warning')
for token in ['exact MajorMUD gameplay draw/mutation ordering','execution-thread CRT state','instruction-equivalent Reforged activation']:
    req(any(token in x for x in catalog.get('nativeDependencies',[])),'catalog-unknown:'+token[:25])
systems={s['id']:s for s in registry['systems']}
req(registry.get('updatedByPass') in {'Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313'},'registry-pass35')
gr=systems['global-rng']
req(gr.get('status')=='classic-native-core-and-direct-call-boundary-proven-gameplay-call-order-bounded','registry-rng-status')
cb=gr.get('classicDirectCallBoundary0306',{})
req(cb.get('state')=='PASS-narrow','registry-direct-boundary-pass-narrow')
req(cb.get('genrdnDirectPerModule')==146 and cb.get('lngrndDirectPerModule')==42,'registry-direct-counts')
req(cb.get('indirectCallsResolved') is False and cb.get('gameplaySemanticMappingResolved') is False,'registry-unknown-flags-false')

# User-protected behavior/source remains present and byte-identical to the reconstructed Pass34 baseline.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'76d6dab9d3c1384bdbe2d6bd7c7c9464ea3c99130221cbbdf1ec36bcd7d86731',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected in protected.items():
    req((root/rel).exists(),'protected-exists:'+Path(rel).name)
    req(sha(rel)==expected,'protected-hash:'+Path(rel).name)
app=text('Apps/Source/Reforged.App/App.xaml.cs'); main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

# Prior parity/runtime fixes highlighted by Pass34 validator remain intact.
req('MaximumPvpLevelDifference = 100' in text('Apps/Source/Reforged.Server/RuntimeClassicConfigurationAuthority.cs'),'pass33-pvp-default-preserved')
req('HighestUncontestedGroup = 126' in text('Apps/Source/Reforged.Server/RuntimeClassicBroadcastGroupAuthority.cs'),'pass30-group126-preserved')
req('"laws" => 31' in text('Apps/Source/Reforged.Server/RuntimeMajorMudNativeHelp.cs'),'pass31-help-preserved')
req('ClassicMessagesLevelKey = "ui.classic.messages.level"' in text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs'),'pass32-messages-preserved')
req('ClassicGivenNameMaximumLength = 10' in text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs'),'pass28-name-preserved')
req('StrengthAlternative is not null' not in text('Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs'),'strengthalternative-regression-absent')
req(text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs').count('bool enabled;')<=1,'enabled-shadow-regression-absent')

# Reforged configuration boundary: no native WCCMMUD.INI file is introduced.
ini_files=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini_files,'no-wccmmud-ini-file',str(ini_files))

report={'schema':'majormud.pass35-classic-worldgroup-rng-callsite-boundary-validation.v1','update':'0306','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_worldgroup_rng_callsite_boundary_pass35_static_validation_0306.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
