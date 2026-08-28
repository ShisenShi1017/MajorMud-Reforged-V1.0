#!/usr/bin/env python3
from __future__ import annotations
import hashlib,json,re,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(ok,name,detail=''):
    checks.append(name)
    if not ok: failures.append({'name':name,'detail':detail})
def js(rel): return json.loads((root/rel).read_text(encoding='utf-8'))
def text(rel): return (root/rel).read_text(encoding='utf-8')
def sha(rel): return hashlib.sha256((root/rel).read_bytes()).hexdigest()

prior=js('Librarium/Parity/majormud_parity_assertions_0315.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0316.json')
ev=js('Librarium/Parity/majormud_classic_trap_boundary_evidence_0316.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0316.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
native=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
reg=text('Apps/Source/Reforged.Server/RuntimeClassicTrapEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass45ClassicTrapBoundarySmokeReport.cs')
living=text('Apps/Source/Reforged.Server/RuntimeLivingWorld.cs')
traps=text('Apps/Source/Reforged.Server/RuntimeClassicExitTraps.cs')
contract=text('Apps/Source/Reforged.Server/RuntimeClassicRoomExitNativeFieldContract.cs')
npc=text('Apps/Source/Reforged.Server/RuntimeClassicNpcAiNativeClosureAudit.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0316_CLASSIC_TRAP_BOUNDARY.md')

req(prior['update']=='0315' and ledger['update']=='0316','ledger-update-sequence')
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
changed=[(i,old[i]['state'],cur[i]['state']) for i in old if old[i]['state']!=cur[i]['state']]
req(changed==[('native.traps','UNKNOWN','STRONG')],'only-trap-promotion',str(changed))
req(len(cur)==200,'ledger-200-assertions')
counts={k:sum(a['state']==k for a in ledger['assertions']) for k in ('PASS','STRONG','UNKNOWN')}
req(counts=={'PASS':97,'STRONG':86,'UNKNOWN':17},'parity-counts',str(counts))
req(abs((counts['PASS']/200*100)-48.5)<1e-9,'strict-percent-48.5')
req(abs(((counts['PASS']+counts['STRONG'])/200*100)-91.5)<1e-9,'evidence-backed-percent-91.5')
req(cur['native.traps']['state']=='STRONG','trap-state-strong')
req(any(str(x).endswith('majormud_classic_trap_boundary_evidence_0316.json') for x in cur['native.traps']['evidence']),'trap-evidence-linked')
for phrase in ['per-exit Para2','SEARCH/disarm probability','failed-disarm','Para3/Para4','state-transition','reset/rearm']:
    req(phrase.lower() in cur['native.traps']['description'].lower(),f'trap-description-retains-{phrase.lower().replace("/","-").replace(" ","-")}')
req(cur['npc.native-combat-message-8480']['state']=='UNKNOWN','nahr-8480-stays-unknown')

req(ev['pass']==45 and ev['update']=='0316' and ev['target']=='native.traps','evidence-pass-update-target')
req(ev['parityPromotion']['assertion']=='native.traps' and ev['parityPromotion']['from']=='UNKNOWN' and ev['parityPromotion']['to']=='STRONG','evidence-promotion')
req(len(ev['nativeHelp'])==2,'two-native-help-anchors')
expected={15:('HL1B4B','2c30090896445871255b4e167609f68ea03e5d52bbc979af3b0d215ad82b563f'),245:('HL1B106','81e097285a019714d7b1c7a73b68087f756d94c55072a1a3527978d895215418')}
for row in ev['nativeHelp']:
    req(row['messageNumber'] in expected,'native-help-id-known',str(row['messageNumber']))
    sym,h=expected[row['messageNumber']]
    req(row['symbol']==sym,f'native-help-symbol-{row["messageNumber"]}')
    req(row['sha256']==h,f'native-help-hash-{row["messageNumber"]}')
    req(row['source']=='Librarium/Evidence/native_message_catalog_correlation_0022.json',f'native-help-source-{row["messageNumber"]}')
req(len(ev['strongCorrelations'])==4,'four-trap-correlations')
req(any(x['id']=='trap.catalog' for x in ev['strongCorrelations']),'trap-catalog-correlation')
req(any(x['id']=='trap.roomrec-schema' for x in ev['strongCorrelations']),'trap-schema-correlation')
req(any(x['id']=='trap.runtime-admission' for x in ev['strongCorrelations']),'trap-runtime-correlation')
req(any(x['id']=='trap.projection-loss' for x in ev['strongCorrelations']),'trap-projection-loss-correlation')
req(len(ev['remainingExactUnknowns'])==8,'eight-explicit-trap-unknowns')

# Verify exact native help records still exist in retained native catalog.
found={}
def walk(x):
    if isinstance(x,dict):
        if x.get('message_number') in expected and x.get('symbol')==expected[x.get('message_number')][0]: found[x['message_number']]=x
        for v in x.values(): walk(v)
    elif isinstance(x,list):
        for v in x: walk(v)
walk(native)
req(set(found)==set(expected),'retained-native-help-records-present')
req('Normally anyone can find a trap by searching' in found[15]['decoded_cp437'],'help15-anyone-search')
req('much higher chance' in found[15]['decoded_cp437'],'help15-traps-skill-higher-chance')
req('attempt to disarm any trap he finds' in found[15]['decoded_cp437'],'help15-disarm-found-trap')
req('after it is found with the' in found[245]['decoded_cp437'] and 'search' in found[245]['decoded_cp437'].lower(),'help245-found-before-disarm')
req('DISARM TRAP' in found[245]['decoded_cp437'],'help245-disarm-syntax')

for const,val in [('TrapSkillHelpMessageNumber','15'),('TrapDisarmHelpMessageNumber','245'),('DecodedExitTrapCount','295'),('DecodedDamageTrapCount','273'),('DecodedSpellTrapCount','22'),('FindTrapsAbilityId','40'),('DisarmTrapsAbilityId','41'),('FindTrapsValueAbilityId','179')]:
    req(f'const int {const} = {val};' in reg,f'registry-{const.lower()}')
for flag in ['ExactPerExitPara2Resolved','ExactSearchProbabilityResolved','ExactDisarmProbabilityResolved','ExactFailedDisarmTriggerResolved','ExactTrapMessageOwnershipResolved','ExactTrapStateTransitionsResolved','ExactTrapResetRearmResolved']:
    req(f'static readonly bool {flag} = false;' in reg,f'registry-open-{flag.lower()}')
req('Rows.Count != 10' in reg,'registry-ten-row-validation')
req('expected-three-proven-trap-rows' in reg and 'expected-two-strong-trap-rows' in reg and 'expected-five-open-trap-rows' in reg,'registry-state-count-validation')

# Runtime trap data and fail-closed behavior.
for token in ['Trap = 9','SpellTrap = 24','Active = 0','Inactive = 1','Hidden = 2','ActiveMovement = 3','InactiveMovement = 4']:
    req(token in contract,'native-field-contract-'+token.lower().replace(' ','-').replace('=','eq'))
req('IsKnownTrapState(int value) => value is >= 0 and <= 4;' in contract,'known-trap-state-range')
req('NativeActivationStateProjected' in traps and 'NativeActivationState' in traps,'classic-trap-native-state-fields')
req('effects.TryGetProperty("native_trap_state"' in traps,'future-explicit-native-trap-state-projection')
req('if (!trap.NativeActivationStateProjected) return string.Empty;' in traps,'missing-para2-fails-closed')
req('The five native state values are storage-proven' in traps,'projected-state-still-fails-closed-comment')
req('return string.Empty;' in traps,'trap-execution-fail-closed')
req('DetectionDifficulty' not in traps and 'DisarmDifficulty' not in traps,'classic-exit-traps-no-generic-difficulty')
req('DISARM TRAP <direction>' in living,'live-disarm-syntax')
req('HasClassicDisarmTrapsAbility' in living,'live-disarm-skill-helper')
req('abilityId==41' in living,'live-disarm-ability41-gate')
req('DISARM TRAP requires the Traps skill.' in living,'live-disarm-skill-message')
req('native SEARCH discovery, the disarm roll, and the trap state transition remain evidence-gated' in living,'live-disarm-unresolved-fail-closed')
req('generic hidden-' in living and 'object search roll' in living,'no-generic-search-roll-reuse')

# Nahr 8480 remains untouched/fail-closed.
req('message-8480-nahr-five-rows-remain-unbound' in npc,'nahr-five-unbound-guard')
req('unbound8480.Length == 5' in npc,'nahr-five-count')
req('row.Npc.LegacyId == 1006' in npc,'nahr-id1006')

req('--smoke-pass45-classic-trap-boundary' in program,'program-pass45-smoke-flag')
req('RuntimePass45ClassicTrapBoundarySmokeReport.Run(Directory.GetCurrentDirectory())' in program,'program-pass45-smoke-call')
req('classic-trap-evidence-registry-valid' in smoke,'smoke-registry-check')
req('runtime-classic-trap-catalog-295' in smoke,'smoke-runtime-count')
req('mme-projection-does-not-invent-per-exit-para2' in smoke,'smoke-projection-loss')
req('majormud_parity_assertions_0316.json' in audit,'audit-current-ledger')
req('majormud_parity_assertions_0315.json' not in audit,'audit-no-stale-ledger-pointer')

req(remain.get('updatedByPass')=='Pass 45 / Update 0316','remaining-registry-pass45')
trapsys=next((x for x in remain['systems'] if x.get('id')=='traps'),None)
req(trapsys is not None,'remaining-trap-entry')
if trapsys:
    req(trapsys.get('classification')=='runtime-active-strong-boundary-exact-execution-open','remaining-trap-classification')
    req(trapsys.get('evidenceClassification')=='STRONG','remaining-trap-evidence-strong')
    b=trapsys.get('pass45TrapBoundary0316',{})
    req(b.get('classification')=='STRONG','remaining-pass45-boundary')
    req((b.get('decodedExitTrapCount'),b.get('damageTrapCount'),b.get('spellTrapCount'))==(295,273,22),'remaining-trap-counts')
    req(b.get('nativeHelp')==['15/HL1B4B','245/HL1B106'],'remaining-native-help')
    req(b.get('perExitPara2Recovered') is False,'remaining-para2-open')
    for key in ['exactSearchRoll','exactDisarmRoll','failedDisarmTrigger','messageOwnership','stateTransitions','resetRearm']:
        req(b.get(key)=='UNKNOWN',f'remaining-{key.lower()}-unknown')

req(score['PassCount']==97 and score['StrongCount']==86 and score['UnknownCount']==17,'scorecard-counts')
req(score['ScoredRequirementCount']==200,'scorecard-total')
req(score['StrictPercent']==48.5 and score['EvidenceBackedPercent']==91.5,'scorecard-percentages')
roomcat=next(x for x in score['Categories'] if x['Category']=='Rooms & World')
req((roomcat['Pass'],roomcat['Strong'],roomcat['Unknown'],roomcat['Total'])==(11,10,0,21),'rooms-category-score')
req(roomcat['EvidenceBackedPercent']==100.0,'rooms-category-evidence-backed-100')
req('UNKNOWN → STRONG' in doc and '97 PASS / 86 STRONG / 17 UNKNOWN' in doc,'doc-parity-delta')
req('Nahr 8480' in doc and 'remains UNKNOWN' in doc,'doc-nahr-pivot')
req('No SEARCH or DISARM probability is inferred' in doc,'doc-no-formula-guess')

# Protected authoritative sources unchanged from Pass44 validation.
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

report={'schema':'majormud.pass45-classic-trap-boundary-validation.v1','update':'0316','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_trap_boundary_pass45_static_validation_0316.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(f"{report['status']} {len(checks)-len(failures)}/{len(checks)}")
if failures:
    for f in failures: print('FAIL',f['name'],f['detail'])
    sys.exit(1)
