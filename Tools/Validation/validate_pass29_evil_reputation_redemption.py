#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name):
    checks.append(name)
    if not cond: failures.append(name)
def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))
def all_strings(value):
    if isinstance(value,dict):
        for child in value.values(): yield from all_strings(child)
    elif isinstance(value,list):
        for child in value: yield from all_strings(child)
    elif isinstance(value,str):
        yield value

source=js('Librarium/Objects/character_foundation/native_runtime_promoted_0137.json')
evidence=js('Librarium/Parity/majormud_evil_reputation_redemption_evidence_0300.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0300.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0300.json')
native_layouts=js('Librarium/Evidence/native_support_binary_layouts_0021.json')
native_messages=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
message_corpus='\n'.join(all_strings(native_messages))
layout_corpus='\n'.join(all_strings(native_layouts))
foundation=text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs')
alignment=text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')
sysop=text('Apps/Source/Reforged.Server/RuntimeWccSysopCommands.cs')
decode_audit=text('Apps/Source/Reforged.Server/RuntimeCharacterDecodeBundleAudit.cs')
compiler_test=text('Apps/Compiler.Tests/Program.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass29EvilReputationSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
law=text('Apps/Source/Reforged.Server/RuntimeClassicDeathLawPvpAuthority.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

rules=source['character_creation_rules']['alignment_evil_point_rules']
mins=rules.get('reputation_minimum_evil_points',{})
expected={'Saint':-220,'Good':-200,'Neutral':-50,'Seedy':30,'Outlaw':40,'Criminal':80,'Villain':120,'Fiend':210}
req(mins==expected,'librarium-exact-reputation-reference-points')
clean=rules['daily_cleanup']['no_evil_action_that_day']
req(clean.get('maximum_daily_reduction')==18,'librarium-maxepday-18-executable')
req(clean.get('default_epcycle_minutes')==60,'librarium-epcycle-60')
req(clean.get('default_epamount')==1,'librarium-epamount-1')
req(rules['daily_cleanup']['evil_action_that_day'].get('evil_points_reduced_by')==2,'librarium-evil-day-reduction-2')
req(clean.get('base_reduction')==3,'librarium-clean-day-base-3')

# Retained WCC setup/message evidence independently contains the defaults.
for token,name in [('MAXEPDAY','native-maxepday-symbol'),('EPCYCLE','native-epcycle-symbol'),('EPAMOUNT','native-epamount-symbol')]:
    req(token in layout_corpus or token in message_corpus,name)
for phrase,name in [('Max EPs to forgive per day? 18','native-maxepday-prompt-18'),('Minutes per Evil Point Cycle? 60','native-epcycle-prompt-60'),('EPs to forgive per cycle? 1','native-epamount-prompt-1')]:
    req(phrase in layout_corpus or phrase in message_corpus,name)

# Generated contract and decoder must carry rather than hard-code away the promoted values.
req('IReadOnlyDictionary<string, int> ReputationMinimumEvilPoints' in foundation,'contract-carries-reputation-thresholds')
req('ReadIntDictionary(rules, "reputation_minimum_evil_points")' in foundation,'decoder-reads-reputation-thresholds')
req('if (maximumDailyReduction <= 0) maximumDailyReduction = 18;' in foundation,'decoder-safe-native-maxepday-fallback')
req('RuntimeCharacterAlignmentRuleContract AlignmentRuleContract' in foundation and ', 18, string.Empty, 300)' in foundation,'default-contract-maxepday-18')

# Runtime EP changes must keep the visible reputation band synchronized.
for token,name in [
    ('["Saint"] = -220','resolver-saint-ref'),('["Good"] = -200','resolver-good-ref'),('["Neutral"] = -50','resolver-neutral-ref'),
    ('["Seedy"] = 30','resolver-seedy-ref'),('["Outlaw"] = 40','resolver-outlaw-ref'),('["Criminal"] = 80','resolver-criminal-ref'),
    ('["Villain"] = 120','resolver-villain-ref'),('["Fiend"] = 210','resolver-fiend-ref')]: req(token in alignment,name)
req(('ClassicDefault { get; } = new(2, 3, 60, 1, 18);' in alignment) or ('RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay' in alignment and 'RuntimeClassicConfigurationAuthority.EvilPointCycleMinutes' in alignment and 'RuntimeClassicConfigurationAuthority.EvilPointsForgivenPerCycle' in alignment),'classic-cleanup-default-maxepday-18')
req('ResolveReputationForEvilPoints' in alignment,'runtime-reputation-resolver-present')
req(alignment.count('Reputation = ResolveReputationForEvilPoints') >= 3,'attack-delta-cleanup-recalculate-reputation')
req('ApplyEvilPointDelta(' in alignment,'ordinary-ep-delta-authority')
req('RuntimeCharacterAlignmentResolver.ApplyEvilPointDelta(alignment, amount, markPositiveDeltaAsEvilAction: false)' in sysop,'sysop-add-evil-recalculates-reputation-without-player-evil-marker')
req('requestedReduction = Math.Min(requestedReduction, configuration.MaximumDailyReduction);' in alignment,'cleanup-enforces-maximum-daily-reduction')
req('state.EvilPoints < 0' in alignment and 'requestedReduction - configuration.EvilActionReduction' in alignment,'negative-ep-base-exception-preserved')
req('state.EvilPoints < LegacyFiendAttackLockoutThreshold' in alignment,'separate-300-lockout-preserved')
req('currentReputation, "Lawful"' not in alignment or 'string.Equals(currentReputation, "Lawful"' in alignment,'lawful-pledge-preservation-explicit')

# Compiler projection and runtime decode audit cover the new fields.
for token,name in [
    ('reputationMinimums.GetProperty("Saint").GetInt32() != -220','compiler-test-saint-ref'),
    ('reputationMinimums.GetProperty("Fiend").GetInt32() != 210','compiler-test-fiend-ref'),
    ('cleanDay.GetProperty("maximum_daily_reduction").GetInt32() != 18','compiler-test-maxepday-18')]: req(token in compiler_test,name)
for token,name in [
    ('AlignmentRuleContract.MaximumDailyReduction == 18','decode-audit-maxepday-18'),
    ('ReputationMinimumEvilPoints.Count == 8','decode-audit-eight-reference-points'),
    ('ResolveReputationForEvilPoints(210, "Villain", catalog.AlignmentRuleContract) == "Fiend"','decode-audit-fiend-transition')]: req(token in decode_audit,name)

# Executable smoke surface, including the user's OmniMUD corroboration point.
for token in ['ep-minus201-saint','ep-minus200-good','ep-29-neutral','ep-30-seedy','ep-40-outlaw','ep-80-criminal','ep-120-villain','ep-209-villain','ep-210-fiend','ep-237-fiend-omnimud-corroboration','maxepday-18-cap-enforced','cleanup-crosses-outlaw-to-seedy','negative-ep-redemption-crosses-good-to-saint']:
    req(token in smoke,f'pass29-smoke:{token}')
req('--smoke-pass29-evil-reputation' in program,'pass29-smoke-cli')

# Source conflict remains evidence-gated; do not regress Pass 28's no-invented-bounty rule.
req('BountyHuntersAttack' not in law,'no-active-bounty-hunter-enforcement-invention')
req('source conflict' in law.lower() or 'source-conflict' in law.lower(),'villain-fiend-source-conflict-commented')
facts={f['id']:f for f in evidence.get('facts',[])}
req(facts.get('villain-fiend-enforcement-source-conflict',{}).get('state')=='UNKNOWN','enforcement-source-conflict-stays-unknown')
req(facts.get('omnimud-classic-redemption-corroboration',{}).get('state')=='CORROBORATING_ONLY','omnimud-not-promoted-as-native-proof')

# Ledger/score closure is narrow: two exact PASS assertions, same UNKNOWN count.
byid={a['id']:a for a in ledger['assertions']}
from collections import Counter
counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0300','ledger-update-0300')
req(len(ledger['assertions'])==180,'ledger-total-180')
req(counts==Counter({'PASS':76,'STRONG':76,'UNKNOWN':28}),'ledger-counts-76-76-28')
req(byid.get('character.evil-point-reputation-bands',{}).get('state')=='PASS','reputation-bands-pass')
req(byid.get('character.evil-point-redemption-defaults',{}).get('state')=='PASS','redemption-defaults-pass')
req(byid.get('native.law-bounty-guards',{}).get('state')=='UNKNOWN','law-aggregate-remains-unknown')
req(byid.get('native.traps',{}).get('state')=='UNKNOWN','trap-aggregate-remains-unknown')
req(score.get('PassCount')==76 and score.get('StrongCount')==76 and score.get('UnknownCount')==28,'score-counts-76-76-28')
req(score.get('ScoredRequirementCount')==180,'score-total-180')
req(abs(score.get('StrictPercent',0)-42.2)<0.01,'score-strict-42.2')
req(abs(score.get('EvidenceBackedPercent',0)-84.4)<0.01,'score-evidence-84.4')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-current-ledger-0300')

# Recent compile/regression fixes must remain fixed.
bundle_d=text('Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs')
world=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
app=text('Apps/Source/Reforged.App/App.xaml.cs')
main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req('StrengthAlternative is not null' not in bundle_d,'strengthalternative-nonnullable-regression-absent')
req(world.count('bool enabled;') <= 1,'runtime-live-world-enabled-shadow-regression-absent')
import re
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;', app)) == 1,'wpf-app-services-field-single')
req(len(re.findall(r'private\s+int\s+_shutdownStarted\s*;', app)) == 1,'wpf-app-shutdown-field-single')
req(len(re.findall(r'private\s+readonly\s+CancellationTokenSource\s+_applicationLifetime\s*=', app)) == 1,'wpf-app-lifetime-field-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(', app)) == 1,'wpf-app-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(', app)) == 1,'wpf-app-onexit-single')
req(len(re.findall(r'public\s+async\s+Task\s+ShutdownApplicationAsync\s*\(', app)) == 1,'wpf-app-shutdown-method-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-not-manual')

report={'schema':'majormud.pass29-evil-reputation-redemption-validation.v1','update':'0300','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/evil_reputation_redemption_pass29_static_validation_0300.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
sys.exit(1 if failures else 0)
