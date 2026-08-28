#!/usr/bin/env python3
import json, pathlib, sys, zipfile
root=pathlib.Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name):
    checks.append(name)
    if not cond: failures.append(name)
def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))

authority=text('Apps/Source/Reforged.Server/RuntimeClassicDeathLawPvpAuthority.cs')
foundation=text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs')
network=text('Apps/Source/Reforged.Server/ServerNetworkHost.cs')
alignment=text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')
compiler=text('Apps/Compiler/src/LibrariumDataWriter.cs')
compiler_test=text('Apps/Compiler.Tests/Program.cs')
bundle=text('Apps/Source/Reforged.Server/RuntimeCharacterBundleCatalog.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass28IdentityLawSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
source=js('Librarium/Objects/character_foundation/native_runtime_promoted_0137.json')
evidence=js('Librarium/Parity/majormud_identity_law_native_closure_evidence_0299.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0299.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0299.json')
messages_obj=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
def all_strings(value):
    if isinstance(value,dict):
        for child in value.values(): yield from all_strings(child)
    elif isinstance(value,list):
        for child in value: yield from all_strings(child)
    elif isinstance(value,str):
        yield value
message_corpus='\n'.join(all_strings(messages_obj))

# Exact identity closure.
req('ClassicGivenNameMaximumLength = 10' in foundation,'given-name-native-max-10')
req('ClassicFamilyNameMaximumLength = 18' in foundation,'family-name-native-max-18')
req('ValidateClassicGivenName(given.Trim())' in network,'creator-given-native-validation')
req('ValidateClassicFamilyName(family.Trim())' in network,'creator-family-native-validation')
req('1 => RuntimeIdentity.ClassicGivenNameMaximumLength' in network,'creator-input-given-max-10')
req('_ => RuntimeIdentity.ClassicFamilyNameMaximumLength' in network,'creator-input-family-max-18')
req('RuntimeIdentity.ValidateClassicGivenName(givenName)' in foundation,'repository-given-native-validation')
req('RuntimeIdentity.ValidateClassicFamilyName(familyName)' in foundation,'repository-family-native-validation')
req('EDTCHAN2' in message_corpus and 'Given Name  ??????????' in message_corpus and 'Family Name ??????????????????' in message_corpus,'native-generator-10-18-retained')

# Exact lawful multiplier is promoted through generated data, not a hard-coded UNKNOWN contradiction.
rules=source['character_creation_rules']['alignment_evil_point_rules']
lawful=rules.get('lawful_target_attack_penalty',{})
req(lawful.get('status')=='PROVEN' and lawful.get('multiplier')==3,'librarium-lawful-x3-proven')
req('lawfulPenalty["multiplier"]?.DeepClone()' in compiler,'compiler-projects-lawful-multiplier')
req('lawfulPenalty.GetProperty("multiplier").GetInt32() != 3' in compiler_test,'compiler-test-requires-lawful-x3')
req('var lawfulMultiplier = RuntimeClassicDeathLawPvpAuthority.LawfulAttackPenaltyMultiplier' in foundation,'runtime-lawful-default')
req('ReadInt(lawfulPenalty, "multiplier")' in foundation,'runtime-lawful-decode')
req('contract.LawfulAttackPenaltyMultiplier' in alignment,'runtime-lawful-consumption')
req('.lawful_target_attack_penalty.' not in bundle,'lawful-multiplier-not-blanket-metadata')

# Correct the unsupported bounty-hunter escalation; preserve only native-help consequences.
req('BountyHuntersAttack' not in authority,'no-classic-bounty-hunter-invention')
req('"OUTLAW" => RuntimeClassicLawConsequence.GuardsAttackUntilDowned' in authority,'outlaw-nonlethal-guard-contract')
req('"CRIMINAL" => RuntimeClassicLawConsequence.GuardsAttackUntilDeath' in authority,'criminal-lethal-guard-contract')
req('"VILLAIN" or "VILLIAN" => RuntimeClassicLawConsequence.GuardsAttackUntilDeath | RuntimeClassicLawConsequence.DivineInterventionEligible' in authority,'villain-criminal-plus-gods-contract')
req('"FIEND" => RuntimeClassicLawConsequence.None' in authority,'fiend-extra-enforcement-fails-closed')
req('evilPoints >= FiendAttackLockoutEvilPoints' in authority,'fiend-300-lockout-preserved')
for phrase in ['Guards will attack you on sight, but will spare your life','Same as Outlaw, except that Guards will SLAY you on sight','Same as Criminal, except that the \"Gods\" may sometimes intervene','Attacking a lawful player will give thrice the evil points']:
    req(phrase in message_corpus,f'native-law-help:{phrase[:30]}')

# Executable regression surface.
for token in ['classic-given-name-10-accepted','classic-given-name-11-rejected','classic-family-name-18-accepted','classic-family-name-19-rejected','villain-criminal-plus-divine-eligibility','fiend-extra-enforcement-remains-unresolved']:
    req(token in smoke,f'pass28-smoke:{token}')
req('--smoke-pass28-identity-law' in program,'pass28-smoke-cli')

# Conservative ledger/score: one narrow PASS added; aggregate identity/law unknowns remain.
req(ledger.get('update')=='0299','ledger-update-0299')
byid={a['id']:a for a in ledger['assertions']}
req(len(ledger['assertions'])==178,'ledger-total-178')
req(byid.get('character.native-name-record-widths',{}).get('state')=='PASS','name-width-pass-added')
req(byid.get('native.character-identity-policy',{}).get('state')=='UNKNOWN','identity-aggregate-still-unknown')
req('minimum length and allowed characters' in byid['native.character-identity-policy']['description'],'identity-residuals-explicit')
req(byid.get('native.law-bounty-guards',{}).get('state')=='UNKNOWN','law-aggregate-still-unknown')
req('No standalone bounty-hunter escalation is promoted' in byid['native.law-bounty-guards']['description'],'law-bounty-invention-corrected')
req(byid.get('law.reputation-enforcement-contract',{}).get('state')=='STRONG','law-contract-remains-strong')
req(score.get('PassCount')==74 and score.get('StrongCount')==76 and score.get('UnknownCount')==28,'score-counts-74-76-28')
req(score.get('ScoredRequirementCount')==178,'score-total-178')
req(abs(score.get('StrictPercent',0)-41.6)<0.01,'score-strict-41.6')
req(abs(score.get('EvidenceBackedPercent',0)-84.3)<0.01,'score-evidence-84.3')
req('majormud_parity_assertions_0316.json' in audit,'runtime-audit-points-latest-ledger')

# Pass 27 trap unknowns are not guessed closed.
req(any(f.get('id')=='trap-pass27-boundary-rechecked' and f.get('state')=='UNKNOWN' for f in evidence.get('facts',[])),'trap-unknown-boundary-recorded')
req(byid.get('native.traps',{}).get('state')=='UNKNOWN','native-traps-still-unknown')

# Recent compile-fix regressions remain absent.
bundle_d=text('Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs')
world=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
req('StrengthAlternative is not null' not in bundle_d,'strength-alternative-nonnullable-regression-absent')
req(world.count('bool enabled;') <= 1,'runtime-live-world-enabled-declaration-not-duplicated')
app=text('Apps/Source/Reforged.App/App.xaml.cs')
main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-not-manual')

report={'schema':'majormud.pass28-identity-law-native-closure-validation.v1','update':'0299','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/identity_law_native_closure_pass28_static_validation_0299.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
sys.exit(1 if failures else 0)
