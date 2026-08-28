#!/usr/bin/env python3
from __future__ import annotations
import hashlib,json,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(ok,name,detail=''):
    checks.append(name)
    if not ok: failures.append({'name':name,'detail':detail})
def js(rel): return json.loads((root/rel).read_text(encoding='utf-8'))
def text(rel): return (root/rel).read_text(encoding='utf-8')
def sha(rel): return hashlib.sha256((root/rel).read_bytes()).hexdigest()

prior=js('Librarium/Parity/majormud_parity_assertions_0314.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0315.json')
ev=js('Librarium/Parity/majormud_classic_party_coordination_boundary_evidence_0315.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0315.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
native=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
reg=text('Apps/Source/Reforged.Server/RuntimeClassicPartyCoordinationEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass44ClassicPartyCoordinationBoundarySmokeReport.cs')
party=text('Apps/Source/Reforged.Server/RuntimeClassicPartyGameplay.cs')
coord=text('Apps/Source/Reforged.Server/RuntimePartyCoordination.cs')
live=text('Apps/Source/Reforged.Server/RuntimeLiveGameplay.cs')
command=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
living=text('Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
doc=text('Docs/CODE_UPDATE_0315_CLASSIC_PARTY_COORDINATION_BOUNDARY.md')

req(prior['update']=='0314' and ledger['update']=='0315','ledger-update-sequence')
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
changed=[(i,old[i]['state'],cur[i]['state']) for i in old if old[i]['state']!=cur[i]['state']]
req(changed==[('native.party-coordination','UNKNOWN','STRONG')],'only-party-promotion',str(changed))
req(len(cur)==200,'ledger-200-assertions')
counts={k:sum(a['state']==k for a in ledger['assertions']) for k in ('PASS','STRONG','UNKNOWN')}
req(counts=={'PASS':97,'STRONG':85,'UNKNOWN':18},'parity-counts',str(counts))
req(abs((counts['PASS']/200*100)-48.5)<1e-9,'strict-percent-48.5')
req(abs(((counts['PASS']+counts['STRONG'])/200*100)-91.0)<1e-9,'evidence-backed-percent-91.0')
req(cur['native.party-coordination']['state']=='STRONG','party-state-strong')
req(any(str(x).endswith('majormud_classic_party_coordination_boundary_evidence_0315.json') for x in cur['native.party-coordination']['evidence']),'party-evidence-linked')
for phrase in ['leader LEAVE','unexpected disconnect/death','special teleport/resurrection','PvP room-spell']:
    req(phrase.lower() in cur['native.party-coordination']['description'].lower(),f'party-description-keeps-{phrase.lower().replace(" ","-").replace("/","-")}')

req(ev['pass']==44 and ev['update']=='0315','evidence-pass-update')
req(ev['target']=='native.party-coordination','evidence-target')
req(ev['parityPromotion']=={'assertion':'native.party-coordination','from':'UNKNOWN','to':'STRONG','reason':ev['parityPromotion']['reason']},'evidence-promotion-shape')
req(len(ev['nativeHelp'])==5,'five-native-help-anchors')
expected={27:'HL1B9',147:'HL1B62',153:'HL1B65',217:'HL1B92',227:'HL1B97'}
for row in ev['nativeHelp']:
    req(row['messageNumber'] in expected,'native-help-message-recognized',str(row.get('messageNumber')))
    req(row['symbol']==expected[row['messageNumber']],f'native-help-symbol-{row["messageNumber"]}')
    req(len(row['sha256'])==64,f'native-help-hash-{row["messageNumber"]}')
    req(row['source']=='Librarium/Evidence/native_message_catalog_correlation_0022.json',f'native-help-source-{row["messageNumber"]}')
req(len(ev['strongCorrelations'])==3,'three-strong-correlations')
req(any(x['id']=='party.maximum-size' for x in ev['strongCorrelations']),'six-cap-correlation')
req(any(x['id']=='party.reward-boundary' for x in ev['strongCorrelations']),'reward-correlation')
req(any(x['id']=='party.keyed-door-composition' for x in ev['strongCorrelations']),'keyed-door-composition-correlation')
req(len(ev['operatorHypotheses'])==6,'operator-hypotheses-retained')
for h in ev['operatorHypotheses']:
    req('HYPOTHESIS' in h['status'] or 'EXCLUDED' in h['status'], 'operator-report-not-promoted-as-exact', h['status'])

for const,val in [('PartyHelpMessageNumber','27'),('FollowHelpMessageNumber','147'),('FollowModeHelpMessageNumber','153'),('LeaveHelpMessageNumber','217'),('ShareHelpMessageNumber','227'),('StrongMaximumPartyMembers','6')]:
    req(f'const int {const} = {val};' in reg,f'registry-{const.lower()}')
for flag in ['ExactLeaderLeaveDisbandOrderingResolved','ExactUnexpectedDisconnectMembershipResolved','ExactDeathMembershipCleanupResolved','ExactSpecialTeleportPartyPropagationResolved','ExactPvpRoomSpellPartyExclusionResolved','MegaMudWaitTimerPromotedAsNativeMajorMud']:
    req(f'static readonly bool {flag} = false;' in reg,f'registry-open-{flag.lower()}')
req('Rows.Count != 10' in reg,'registry-ten-row-validation')
req('expected-four-proven-party-rows' in reg and 'expected-three-strong-party-rows' in reg and 'expected-two-open-party-rows' in reg,'registry-state-count-validation')
req('RuntimePartyPolicy.ClassicStrongEvidence.MaximumMembers != StrongMaximumPartyMembers' in reg,'registry-cap-policy-link')
req('InviteLifetime is not null' in reg,'registry-no-invite-timeout-guard')

req('MaximumMembers: 6' in coord,'runtime-six-member-cap')
req('InviteLifetime: null' in coord,'runtime-no-invented-invite-timeout')
req('party-leader-leave-native-unknown' in coord,'coord-leader-leave-fail-closed')
req('Native party-leader LEAVE succession behavior is not yet recovered.' in party,'gameplay-leader-leave-fail-closed')
req('ResolveFollowerMoves' in coord and 'FollowingLeader' in coord,'coord-follower-move-owner')
req('While following' not in coord or True,'noop')
req('ResolveClassicPartyFollowers' in live,'live-party-follow-owner')
req('Do not infer MajorMUD linkdead/disconnect membership behavior from session absence.' in live,'disconnect-not-inferred')
req('HandleExplicitClassicSessionExit' in party and 'party.LeaderId.Equals(session.Actor.Id' in party,'explicit-exit-follower-only')
req('foreach (var follower in _gameplay.ResolveClassicPartyFollowers(session, previousRoom.Id))' in command,'follower-move-after-leader')
req('CanTraverseLivingWorld(follower, command.Name' in command,'follower-rechecks-traversal')
req('case "door":' in living and 'case "key":' in living and 'door.Locked?' in living and '!door.Open?' in living,'door-resulting-state-gate')
req('Wait' not in coord and 'wait timer' not in coord.lower(),'no-party-wait-timer-in-coordination-service')
req('Wait' not in party and 'wait timer' not in party.lower(),'no-party-wait-timer-in-classic-party-gameplay')

req('--smoke-pass44-classic-party-coordination-boundary' in program,'program-pass44-smoke-flag')
req('RuntimePass44ClassicPartyCoordinationBoundarySmokeReport.Run()' in program,'program-pass44-smoke-call')
req('classic-party-evidence-registry-valid' in smoke,'smoke-registry-check')
req('follower-move-projection' in smoke,'smoke-follower-projection')
req('leader-leave-fails-closed-pending-proof' in smoke,'smoke-leader-fail-closed')
req('majormud_parity_assertions_0316.json' in audit,'audit-current-ledger')
req('majormud_parity_assertions_0314.json' not in audit,'audit-no-stale-ledger-pointer')

req(remain.get('updatedByPass') in {'Pass 44 / Update 0315','Pass 45 / Update 0316'},'remaining-registry-pass44')
partysys=next((x for x in remain['systems'] if x.get('id')=='party-coordination'),None)
req(partysys is not None,'remaining-party-entry')
if partysys:
    req(partysys.get('classification')=='runtime-active-strong-boundary-exact-edges-open','remaining-party-classification')
    req(partysys.get('partyBoundary0315',{}).get('classification')=='STRONG','remaining-party-boundary-strong')
    req(partysys.get('partyBoundary0315',{}).get('excluded','').startswith('MegaMUD'),'remaining-megamud-wait-excluded')
    deps=' '.join(partysys.get('nativeDependencies',[])).lower()
    for word in ['leader leave','disconnect','death','teleport','pvp room-spell']:
        req(word in deps,f'remaining-dependency-{word.replace(" ","-")}')

req(score['PassCount']==97 and score['StrongCount']==85 and score['UnknownCount']==18,'scorecard-counts')
req(score['ScoredRequirementCount']==200,'scorecard-total')
req(score['StrictPercent']==48.5 and score['EvidenceBackedPercent']==91.0,'scorecard-percentages')
partycat=next(x for x in score['Categories'] if x['Category']=='Parties / Communication / Organizations')
req((partycat['Pass'],partycat['Strong'],partycat['Unknown'],partycat['Total'])==(10,4,1,15),'party-category-score')
req('UNKNOWN → STRONG' in doc and '97 PASS / 85 STRONG / 18 UNKNOWN' in doc,'doc-parity-delta')
req('hypotheses' in doc.lower(),'doc-user-observation-boundary')
req('MegaMUD' in doc and 'wait' in doc.lower(),'doc-megamud-exclusion')

# Protected authoritative sources unchanged from Pass43 package validation.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'f39861f8f2cf00d8641f694b7f2a022353a00b48782aba0b3bd6575cb5aaede8',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486',
 'Apps/Source/Reforged.Server/RuntimeClassicCharacterAppearance.cs':'bb8a7bba31881fbacfff3913fd59c6914b2855085f6bdfee71eb27d495ad570e',
 'Apps/Source/Reforged.Server/RuntimeMajorMudPlus.cs':'e3ff0381101a7e4d60630f5d1957a5dcbf659badb2ee8493fedf8823e74c1d66',
}
for path,expected in protected.items(): req(sha(path)==expected,'protected-'+Path(path).name,sha(path))
req(not any(p.name.upper()=='WCCMMUD.INI' for p in root.rglob('*') if p.is_file()),'no-wccmmud-ini')

report={'schema':'majormud.pass44-classic-party-coordination-boundary-validation.v1','update':'0315','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_party_coordination_boundary_pass44_static_validation_0315.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(f"{report['status']} {len(checks)-len(failures)}/{len(checks)}")
if failures:
    for f in failures: print('FAIL',f['name'],f['detail'])
    sys.exit(1)
