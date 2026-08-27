#!/usr/bin/env python3
import json, re
from collections import Counter
from pathlib import Path

root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append({'check':name,'detail':detail})
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))

evidence=js('Librarium/Parity/majormud_native_rng_evidence_0305.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0305.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0305.json')
semantic=js('Librarium/Evidence/worldgroup_semantic_exhaustion_0037.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
catalog=js('Librarium/RemainingSystems/global_random_catalog.json')
prng=text('Apps/Source/Reforged.Server/WorldgroupPrngRuntimeRegistry.cs')
runtime=text('Apps/Source/Reforged.Server/RuntimeClassicWorldgroupPrng.cs')
exports=text('Apps/Source/Reforged.Server/WorldgroupExportImplementationRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass34ClassicWorldgroupPrngSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
global_ext=text('Apps/Source/Reforged.Server/RuntimeGlobalRandom.cs')

# Retained native Worldgroup closure.
req(semantic.get('status')=='semantically_exhausted_with_explicit_external_artifact_boundary','worldgroup-semantic-exhausted')
req(semantic.get('exit_criteria',{}).get('rng_closed') is True,'worldgroup-rng-closed')
req('PRNG implementation and seed lifecycle' in semantic.get('closed_domains',[]),'worldgroup-prng-domain-closed')
for token in [
 'CW3220MT.DLL','4a2f6889f9dc5cfdf4ac44156dde8db0d20c6a0f63db06c576ac0bbba2011a15',
 '0x00016100u','0x0001611Cu','0x0004AE08u','0x0001D5A4u','0x000360FAu',
 'seed = seed * 0x015A4E35 + 1 modulo 2^32','thread-local runtime state',
 '(hour << 11) + (minute << 5) + (second >> 1)']:
    req(token in prng,'native-registry:'+token[:28])

# Exact executable raw generator constants/formula.
for token in ['BorlandMultiplier = 0x015A4E35u','BorlandIncrement = 1u','RandMax = 0x7FFF',
              'unchecked(seed * BorlandMultiplier + BorlandIncrement)','(advancedSeed >> 16) & RandMax']:
    req(token in prng,'prng-code:'+token[:30])
req('RuntimeClassicWorldgroupPrng' in runtime,'runtime-class-present')
req('WorldgroupPrngRuntimeRegistry.AdvanceSeed(previous)' in runtime,'runtime-uses-authority-transition')
req('WorldgroupPrngRuntimeRegistry.ProjectRand15(next)' in runtime,'runtime-uses-authority-output')
req('_seed = seed;' in runtime and '_sequence = 0;' in runtime,'srand-exact-reset')
req('worldSeed == 0' not in runtime,'classic-zero-seed-not-normalized')
req('SplitMix64' not in runtime and 'xorshift' not in runtime.lower(),'classic-no-extension-prng')

# Mathematical reference vectors, independent of C# source.
def nxt(seed): return (seed*0x015A4E35 + 1) & 0xffffffff
def out(seed): return (seed >> 16) & 0x7fff
vectors=[
 (1234,2236414843,1356),(2236414843,4056779384,29133),(4056779384,3589159641,21998),
 (3589159641,1770671342,27018),(1770671342,4139675975,30398),(4139675975,904336820,13799),
 (904336820,3871102533,26300),(3871102533,3230196298,16520),(3230196298,1766679891,26957),
 (1766679891,1410548784,21523)]
for i,(seed,new,value) in enumerate(vectors,1):
    req(nxt(seed)==new and out(new)==value,f'borland-reference-vector-{i:02d}')
req(nxt(0)==1 and out(1)==0,'zero-seed-transition')
req(0 <= out(nxt(0xffffffff)) <= 32767,'uint32-overflow-domain')
msvc=((1234*214013+2531011)&0xffffffff); msvcval=(msvc>>16)&0x7fff
req((msvc,msvcval)==(266623053,4068),'msvc-reference-vector')
req((msvc,msvcval)!=(2236414843,1356),'msvc-candidate-rejected')

# Startup packed-time lifecycle.
def pack(h,m,s): return (h<<11)+(m<<5)+(s>>1)
for h,m,s,expected,name in [(0,0,0,0,'midnight'),(0,0,1,0,'second-pair'),(0,0,2,1,'two-second-step'),(12,34,56,25692,'midday'),(23,59,58,49021,'last-pair'),(23,59,59,49021,'last-second')]:
    req(pack(h,m,s)==expected,'startup-pack-'+name)
for token in ['PackStartupTimeSeed','hour is < 0 or > 23','minute is < 0 or > 59','second is < 0 or > 59']:
    req(token in prng,'startup-code:'+token)
req(evidence['startupSeed']['startupSrandCallsInStaticReachablePath']==1,'startup-srand-once')
req(evidence['startupSeed']['resolutionSeconds']==2,'startup-two-second-evidence')

# Host range helper boundary retained, without inventing full range loop activation.
for sym,rva in [('_genrdn','0x0000738Cu'),('_lngrnd','0x000073DEu')]:
    req(sym in exports and rva in exports,'range-export-'+sym)
req('exclusiveMaximum is 0 returns 0' in exports,'genrdn-zero-edge')
req('exclusiveMaximum <= minimum returns minimum' in exports,'genrdn-min-edge')
req('shared host PRNG' in exports,'genrdn-shared-host-dependency')
req('Instruction-equivalent range routine to _genrdn' in exports,'lngrnd-equivalent-contract')
req('NextInclusive' not in runtime,'raw-runtime-does-not-invent-range-mapping')

# Screenshot hypothesis and retained evidence conclusion are explicit.
req(evidence.get('update')=='0305' and evidence.get('pass')==34,'evidence-pass34')
req(evidence['nativeRuntime']['multiplierDecimal']==22695477,'evidence-multiplier')
req(evidence['nativeRuntime']['stateWidthBits']==32,'evidence-state-width')
req(evidence['nativeRuntime']['randMax']==32767,'evidence-randmax')
req(evidence['nativeRuntime']['stateOwnership']=='CW3220MT thread-local CRT state','evidence-thread-local')
req(evidence['validationVector']['nativeNextSeed']==2236414843 and evidence['validationVector']['nativeReturnedValue']==1356,'evidence-native-vector')
req(evidence['validationVector']['rejectedMsvcNextSeed']==266623053 and evidence['validationVector']['rejectedMsvcReturnedValue']==4068,'evidence-msvc-rejection-vector')

# Smoke path exercises exact vectors and candidate rejection.
for token in ['borland-vector-{i + 1:00}','expected.Length','srand-zero-preserved','uint32-modulo-overflow','startup-seed-two-second-resolution','msvc-lcg-candidate-rejected','host-range-exclusive-shared-contract']:
    req(token in smoke,'pass34-smoke:'+token)
req('--smoke-pass34-classic-worldgroup-prng' in program,'pass34-smoke-cli')
req('RuntimePass34ClassicWorldgroupPrngSmokeReport.Run()' in program,'pass34-smoke-run')

# Reforged extension RNG is not misidentified as Classic parity owner.
req('SplitMix64' in global_ext and 'RuntimeRandomScope' in global_ext,'reforged-extension-rng-preserved')
req('Reforged extension' in catalog.get('runtimeOwner',''),'catalog-extension-boundary')
req('do not substitute the Reforged xorshift multi-stream service for Classic native RNG' in catalog.get('safeFallback',[]),'catalog-no-substitution')
req(any('gameplay draw/mutation ordering' in x for x in catalog.get('nativeDependencies',[])),'catalog-pull-order-unknown')

systems={s['id']:s for s in registry['systems']}
req(registry.get('updatedByPass') in {'Pass 34 / Update 0305','Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313'},'registry-pass34')
gr=systems['global-rng']
req(gr.get('status') in {'classic-native-core-proven-gameplay-call-order-bounded','classic-native-core-and-direct-call-boundary-proven-gameplay-call-order-bounded'},'registry-rng-status')
req(gr.get('classicPrng0305',{}).get('multiplier')=='0x015A4E35','registry-rng-multiplier')
req(gr.get('classicPrng0305',{}).get('rejectedCandidate')=='MSVC 214013/2531011','registry-rejected-candidate')
req(any('callsite-to-Worldgroup-execution-thread' in x for x in gr.get('nativeDependencies',[])),'registry-thread-mapping-unknown')

# Ledger: three narrow PASS assertions; broad gameplay integration remains UNKNOWN.
byid={a['id']:a for a in ledger['assertions']}; counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0305','ledger-update-0305')
req(len(ledger['assertions'])==199,'ledger-total-199')
req(counts==Counter({'PASS':96,'STRONG':76,'UNKNOWN':27}),'ledger-counts',str(counts))
for aid in ['native.global-rng.borland-crt-core','native.global-rng.startup-packed-time-seed','native.global-rng.host-range-endpoint-contract']:
    req(byid.get(aid,{}).get('state')=='PASS','ledger-pass:'+aid)
req(byid.get('native.global-rng.native-contract',{}).get('state')=='UNKNOWN','ledger-broad-rng-stays-unknown')
unknown_desc=byid['native.global-rng.native-contract']['description']
req('gameplay PRNG draw ordering' in unknown_desc and 'thread' in unknown_desc and '_genrdn/_lngrnd' in unknown_desc,'ledger-unknown-narrowed')
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(96,76,27,199),'score-counts')
req(abs(score['StrictPercent']-48.2)<0.01,'score-strict-48.2')
req(abs(score['EvidenceBackedPercent']-86.4)<0.01,'score-evidence-86.4')
cat={c['Category']:c for c in score['Categories']}
req((cat['Host / Persistence / Worldgroup']['Pass'],cat['Host / Persistence / Worldgroup']['Strong'],cat['Host / Persistence / Worldgroup']['Unknown'],cat['Host / Persistence / Worldgroup']['Total'])==(3,1,4,8),'score-host-category')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-current-ledger')

# Prior pass behavior and compile-regression safeguards remain intact.
req('MaximumPvpLevelDifference = 100' in text('Apps/Source/Reforged.Server/RuntimeClassicConfigurationAuthority.cs'),'pass33-pvp-default-preserved')
req('HighestUncontestedGroup = 126' in text('Apps/Source/Reforged.Server/RuntimeClassicBroadcastGroupAuthority.cs'),'pass30-group126-preserved')
req('"laws" => 31' in text('Apps/Source/Reforged.Server/RuntimeMajorMudNativeHelp.cs'),'pass31-help-preserved')
req('ClassicMessagesLevelKey = "ui.classic.messages.level"' in text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs'),'pass32-messages-preserved')
req('ClassicGivenNameMaximumLength = 10' in text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs'),'pass28-name-preserved')
bundle=text('Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs')
world=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
app=text('Apps/Source/Reforged.App/App.xaml.cs'); main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req('StrengthAlternative is not null' not in bundle,'strengthalternative-regression-absent')
req(world.count('bool enabled;')<=1,'enabled-shadow-regression-absent',str(world.count('bool enabled;')))
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

report={'schema':'majormud.pass34-classic-worldgroup-prng-validation.v1','update':'0305','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_worldgroup_prng_pass34_static_validation_0305.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
