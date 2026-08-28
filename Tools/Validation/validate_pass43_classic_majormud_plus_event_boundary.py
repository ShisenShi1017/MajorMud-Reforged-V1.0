#!/usr/bin/env python3
import hashlib, json, re
from pathlib import Path
from collections import Counter

root = Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append({'check':name,'detail':detail})
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))
def sha(rel): return hashlib.sha256((root/rel).read_bytes()).hexdigest()

prior=js('Librarium/Parity/majormud_parity_assertions_0313.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0314.json')
evidence=js('Librarium/Parity/majormud_classic_majormud_plus_event_boundary_evidence_0314.json')
plus=js('Librarium/Objects/majormud_plus/majormud_plus_records_0139.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0314.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicMajorMudPlusEventEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass43ClassicMajorMudPlusEventBoundarySmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
runtime=text('Apps/Source/Reforged.Server/RuntimeMajorMudPlus.cs')
closure=text('Reports/RuntimeVerification/majormud_plus_runtime_closure.md')
doc=text('Docs/CODE_UPDATE_0314_CLASSIC_MAJORMUD_PLUS_EVENT_BOUNDARY.md')
appearance=text('Apps/Source/Reforged.Server/RuntimeClassicCharacterAppearance.cs')

# Retained source identity and exact native-derived contest contract.
req(sha('Librarium/Objects/majormud_plus/majormud_plus_records_0139.json')=='4a22ad0728335c2c88a2fc2b477b65878dcee8e58348f7e17e5b12853fc7cedc','plus-contract-sha')
req(sha('Reports/RuntimeVerification/majormud_plus_runtime_closure.md')=='155cc6a08f03e90f98844045fe6f55ec86ab9439fd48eeff28460431aae31654','plus-runtime-closure-sha')
rec=plus['records'][0]; contract=rec['contest_contract']; native=contract['native_evidence']
req(rec['id']=='majormud_plus.entertainment_edition','plus-record-id')
req(rec['contest_modes']==['visible','hidden'],'contest-visible-hidden-modes')
req(contract['canonical_item_id']=='item.965' and contract['legacy_item_number']==965,'contest-item-965')
req(contract['maximum_attempts_per_item']==50,'contest-maximum-attempts-50')
req(contract['excluded_room_attribute_mask']==16,'contest-room-mask-0x10')
req(contract['visible_room_capacity']==17 and contract['hidden_room_capacity']==15,'contest-capacities-17-15')
req(len(contract['placement_ranges'])==17,'contest-range-count-17')
req(sum(r['quantity'] for r in contract['placement_ranges'])==40,'contest-rune-total-40')
expected_ranges=[
 (1,547,815,1),(1,869,1009,1),(1,1451,1633,1),(1,1640,1794,1),(1,1833,2117,1),
 (2,41,1122,4),(2,1124,2514,6),(3,1,785,3),(6,134,548,2),(6,751,832,1),
 (6,836,994,1),(6,996,1243,1),(6,1407,1566,1),(6,1626,3107,4),(7,1,1358,6),
 (8,1,955,5),(10,1,274,1)]
actual_ranges=[(r['map'],r['minimum_room'],r['maximum_room'],r['quantity']) for r in contract['placement_ranges']]
req(actual_ranges==expected_ranges,'contest-exact-placement-table',str(actual_ranges))
req(native['utility']=='WCCMMUTL.EXE','native-utility')
req(native['placement_routine']=='0x00404D80-0x00404FA1','native-placement-routine')
req(native['room_admission_and_insert']=='0x00404CC4-0x00404D7F','native-room-admission-routine')
req(native['result_scan']=='0x00404FA2-0x0040521C','native-result-scan-routine')
req(native['classification']=='native_executable_exact','native-executable-classification')
for token in ['17 range rows place 40 `item.965` runes','at most 50 attempts','17-visible / 15-hidden','exclude room attribute `0x10`','does not contain an automatic prize or ranking mutation']:
    req(token in closure,'closure-'+re.sub(r'\W+','-',token)[:45])

# Evidence artifact and guarded promotion.
req(evidence['schema']=='majormud.classic-majormud-plus-event-boundary-evidence.v1','evidence-schema')
req(evidence['pass']==43 and evidence['update']=='0314','evidence-pass-update')
req(evidence['retainedSource']['nativeUtility']=='WCCMMUTL.EXE','evidence-native-utility')
req(evidence['exactContestContract']['legacy_item_number']==965,'evidence-item-965')
req(len(evidence['exactContestContract']['placement_ranges'])==17,'evidence-17-ranges')
req(sum(r['quantity'] for r in evidence['exactContestContract']['placement_ranges'])==40,'evidence-40-runes')
req(len(evidence['remainingUnknown'])==3,'evidence-three-rng-open-edges')
for token in ['Random.Shared as native RNG evidence','a universal native event scheduler','automatic prize or ranking mutation','guessed contest duration/reset cadence']:
    req(token in evidence['notPromoted'],'not-promoted-'+re.sub(r'\W+','-',token)[:45])
req(evidence['parityPromotion']['assertion']=='native.events','promotion-target')
req(evidence['parityPromotion']['from']=='UNKNOWN' and evidence['parityPromotion']['to']=='STRONG','promotion-state')

# Live owner matches the exact structural contract, but keep RNG exactness unresolved.
for token in [
 'contract.GetProperty("canonical_item_id")','ReadInt(contract, "legacy_item_number")','ReadInt(contract, "maximum_attempts_per_item")',
 'ReadInt(contract, "excluded_room_attribute_mask")','ReadInt(contract, "visible_room_capacity")','ReadInt(contract, "hidden_room_capacity")',
 'ranges.Sum(value => value.Quantity) != 40','catalog.ContestModes.Contains(mode)','contract.MaximumAttemptsPerItem',
 'contract.ExcludedRoomAttributeMask','contract.HiddenRoomCapacity : contract.VisibleRoomCapacity',
 'RemoveGroundItems(catalog.ContestContract.CanonicalItemId)','majormud-plus-contest-finish']:
    req(token in runtime,'runtime-'+re.sub(r'\W+','-',token)[:45])
req('Random.Shared.Next(candidates.Count)' in runtime,'runtime-rng-remains-reforged-unproven')
req(sha('Apps/Source/Reforged.Server/RuntimeMajorMudPlus.cs')=='e3ff0381101a7e4d60630f5d1957a5dcbf659badb2ee8493fedf8823e74c1d66','runtime-plus-gameplay-source-unchanged')

# Dedicated registry/smoke must preserve the UNKNOWN/excluded edges.
for token in ['LegacyRuneItemNumber = 965','CanonicalRuneItemId = "item.965"','PlacementRangeCount = 17','TotalRuneQuantity = 40',
 'MaximumAttemptsPerRune = 50','ExcludedRoomAttributeMask = 0x10','VisibleRoomCapacity = 17','HiddenRoomCapacity = 15',
 'ExactNativeRngCallsiteThreadDrawOrderingResolved = false','AutomaticPrizeOrRankingMutationPromoted = false','GenericNativeEventSchedulerPromoted = false',
 'contest.native-routines','contest.rune-identity','contest.placement-ranges','contest.attempt-and-room-boundary','contest.visible-hidden-modes',
 'contest.status-finish-scan','contest.no-auto-prize-ranking','contest.live-runtime-connection','contest.native-rng-order','generic.event-scheduler',
 'expected-seven-proven-plus-event-rows','expected-one-strong-plus-event-row','expected-one-open-plus-event-row','expected-one-excluded-plus-event-row']:
    req(token in registry,'registry-'+re.sub(r'\W+','-',token)[:46])
for token in ['classic-plus-event-evidence-registry-valid','native-rune-item-965','native-seventeen-placement-ranges','native-forty-runes',
 'native-fifty-attempt-cap','native-room-mask-0x10','native-visible-capacity-17','native-hidden-capacity-15',
 'exact-contest-rng-order-remains-unknown','no-invented-prize-ranking','no-generic-native-event-scheduler-claim']:
    req(token in smoke,'smoke-'+token)

# CLI and current audit ledger.
req('--smoke-pass43-classic-majormud-plus-event-boundary' in program,'pass43-cli')
req('RuntimePass43ClassicMajorMudPlusEventBoundarySmokeReport.Run()' in program,'pass43-run')
req(program.count('--smoke-pass43-classic-majormud-plus-event-boundary')==1,'pass43-cli-single')
req('majormud_parity_assertions_0316.json' in audit,'audit-current-0314')
req('majormud_parity_assertions_0313.json' not in audit,'audit-prior-retired')

# Exactly one assertion state change.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior['update']=='0313' and ledger['update']=='0314','ledger-updates')
req(len(old)==200 and len(cur)==200,'ledger-total-200')
req(old['native.events']['state']=='UNKNOWN','prior-events-unknown')
req(cur['native.events']['state']=='STRONG','current-events-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('native.events','UNKNOWN','STRONG')],'only-events-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,84,19),'ledger-counts-97-84-19',str(counts))
for aid in ['character.death-xp-loss-exact','native.global-rng.native-contract','npc.native-ai-semantics','native.traps','native.world-reset-cleanup']:
    req(cur[aid]['state']=='UNKNOWN','neighbor-remains-unknown-'+aid)

# Scorecard.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,84,19,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict')
req(abs(score['EvidenceBackedPercent']-90.5)<0.01,'score-evidence')
tc=next(c for c in score['Categories'] if c['Category']=='Timers / Resets / World Rules')
req((tc['Pass'],tc['Strong'],tc['Unknown'],tc['Total'])==(6,8,1,15),'timers-category-counts',str(tc))
req(abs(tc['EvidenceBackedPercent']-93.3)<0.01,'timers-category-evidence')

# Remaining systems registry.
req(remain.get('updatedByPass') in {'Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'remaining-update-0314')
events=next(s for s in remain['systems'] if s['id']=='events')
req(events['status']=='implemented-strong-classic-plus-contest-boundary','remaining-events-status')
req(events['evidenceClassification']=='native-executable-exact-structure-with-rng-order-unresolved','remaining-events-evidence')
eb=events['classicPlusEvent0314']
req(eb['state']=='STRONG' and eb['legacyItemNumber']==965 and eb['placementRangeCount']==17 and eb['runeQuantity']==40,'remaining-events-contract')
req(eb['maximumAttemptsPerRune']==50 and eb['excludedRoomAttributeMask']==16 and eb['visibleRoomCapacity']==17 and eb['hiddenRoomCapacity']==15,'remaining-events-caps')
req(eb['exactRngDrawOrderingResolved'] is False and eb['automaticPrizeOrRankingMutation'] is False and eb['genericNativeEventSchedulerClaimed'] is False,'remaining-events-gates')

# CS0162 cleanup: exactly the existing compile-time invariant flags become runtime-readonly invariants.
warning_flags={
 'Apps/Source/Reforged.Server/WorldgroupRngDirectCallsiteRegistry.cs':['IndirectCallsitesResolved','GameplaySemanticMappingResolved','ExecutionThreadCrtMappingResolved','GameplayDrawOrderingResolved','InstructionEquivalentRangeLoopPortResolved'],
 'Apps/Source/Reforged.Server/RuntimeCharacterModifierOrderEvidenceRegistry.cs':['GameplayFormulaChanged','UniversalCrossCategoryOrderResolved','GenericEquipmentPercentageStageResolved','DiseaseCurseInjuryOrderingResolved','HardStateStructMutationResolved'],
 'Apps/Source/Reforged.Server/RuntimeClassicConfigurationAuthority.cs':['ReforgedUsesNativeConfigurationFile'],
 'Apps/Source/Reforged.Server/RuntimeClassicLifecycleDeathEvidenceRegistry.cs':['PriorityQueueFormulaPromoted','FixedThreeMinuteLinkdeadTimerPromoted','SameUserIpReconnectRulePromoted','PlayerJsonNativeSavePromoted','DeathXpUnderLevelTenImmunityPromoted','DeathXpTenPercentPromoted','DeathXpTwoMillionCapPromoted','ExactLeaderExitSuccessionResolved','ExactLinkdeadGameplayResolved','ExactCrossOwnerExitOrderingResolved'],
 'Apps/Source/Reforged.Server/RuntimeClassicRegenerationEvidenceRegistry.cs':['GameplayFormulaCorrected','StockMajorMudBranchSelected','ExactHostPhaseResolved','ExactMeditateResponseResolved','ExactStatusCleanupOrderingResolved'],
 'Apps/Source/Reforged.Server/RuntimeClassicSchedulerEvidenceRegistry.cs':['UniversalTenSecondGameplayPhasePromoted','ExactSameCycleOrderingResolved','ExactHostPhaseResolved','ExactBackgroundCadenceResolved','ExactNpcPlayerFirstOrderingResolved','ExactSpawnNextTickActionResolved']}
flag_count=0
for rel,names in warning_flags.items():
    src=text(rel)
    for name in names:
        flag_count+=1
        req(re.search(rf'public\s+static\s+readonly\s+bool\s+{re.escape(name)}\s*=\s*(?:true|false)\s*;',src) is not None,'warning-readonly-'+name)
        req(re.search(rf'public\s+const\s+bool\s+{re.escape(name)}\b',src) is None,'warning-no-const-'+name)
identity=text('Apps/Source/Reforged.Server/RuntimeClassicIdentityShadowEvidenceRegistry.cs')
req('public static readonly int ShadowAbilityId = 9;' in identity,'warning-readonly-shadow-registry-id')
req('public const int ShadowAbilityId = 9;' not in identity,'warning-no-const-shadow-registry-id')
req(flag_count==32,'warning-bool-flag-count-32',str(flag_count))
# 32 bool flags + one Shadow registry int remove the 30 reported unreachable warning sites.

# Current authoritative Shadow implementation must remain exactly preserved.
req(sha('Apps/Source/Reforged.Server/RuntimeClassicCharacterAppearance.cs')=='bb8a7bba31881fbacfff3913fd59c6914b2855085f6bdfee71eb27d495ad570e','pass40-appearance-authoritative-sha')
for token in ['public const int ShadowAbilityId = 9;','public const string ShadowLookPayload = "A shadowy figure!";','TryResolveEquippedShadowLookOverride']:
    req(token in appearance,'pass40-shadow-'+re.sub(r'\W+','-',token)[:38])

# Protected current authoritative user work.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'f39861f8f2cf00d8641f694b7f2a022353a00b48782aba0b3bd6575cb5aaede8',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected in protected.items(): req(sha(rel)==expected,'protected-hash-'+Path(rel).name)
ini=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini,'no-wccmmud-ini',str(ini))

# Documentation should make the STRONG/RNG boundary and warning cleanup explicit.
for token in ['UNKNOWN → STRONG','97 PASS / 84 STRONG / 19 UNKNOWN = 200','90.5%','17','40','50','0x10','17 visible / 15 hidden','static readonly','CS0162','does **not** claim exact native contest RNG parity','No global RNG replacement was made']:
    req(token.lower() in doc.lower(),'doc-'+re.sub(r'\W+','-',token)[:42])

report={'schema':'majormud.pass43-classic-majormud-plus-event-boundary-validation.v1','update':'0314','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_majormud_plus_event_boundary_pass43_static_validation_0314.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
