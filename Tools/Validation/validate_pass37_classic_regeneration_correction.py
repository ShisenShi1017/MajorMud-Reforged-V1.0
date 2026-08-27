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

evidence=js('Librarium/Parity/majormud_classic_regeneration_correction_evidence_0308.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0308.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0307.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0308.json')
messages=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
binary=js('Librarium/BinaryCorrelation0085/wcc_wbb_binary_correlation_0085.json')
formula_gap=js('Librarium/FormulaGapResolution/formula_gap_resolution_index_0077.json')
csv=text('Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.csv')
regen=text('Apps/Source/Reforged.Server/RuntimeCharacterRegeneration.cs')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicRegenerationEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass37ClassicRegenerationCorrectionSmokeReport.cs')
item_smoke=text('Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0308_CLASSIC_REST_MEDITATE_REGENERATION.md')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')

# Exact native HELP message 205.
help205=None
for cat in messages.get('catalogs',[]):
    if cat.get('mcv_file')=='WCCMMHLP.mcv':
        help205=next((e for e in cat.get('entries',[]) if e.get('message_number')==205),None)
        break
req(help205 is not None,'native-help-205-present')
if help205:
    h=help205.get('decoded_cp437','')
    req(help205.get('sha256')=='a1dfc8abf42b30f79da5de75d34404409c3cf9f3e782be2daa3d4d0341b29b97','native-help-205-hash')
    req('REST command will increase your rate of HP regeneration' in h,'native-help-rest-increases-hp')
    req('MEDITATE command will increase your rate of Mana regeneration' in h,'native-help-meditate-increases-mana')
    req(h.count('at least 20 seconds')==2,'native-help-20-second-both-modes')
    req('If you move, attack, or are attacked during this period' in h,'native-help-interruption-causes')
    req('will be terminated and you will get no bonus' in h,'native-help-no-bonus-on-interruption')

# Binary strings corroborate native owners but do not overclaim formula.
timing=binary.get('timing_strings',[])
req('RESTING - REST REGEN %d.' in timing,'binary-rest-regen-string')
req('MEDITATE - MANA REGEN %d.' in timing,'binary-meditate-regen-string')
req(binary.get('status')=='PASS_WITH_LIMITATIONS','binary-correlation-limited')

# Retained MMUD Explorer formula branch anchors.
for line,name in [
('Result := (((Level + 20) * HEA) div 750);','retained-stock-pascal-div750'),
('nHPRegen = Fix(((nLevel + 20) * nHealth) / 500)','retained-greatermud-div500'),
('nHPRegen = Fix(((nLevel + 20) * nHealth) / 750)','retained-stock-div750'),
('CalcManaRegen = Fix((nINT + nWil) / 2)','retained-druid-average'),
('CalcManaRegen = Fix(((nMPRegen + 100) * 1) / 100)','retained-kai-special'),
('CalcManaRegen = Fix((((nLevel + 20) * CalcManaRegen) * (nMagicLVL + 2)) / 1650)','retained-mana-div1650')]:
    req(line in csv,name)

# Formula gap retained cadence boundary.
fg=json.dumps(formula_gap)
req('0077-REGEN-TICKS' in fg,'formula-gap-regen-record')
req('rest 20s, regen 30s, meditate 10s' in fg,'formula-gap-cadence-30-20-10')
req('resting-rate' in fg and 'passive HP = HPRegen/3 per 30s' in fg,'formula-gap-resting-rate-shape')

# Structured Pass37 evidence is conservative.
req(evidence.get('schema')=='majormud.classic-regeneration-correction.v1','evidence-schema')
req(evidence.get('update')=='0308' and evidence.get('pass')==37,'evidence-pass37')
req(evidence.get('classification')=='STRONG_STOCK_CLASSIC_REGENERATION_BOUNDARY','evidence-classification')
req(evidence.get('strongRecoveredFormulaEvidence',{}).get('upstreamBlobSha')=='1d7981a0edcf9312a89f935277541a16fad81b4d','upstream-source-sha')
req(evidence.get('strongCadenceEvidence',{}).get('passiveSeconds')==30,'evidence-passive-30')
req(evidence.get('strongCadenceEvidence',{}).get('restSeconds')==20,'evidence-rest-20')
req(evidence.get('strongCadenceEvidence',{}).get('meditateSeconds')==10,'evidence-meditate-10')
promo=evidence.get('parityPromotion',{})
req(promo.get('assertion')=='native.rest-inn-recovery-policy','promotion-target')
req(promo.get('from')=='UNKNOWN' and promo.get('to')=='STRONG','promotion-unknown-to-strong')
req(len(evidence.get('stillUnknown',[]))>=4,'evidence-keeps-unknown-boundaries')

# Live runtime correction.
req('PassiveCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.PassiveRegenerationSeconds)' in regen and 'PassiveRegenerationSeconds = 30' in text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs'),'runtime-passive-30')
req('RestCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.RestRegenerationSeconds)' in regen and 'RestRegenerationSeconds = 20' in text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs'),'runtime-rest-20')
req('MeditateCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.MeditateRegenerationSeconds)' in regen and 'MeditateRegenerationSeconds = 10' in text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs'),'runtime-meditate-10')
req('((level + 20) * health) / 750' in regen,'runtime-stock-hp-div750')
req('CalculateClassicHitPointBase(character.Level, health) * 3' in regen,'runtime-rest-x3')
req('return meditating ? baseRate : ApplyPercent(baseRate, percent);' in regen,'runtime-meditate-before-mpregen')
req('if (magic.Type == 5)' in regen and 'return ApplyPercent(1, percent);' in regen,'runtime-kai-special')
req('GreaterMUD /500 branch' in regen,'runtime-explicitly-rejects-greatermud-branch')
req('/ 500)' not in re.sub(r'//.*','',regen),'runtime-no-active-div500')

# Evidence registry and smoke keep unresolved phase/presentation/cleanup gated.
for token in ['StockHitPointDivisor = 750','RestingMultiplier = 3','PassiveCadenceSeconds = 30','RestCadenceSeconds = 20','MeditateCadenceSeconds = 10','FocusActivationSeconds = 20']:
    req(token in registry,'registry-'+token.split()[0].lower())
req('GameplayFormulaCorrected = true' in registry,'registry-formula-corrected')
req('StockMajorMudBranchSelected = true' in registry,'registry-stock-branch')
for token in ['ExactHostPhaseResolved = false','ExactMeditateResponseResolved = false','ExactStatusCleanupOrderingResolved = false']:
    req(token in registry,'registry-gate-'+token.split()[0])
req('RuntimeClassicRegenerationEvidenceState.RejectedForClassic' in registry,'registry-rejects-greatermud')
req('RuntimeClassicRegenerationEvidenceRegistry.Validate().Count == 0' in smoke,'smoke-registry-valid')
for token in ['passive.HitPointRate == 3','rest.HitPointRate == 9','passive.ManaRate == 8','meditate.ManaRate == 4']:
    req(token in smoke,'smoke-probe-'+token.split('==')[1].strip())
req('ManaRate == 2' in smoke,'smoke-kai-two')
req('rest.HitPointRate != 9 || meditate.ManaRate != 4' in item_smoke,'item-smoke-corrected-rest-meditate')
req('kaiPassive.ManaRate != 2 || kaiMeditate.ManaRate != 2' in item_smoke,'item-smoke-kai')

# CLI and current audit ledger.
req('--smoke-pass37-classic-regeneration-correction' in program,'pass37-smoke-cli')
req('RuntimePass37ClassicRegenerationCorrectionSmokeReport.Run()' in program,'pass37-smoke-run')
req(program.count('--smoke-pass37-classic-regeneration-correction')==1,'pass37-smoke-cli-single')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-current-ledger')
req('majormud_parity_assertions_0307.json' not in audit,'runtime-audit-old-ledger-retired')

# Only target state changes from 0307 -> 0308.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior.get('update')=='0307','prior-ledger-0307')
req(ledger.get('update')=='0308','ledger-update-0308')
req(len(old)==200 and len(cur)==200,'ledger-total-stable-200')
req(old['native.rest-inn-recovery-policy']['state']=='UNKNOWN','prior-rest-policy-unknown')
req(cur['native.rest-inn-recovery-policy']['state']=='STRONG','current-rest-policy-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('native.rest-inn-recovery-policy','UNKNOWN','STRONG')],'only-one-state-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,78,25),'ledger-counts-97-78-25',str(counts))
req(cur['character.hp-regeneration-formula']['state']=='STRONG','hp-formula-stays-strong')
req(cur['character.mana-regeneration-meditation']['state']=='STRONG','mana-formula-stays-strong')
req('0308' in ''.join(cur['character.hp-regeneration-formula']['evidence']),'hp-formula-links-pass37')
req('0308' in ''.join(cur['character.mana-regeneration-meditation']['evidence']),'mana-formula-links-pass37')

# Scorecard exact.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,78,25,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict-48.5')
req(abs(score['EvidenceBackedPercent']-87.5)<0.01,'score-evidence-87.5')
cat={c['Category']:c for c in score['Categories']}
req((cat['Character & Status']['Pass'],cat['Character & Status']['Strong'],cat['Character & Status']['Unknown'],cat['Character & Status']['Total'])==(24,17,5,46),'character-category-counts')
req(abs(cat['Character & Status']['EvidenceBackedPercent']-89.1)<0.01,'character-category-evidence-89.1')

# Remaining-system registry now expresses the narrowed boundary.
systems={s['id']:s for s in remain['systems']}
r=systems['rest-inn-recovery-policy']
req(remain.get('updatedByPass') in {'Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313'},'remaining-registry-update')
req(r.get('status')=='implemented-strong-classic-regeneration-boundary','remaining-rest-status')
req(r.get('evidenceClassification')=='strong-stock-major-mud-regeneration-with-native-help-boundary','remaining-rest-evidence-class')
rr=r.get('classicRegeneration0308',{})
req(rr.get('stockHitPointDivisor')==750 and rr.get('restingMultiplier')==3,'remaining-stock-hp')
req((rr.get('passiveCadenceSeconds'),rr.get('restCadenceSeconds'),rr.get('meditateCadenceSeconds'),rr.get('focusActivationSeconds'))==(30,20,10,20),'remaining-cadences')
req(len(r.get('nativeDependencies',[]))==3,'remaining-keeps-native-dependencies')

# Documentation makes confidence boundary clear.
for token in ['GreaterMUD','/750','multiply `baseHP` by **3**','UNKNOWN -> STRONG','97 PASS / 78 STRONG / 25 UNKNOWN = 200','Exact host-clock phase']:
    req(token in doc,'doc-token-'+re.sub(r'\W+','-',token)[:24])

# User-protected Borin / Custom1 / WPF sources stay byte-identical.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'76d6dab9d3c1384bdbe2d6bd7c7c9464ea3c99130221cbbdf1ec36bcd7d86731',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected in protected.items():
    req((root/rel).exists(),'protected-exists-'+Path(rel).name)
    req(sha(rel)==expected,'protected-hash-'+Path(rel).name)
app=text('Apps/Source/Reforged.App/App.xaml.cs'); main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

# No native INI contamination.
ini=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini,'no-wccmmud-ini-file',str(ini))

report={'schema':'majormud.pass37-classic-regeneration-correction-validation.v1','update':'0308','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_regeneration_correction_pass37_static_validation_0308.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
