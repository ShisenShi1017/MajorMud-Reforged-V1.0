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

native=js('Librarium/Evidence/native_support_catalog_0020.json')
evidence=js('Librarium/Parity/majormud_classic_file_configuration_boundary_evidence_0304.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0304.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0304.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
config=text('Apps/Source/Reforged.Server/RuntimeClassicConfigurationAuthority.cs')
timing=text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs')
alignment=text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')
pvp=text('Apps/Source/Reforged.Server/RuntimeLivePvp.cs')
compat=text('Apps/Source/Reforged.Server/RuntimeImportExportCompatibility.cs')
compat_smoke=text('Apps/Source/Reforged.Server/RuntimeImportExportCompatibilitySmokeReport.cs')
recovery=text('Apps/Source/Reforged.Server/RuntimeErrorRecovery.cs')
recovery_smoke=text('Apps/Source/Reforged.Server/RuntimeErrorRecoverySmokeReport.cs')
worldrules=text('Apps/Source/Reforged.Server/RuntimeWorldRulesConfiguration.cs')
sysop=text('Apps/Source/Reforged.Server/RuntimeWccSysopCommands.cs')
passsmoke=text('Apps/Source/Reforged.Server/RuntimePass33ClassicFileConfigurationBoundarySmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

# Retained native WCCMMUD setup evidence.
req(sha('Librarium/Evidence/native_support_catalog_0020.json')=='3253760117103f0b5d87aaf212724d28f9a9ba9d1f3653a0a20d2292fce3d317','native-support-catalog-sha')
records=[]
def walk(x):
    if isinstance(x,dict):
        if x.get('file')=='WCCMMUD.mcv': records.append(x)
        for v in x.values(): walk(v)
    elif isinstance(x,list):
        for v in x: walk(v)
walk(native)
req(len(records)==1,'native-wccmmud-mcv-record')
rec=records[0] if records else {}
req(rec.get('sha256')=='8e0daa7cd59fdf1aec947ad46a1e7c61b3eb519d2bb084ee05a5324d2038a30b','native-wccmmud-mcv-sha')
prompts=rec.get('configuration_prompts',[])
req(rec.get('configuration_prompt_count')==44 and len(prompts)==44,'native-44-setup-prompts')
pt={p.get('text') for p in prompts}
for exact in [
 'Seconds between saving Player data? 60','Seconds between saving Buffer data? 300',
 'Maximum level difference for PVP? 100','Number of seconds of inactivity allowed? 5000',
 'Max EPs to forgive per day? 18','Minutes per Evil Point Cycle? 60','EPs to forgive per cycle? 1',
 'Enable the choice of Lawful? YES','Keep some experience after final death? YES',
 'Gold to charge to change gender? 5000','Sweep coins of this and lower? GOLD','Sweep coins from gang houses? NO']:
    req(exact in pt,'native-prompt:'+exact[:28])

# User/operator evidence boundary and no legacy INI creation.
req(evidence.get('update')=='0304' and evidence.get('pass')==33,'evidence-update-0304')
joined=' '.join(evidence.get('runtimeCorrections',[])).lower()
for token in ['does not create/read/write wccmmud.ini','default pvp maximum-level-difference is 100','exact source/target version','no-op recovery disposition']:
    req(token in joined,'evidence-runtime:'+token[:24])
req(not any(p.is_file() and p.name.lower()=='wccmmud.ini' for p in root.rglob('*')),'reforged-no-wccmmud-ini-file')

# Reforged internal configuration authority.
for token in [
 'NativeConfigurationFileName = "WCCMMUD.INI"','ReforgedUsesNativeConfigurationFile = false',
 'NativeConfigurationPromptCount = 44','MaximumPvpLevelDifference = 100',
 'MaximumEvilPointsForgivenPerDay = 18','EvilPointCycleMinutes = 60','EvilPointsForgivenPerCycle = 1']:
    req(token in config,'config-authority:'+token[:28])
req('RuntimeClassicConfigurationAuthority.PlayerDataSaveIntervalSeconds' in timing,'timing-projects-native-player-save')
req('RuntimeClassicConfigurationAuthority.BufferDataSaveIntervalSeconds' in timing,'timing-projects-native-buffer-save')
req('RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay' in alignment,'alignment-projects-maxepday')
req('RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference' in pvp,'pvp-projects-native-default')
req('Reforged intentionally does not create or parse that legacy file' in worldrules,'worldrules-no-ini-parser')
req('native MajorMUD reloaded WCCMMUD.INI' in sysop and 'no literal file target in Reforged' in sysop,'sysop-reload-no-literal-file-target')
req('WCCMUD.INI' not in sysop,'sysop-old-ini-typo-absent')

# Exact-file/exact-version Classic compatibility boundary.
for token in [
 'classic-native-file-replacement-is-external','classic-native-version-mismatch-hard-failure',
 'classic-native-file-identity-required','classic-native-file-mismatch-hard-failure',
 'request.SourceVersion != request.TargetVersion','RuntimeCompatibilityOperation.Import or RuntimeCompatibilityOperation.Export or RuntimeCompatibilityOperation.Migrate']:
    req(token in compat,'compat:'+token[:28])
req('Custom/AI compatibility operations remain Reforged extension behavior' in compat,'compat-custom-ai-extension-preserved')
for token in ['classic-version-mismatch-hard-failure','classic-native-file-exact-match','classic-native-file-mismatch-hard-failure','classic-import-is-external-file-replacement','classic-migration-unsupported']:
    req(token in compat_smoke,'compat-smoke:'+token)

# Normal rejected gameplay action no-op boundary.
req('GameplayActionRejected' in recovery and 'RuntimeRecoveryAction.NoOp' in recovery,'recovery-noop-enum-route')
req('classic-gameplay-action-no-op' in recovery,'recovery-noop-code')
req('classic-gameplay-failure-no-op' in recovery_smoke,'recovery-smoke-noop')

# Dedicated pass smoke path.
for token in ['reforged-has-no-wccmmud-ini','native-44-configuration-prompts','pvp-store-default-100','classic-exact-file-version-validation','classic-wrong-file-hard-failure','classic-wrong-version-hard-failure','classic-no-migration-subsystem','classic-rejected-gameplay-action-no-op']:
    req(token in passsmoke,'pass33-smoke:'+token)
req('--smoke-pass33-classic-file-config' in program,'pass33-smoke-cli')
req('RuntimePass33ClassicFileConfigurationBoundarySmokeReport.Run(projectRoot)' in program,'pass33-smoke-run')

# Ledger classification and score.
byid={a['id']:a for a in ledger['assertions']}; counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0304','ledger-update-0304')
req(len(ledger['assertions'])==196,'ledger-total-196')
req(counts==Counter({'PASS':93,'STRONG':76,'UNKNOWN':27}),'ledger-counts',str(counts))
req(byid.get('native.import-export-compatibility',{}).get('state')=='PASS','ledger-import-export-closed')
for aid in ['native.error-handling.failed-gameplay-action-noop','native.world-rules-configuration.source-boundary','native.world-rules-configuration.default-prompt-catalog','native.pvp.default-maximum-level-difference']:
    req(byid.get(aid,{}).get('state')=='PASS','ledger-pass:'+aid)
req(byid.get('native.error-handling-recovery',{}).get('state')=='UNKNOWN','ledger-broad-error-stays-unknown')
req(byid.get('native.world-rules-configuration',{}).get('state')=='UNKNOWN','ledger-broad-worldrules-stays-unknown')
req(byid.get('native.pvp-policy',{}).get('state')=='UNKNOWN','ledger-broad-pvp-stays-unknown')
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(93,76,27,196),'score-counts')
req(abs(score['StrictPercent']-47.4)<0.01,'score-strict-47.4')
req(abs(score['EvidenceBackedPercent']-86.2)<0.01,'score-evidence-86.2')
cat={c['Category']:c for c in score['Categories']}
req((cat['Import / Export / Limits / Recovery']['Pass'],cat['Import / Export / Limits / Recovery']['Unknown'],cat['Import / Export / Limits / Recovery']['Total'])==(2,2,4),'score-import-recovery-category')
req((cat['Timers / Resets / World Rules']['Pass'],cat['Timers / Resets / World Rules']['Unknown'],cat['Timers / Resets / World Rules']['Total'])==(6,4,15),'score-worldrules-category')
req('majormud_parity_assertions_0316.json' in audit,'runtime-audit-current-ledger')

# Registry boundary.
req(registry.get('updatedByPass') in {'Pass 33 / Update 0304','Pass 34 / Update 0305','Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'registry-pass33')
systems={s['id']:s for s in registry['systems']}
req(systems['import-export-compatibility'].get('classicBoundary0304',{}).get('state')=='PASS','registry-import-boundary-pass')
req(systems['world-rules-configuration'].get('classicConfiguration0304',{}).get('reforgedHasNativeIni') is False,'registry-no-ini')
req(systems['world-rules-configuration'].get('classicConfiguration0304',{}).get('retainedPromptCount')==44,'registry-44-prompts')
req(systems['error-handling-recovery'].get('classicFailureBoundary0304',{}).get('ordinaryRejectedGameplayAction')=='no-op/state unchanged','registry-gameplay-noop')

# Prior pass behavior and compile-regression safeguards remain intact.
req('HighestUncontestedGroup = 126' in text('Apps/Source/Reforged.Server/RuntimeClassicBroadcastGroupAuthority.cs'),'pass30-group126-preserved')
req('"laws" => 31' in text('Apps/Source/Reforged.Server/RuntimeMajorMudNativeHelp.cs'),'pass31-laws-preserved')
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

report={'schema':'majormud.pass33-classic-file-configuration-boundary-validation.v1','update':'0304','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_file_configuration_boundary_pass33_static_validation_0304.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
