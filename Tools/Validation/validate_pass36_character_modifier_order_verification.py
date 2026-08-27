#!/usr/bin/env python3
import hashlib, json, re
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

evidence=js('Librarium/Parity/majormud_character_modifier_order_verification_evidence_0307.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0307.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0306.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0307.json')
creation=text('Librarium/Evidence/Character/CHARACTER_CREATION_PASS_0141.md')
secondary=text('Librarium/Evidence/Character/CHARACTER_HP_MANA_CALC_MUDBRAIN_0137.md')
equipment=text('Librarium/Evidence/Character/CHARACTER_STATISTICS_PASS_0154.md')
combat=text('Librarium/Evidence/Combat/DLLCombatResolutionOrder_0130.md')
abilities=js('Librarium/Objects/abilities/ability_records.json')
registry_code=text('Apps/Source/Reforged.Server/RuntimeCharacterModifierOrderEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass36CharacterModifierOrderVerificationSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0307_CLASSIC_CHARACTER_MODIFIER_ORDER_VERIFICATION.md')

# Native source anchors used to vet the screenshots.
req('Starting statistics are the selected race minimum statistics.' in creation,'creation-race-minimum-exact-anchor')
req('calculate_secondary_stats @0x0041a424' in secondary,'secondary-native-function-anchor')
req('every time stats change (creation, level-up, stat drain, equip)' in secondary,'secondary-recompute-trigger-anchor')
req('MaxHP ability' in secondary and 'equip cache' in secondary,'secondary-ability-equipment-cache-anchor')
req('checked additive aggregation' in equipment,'equipment-additive-anchor')
req('cross-category modifier ordering remain unknown' in equipment,'equipment-global-order-stays-unresolved')
req('HIT CHANCE (Armor Class)' in combat and 'DODGE' in combat,'combat-ac-dodge-order-anchor')
req('negative dodge_bn (blind/debuff)' in combat,'combat-blind-debuff-field-anchor')

# Primary-stat ability descriptions are signed parameter deltas, not percentage-stage proof.
ability_map={str(r.get('legacy_id')):r for r in abilities.get('records',[])}
expected={45:'Wisdom',46:'Strength',47:'Health',48:'Agility',49:'Charm'}
for aid,name in expected.items():
    row=ability_map.get(str(aid))
    req(row is not None,f'ability-{aid}-present')
    if row:
        fields=row.get('public_reference_0139',{}).get('fields',{})
        req(fields.get('name')==name,f'ability-{aid}-name')
        desc=str(fields.get('description',''))
        req('Increases or decreases' in desc and 'value specified in the parameter' in desc,f'ability-{aid}-signed-parameter-description',desc)

# Structured Pass36 evidence remains conservative.
req(evidence.get('schema')=='majormud.classic-character-modifier-order-verification.v1','evidence-schema')
req(evidence.get('update')=='0307' and evidence.get('pass')==36,'evidence-pass36')
req(evidence.get('classification')=='STRONG_PARTIAL_NATIVE_ORDER_BOUNDARY','evidence-classification')
req(evidence.get('runtimeMutation') is False,'evidence-no-runtime-mutation')
promo=evidence.get('parityPromotion',{})
req(promo.get('assertion')=='character.cross-category-modifier-order','promotion-target')
req(promo.get('from')=='UNKNOWN' and promo.get('to')=='STRONG','promotion-unknown-to-strong')
rejected={x['claim']:x['disposition'] for x in evidence.get('rejectedOrUnprovenScreenshotClaims',[])}
req(any('creation' in k and v=='REJECTED' for k,v in rejected.items()),'reject-class-base-creation')
req(any('ten seconds' in k and v=='REJECTED' for k,v in rejected.items()),'reject-ten-second-poison')
req(any('equipment' in k and v=='UNPROVEN' for k,v in rejected.items()),'equipment-percent-stage-unproven')
req(any('blindness' in k and v=='REJECTED_AS_CURRENT_AUTHORITY' for k,v in rejected.items()),'blindness-minus50-not-adopted')
req(any('sleep/paralysis' in k and v=='UNPROVEN_NOT_ADOPTED' for k,v in rejected.items()),'sleep-paralysis-ac-zero-not-adopted')

# Historical ledger stays historical; only the targeted assertion moves state.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior.get('update')=='0306','prior-ledger-0306')
req(ledger.get('update')=='0307','ledger-update-0307')
req(len(old)==200 and len(cur)==200,'ledger-total-stable-200')
req(old['character.cross-category-modifier-order']['state']=='UNKNOWN','prior-cross-category-unknown')
req(cur['character.cross-category-modifier-order']['state']=='STRONG','current-cross-category-strong')
changed=[]
for aid,row in cur.items():
    if old[aid]['state']!=row['state']: changed.append((aid,old[aid]['state'],row['state']))
req(changed==[('character.cross-category-modifier-order','UNKNOWN','STRONG')],'only-one-state-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,77,26),'ledger-counts-97-77-26',str(counts))
req(cur['spells.spell-round-three-seconds']['state']=='PASS','three-second-status-round-still-pass')
req('three-second' in cur['spells.spell-round-three-seconds']['description'],'three-second-status-round-description')

# Scorecard matches the ledger exactly.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,77,26,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict-48.5')
req(abs(score['EvidenceBackedPercent']-87.0)<0.01,'score-evidence-87.0')
cat={c['Category']:c for c in score['Categories']}
req((cat['Character & Status']['Pass'],cat['Character & Status']['Strong'],cat['Character & Status']['Unknown'],cat['Character & Status']['Total'])==(24,16,6,46),'character-category-counts')
req(abs(cat['Character & Status']['EvidenceBackedPercent']-87.0)<0.01,'character-category-evidence-87')

# Evidence-only runtime registry; no execution authority is accidentally enabled.
for token in [
    'GameplayFormulaChanged = false',
    'UniversalCrossCategoryOrderResolved = false',
    'GenericEquipmentPercentageStageResolved = false',
    'DiseaseCurseInjuryOrderingResolved = false',
    'HardStateStructMutationResolved = false']:
    req(token in registry_code,'registry-gate:'+token.split()[0])
req('expected-ten-evidence-rows' in registry_code,'registry-row-count-validation')
req('screenshot.poison-ten-second-tick' in registry_code and 'RuntimeCharacterModifierOrderEvidenceState.Rejected' in registry_code,'registry-poison-rejection')
req('screenshot.global-linear-pipeline' in registry_code and 'RuntimeCharacterModifierOrderEvidenceState.Unknown' in registry_code,'registry-global-order-unknown')
req('RuntimeCharacterModifierOrderEvidenceRegistry.Validate().Count == 0' in smoke,'smoke-registry-validation')
req('no-gameplay-formula-change' in smoke,'smoke-no-formula-change')
req('--smoke-pass36-character-modifier-order-verification' in program,'pass36-smoke-cli')
req('RuntimePass36CharacterModifierOrderVerificationSmokeReport.Run()' in program,'pass36-smoke-run')
req(program.count('--smoke-pass36-character-modifier-order-verification')==1,'pass36-smoke-cli-single')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-current-ledger')
req('majormud_parity_assertions_0306.json' not in audit,'runtime-audit-old-ledger-retired')

# The pass documentation must explicitly preserve the evidence boundary.
for token in ['hypotheses','No gameplay formula or modifier caller is changed','97 PASS / 77 STRONG / 26 UNKNOWN = 200','3-second']:
    req(token in doc,'doc-boundary:'+token[:18])

# No gameplay modifier implementation source changed relative to Pass35 reconstructed baseline.
gameplay_hashes={
 'Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs':'080484f6376cded3aa7c6daff337161c83df0ca90221f7aa7ab0eba7e11af839',
 'Apps/Source/Reforged.Server/RuntimePlayerCombatProfiles.cs':'93936ff047bd475fb9379147849c154c02666582459e196935cf8f82dc63b44a',
 'Apps/Source/Reforged.Server/ClassicCombatResolver.cs':'411c2465b220c7937c081aaa22189eef1e1844936f2dd5fb93630eb5a6244fa0',
 'Apps/Source/Reforged.Server/RuntimeClassicFormulaAuthority.cs':'ca1d7745e7c858805391913c10b81e7883e75c547909293ff4943ff9b6c4800c',
 'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs':'f24ddf74b95013b6e94af50461a1b15a6f07e98d69d0719923b435fb145f6f6a'}
for rel,expected_hash in gameplay_hashes.items(): req(sha(rel)==expected_hash,'gameplay-source-unchanged:'+Path(rel).name)
status_now=text('Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs'); timing_now=text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs')
req('CombatRoundSeconds = RuntimeClassicTimingAuthority.CombatRoundSeconds' in status_now and 'CombatRoundSeconds = 5' in timing_now,'gameplay-timing-centralized-combat-unchanged')
req('SpellRoundSeconds = RuntimeClassicTimingAuthority.SpellSystemRoundSeconds' in status_now and 'SpellSystemRoundSeconds = 3' in timing_now,'gameplay-timing-centralized-spell-unchanged')

# User-protected Borin / Custom1 / WPF sources remain byte-identical.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'76d6dab9d3c1384bdbe2d6bd7c7c9464ea3c99130221cbbdf1ec36bcd7d86731',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected_hash in protected.items():
    req((root/rel).exists(),'protected-exists:'+Path(rel).name)
    req(sha(rel)==expected_hash,'protected-hash:'+Path(rel).name)
app=text('Apps/Source/Reforged.App/App.xaml.cs'); main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

# Native INI remains evidence-only; Reforged must not acquire a WCCMMUD.INI.
ini_files=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini_files,'no-wccmmud-ini-file',str(ini_files))

report={'schema':'majormud.pass36-character-modifier-order-verification-validation.v1','update':'0307','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/character_modifier_order_verification_pass36_static_validation_0307.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
