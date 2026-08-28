#!/usr/bin/env python3
import hashlib,json,re
from pathlib import Path
from collections import Counter
root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append({'check':name,'detail':detail})
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))
def sha(rel): return hashlib.sha256((root/rel).read_bytes()).hexdigest()

evidence=js('Librarium/Parity/majormud_classic_gameplay_failure_boundary_evidence_0312.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0312.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0311.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0312.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
recovery=text('Apps/Source/Reforged.Server/RuntimeErrorRecovery.cs')
recovery_smoke=text('Apps/Source/Reforged.Server/RuntimeErrorRecoverySmokeReport.cs')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicGameplayFailureEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass41ClassicGameplayFailureBoundarySmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0312_CLASSIC_GAMEPLAY_FAILURE_BOUNDARY.md')
evdoc=text('Librarium/Evidence/HostRuntime/CLASSIC_GAMEPLAY_FAILURE_BOUNDARY_PASS_0312.md')
pass33=js('Librarium/Parity/majormud_classic_file_configuration_boundary_evidence_0304.json')
compat=text('Apps/Source/Reforged.Server/RuntimeImportExportCompatibility.cs')

# Evidence identity / classification
req(evidence['schema']=='majormud.classic-gameplay-failure-boundary-evidence.v1','evidence-schema')
req(evidence['pass']==41 and evidence['update']=='0312','evidence-pass-update')
req(evidence['status']=='EVIDENCE_BACKED_BOUNDARY','evidence-status')
req(evidence['parityPromotion']['assertion']=='native.error-handling-recovery','promotion-id')
req(evidence['parityPromotion']['from']=='UNKNOWN' and evidence['parityPromotion']['to']=='STRONG','promotion-state')
req(len(evidence['facts'])==6,'evidence-six-facts')
for fid,state in [('ordinary-gameplay-rejection','PASS-narrow'),('catastrophic-native-file-separation','PASS'),('exact-command-failure-presentation','UNKNOWN'),('malformed-post-startup-reference','UNKNOWN'),('catastrophic-save-rollback-order','UNKNOWN'),('reforged-rollback-extension-boundary','EXCLUDED_FROM_CLASSIC_EVIDENCE')]:
    row=next((x for x in evidence['facts'] if x['id']==fid),None)
    req(row is not None,'fact-'+fid)
    if row: req(row['state']==state,'fact-state-'+fid,row['state'])
for claim in ['generic native error text for all failed commands','graceful recovery from wrong/corrupt native files','universal crash behavior for every missing post-startup record','catastrophic save rollback ordering','Worldgroup host/session crash recovery as MajorMUD gameplay behavior']:
    req(claim in evidence['notPromoted'],'not-promoted-'+re.sub(r'\W+','-',claim)[:36])

# Runtime recovery route must be narrow NoOp regardless of rollback availability / character scope.
for tok in ['GameplayActionRejected','RuntimeRecoveryAction.NoOp','classic-gameplay-action-no-op','leaves state','must never be generalized to wrong/corrupt native data files','failure text versus silence remains command/action-specific']:
    req(tok in recovery,'recovery-'+re.sub(r'\W+','-',tok)[:38])
# Ensure the actual case does not accidentally request rollback/disable/stop.
case=re.search(r'RuntimeFailureKind\.GameplayActionRejected\s*\n\s*=>\s*Decision\(context,\s*"classic-gameplay-action-no-op",\s*RuntimeRecoveryAction\.NoOp\)',recovery)
req(case is not None,'recovery-exact-noop-case')
for tok in ['service.Apply(new(RuntimeFailureKind.GameplayActionRejected','CharacterScoped:true','RollbackAvailable:true','classic-gameplay-failure-no-op','classic-gameplay-failure-no-disable-state']:
    req(tok in recovery_smoke,'recovery-smoke-'+re.sub(r'\W+','-',tok)[:36])

# Pass 41 registry explicitly separates branches.
for tok in ['RejectedGameplayDecisionCode = "classic-gameplay-action-no-op"','gameplay.rejection-state-preserving','gameplay.rejection-no-recovery-side-effects','native-file.incompatible-hard-load-boundary','gameplay.command-failure-wording','gameplay.malformed-reference-behavior','persistence.catastrophic-save-rollback-order','reforged.transactional-rollback','worldgroup.crash-recovery','expected-eight-gameplay-failure-evidence-rows','expected-three-proven-failure-rows','expected-three-open-failure-rows','expected-two-excluded-failure-rows']:
    req(tok in registry,'registry-'+re.sub(r'\W+','-',tok)[:40])
for tok in ['classic-gameplay-failure-evidence-registry-valid','rejected-action-decision-accepted','rejected-action-noop-code','rejected-action-is-noop','rejected-action-does-not-rollback','rejected-action-does-not-disable-subsystem','rejected-action-does-not-stop-world-load','rejected-action-preserves-recovery-service-state','catastrophic-native-load-boundary-is-not-gameplay-noop','exact-failure-wording-remains-unknown','catastrophic-save-order-remains-unknown']:
    req(tok in smoke,'smoke-'+tok)

# Pass 33 exact native file boundary remains authoritative and separate.
joined=' '.join(pass33.get('runtimeCorrections',[])).lower()
req('exact source/target version' in joined,'pass33-exact-version-retained')
req('does not create/read/write wccmmud.ini' in joined,'pass33-no-ini-retained')
for tok in ['classic-native-version-mismatch-hard-failure','classic-native-file-mismatch-hard-failure','classic-native-file-replacement-is-external']:
    req(tok in compat,'compat-'+tok)

# CLI + current audit ledger
req('--smoke-pass41-classic-gameplay-failure-boundary' in program,'pass41-cli')
req('RuntimePass41ClassicGameplayFailureBoundarySmokeReport.Run()' in program,'pass41-run')
req(program.count('--smoke-pass41-classic-gameplay-failure-boundary')==1,'pass41-cli-single')
req('majormud_parity_assertions_0316.json' in audit,'audit-current-0312')
req('majormud_parity_assertions_0311.json' not in audit,'audit-prior-retired')

# Exactly one assertion promotion.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior['update']=='0311','prior-update-0311')
req(ledger['update']=='0312','ledger-update-0312')
req(len(old)==200 and len(cur)==200,'ledger-total-200')
req(old['native.error-handling-recovery']['state']=='UNKNOWN','prior-broad-error-unknown')
req(cur['native.error-handling-recovery']['state']=='STRONG','current-broad-error-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('native.error-handling-recovery','UNKNOWN','STRONG')],'only-error-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,82,21),'ledger-counts-97-82-21',str(counts))
for aid in ['character.death-xp-loss-exact','native.law-bounty-guards','commands.native-player-command-table','native.save-coordination','native.capacity-resource-limits']:
    req(cur[aid]['state']=='UNKNOWN','neighbor-remains-unknown-'+aid)

# Scorecard
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,82,21,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict')
req(abs(score['EvidenceBackedPercent']-89.5)<0.01,'score-evidence')
ic=next(c for c in score['Categories'] if c['Category']=='Import / Export / Limits / Recovery')
req((ic['Pass'],ic['Strong'],ic['Unknown'],ic['Total'])==(2,1,1,4),'recovery-category-counts',str(ic))
req(abs(ic['StrictPercent']-50.0)<0.01,'recovery-category-strict')
req(abs(ic['EvidenceBackedPercent']-75.0)<0.01,'recovery-category-evidence')

# Remaining systems
req(remain.get('updatedByPass') in {'Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'remaining-update-0312')
ids={s['id']:s for s in remain['systems']}; er=ids['error-handling-recovery']
req(er.get('status')=='implemented-strong-classic-failure-boundary','remaining-error-status')
req(er.get('evidenceClassification')=='strongly-correlated','remaining-error-evidence')
b=er.get('classicFailureBoundary0312',{})
req(b.get('state')=='STRONG','remaining-boundary-strong')
req(b.get('ordinaryRejectedGameplayAction')=='no-op/state unchanged','remaining-noop')
req(b.get('ordinaryRejectionRollback') is False,'remaining-no-rollback')
req(b.get('ordinaryRejectionDisablesSubsystem') is False,'remaining-no-disable')
req(b.get('ordinaryRejectionStopsWorldLoad') is False,'remaining-no-stop')
req('hard startup/load failure' in b.get('wrongCorruptNativeFile',''),'remaining-hard-load')
req(len(b.get('remainingUnknown',[]))==3,'remaining-three-open')

# Documentation and no guessing.
for token in ['UNKNOWN → STRONG','state-preserving','wrong/corrupt native MajorMUD files','catastrophic startup/load failures','exact command/action-specific failure text','97 PASS / 82 STRONG / 21 UNKNOWN = 200','89.5%']:
    req(token.lower() in doc.lower() or token.lower() in evdoc.lower(),'doc-'+re.sub(r'\W+','-',token)[:34])
req('generic synthetic Classic error message was added' in doc,'doc-no-generic-error-text')
req('No `WCCMMUD.INI` file was added' in doc,'doc-no-ini-statement')

# Protected files from Pass 40 must remain exact.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'f39861f8f2cf00d8641f694b7f2a022353a00b48782aba0b3bd6575cb5aaede8',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected in protected.items():
    req((root/rel).exists(),'protected-exists-'+Path(rel).name)
    req(sha(rel)==expected,'protected-hash-'+Path(rel).name)
ini=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini,'no-wccmmud-ini',str(ini))

# Preserve Pass 40 Shadow behavior and room-list gate.
appearance=text('Apps/Source/Reforged.Server/RuntimeClassicCharacterAppearance.cs'); live=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
req('ShadowLookPayload = "A shadowy figure!"' in appearance,'pass40-shadow-look-preserved')
req('TryResolveEquippedShadowLookOverride' in live,'pass40-shadow-consumer-preserved')
room_block=live[live.index('var nativePlayers = roomSessions.Select'):live.index('var occupants = RuntimeMajorMudCompatibility.FormatRoomOccupants')]
req('other.Actor.DisplayName' in room_block and 'TryResolveEquippedShadowLookOverride' not in room_block,'pass40-room-list-gate-preserved')

report={'schema':'majormud.pass41-classic-gameplay-failure-boundary-validation.v1','update':'0312','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_gameplay_failure_boundary_pass41_static_validation_0312.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
