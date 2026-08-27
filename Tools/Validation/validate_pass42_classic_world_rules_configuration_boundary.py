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

native=js('Librarium/Evidence/native_support_catalog_0020.json')
evidence=js('Librarium/Parity/majormud_classic_world_rules_configuration_boundary_evidence_0313.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0313.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0312.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0313.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
config=text('Apps/Source/Reforged.Server/RuntimeClassicConfigurationAuthority.cs')
timing=text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs')
pvp=text('Apps/Source/Reforged.Server/RuntimeLivePvp.cs')
alignment=text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')
worldrules=text('Apps/Source/Reforged.Server/RuntimeWorldRulesConfiguration.cs')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicWorldRulesConfigurationEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass42ClassicWorldRulesConfigurationBoundarySmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0313_CLASSIC_WORLD_RULES_CONFIGURATION_BOUNDARY.md')
evdoc=text('Librarium/Evidence/ClassicConfiguration/CLASSIC_WORLD_RULES_CONFIGURATION_BOUNDARY_PASS_0313.md')

# Native source identity and complete retained prompt catalog.
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
req(rec.get('configuration_prompt_count')==44 and len(prompts)==44,'native-44-prompts')
pt={(p.get('offset'),p.get('text')) for p in prompts}
for row in [(264,'Seconds between saving Player data? 60'),(303,'Seconds between saving Buffer data? 300'),(1363,'Maximum level difference for PVP? 100'),(1761,'Max EPs to forgive per day? 18'),(1792,'Minutes per Evil Point Cycle? 60'),(1825,'EPs to forgive per cycle? 1')]:
    req(row in pt,'native-active-prompt-'+str(row[0]))

# Evidence and promotion boundary.
req(evidence['schema']=='majormud.classic-world-rules-configuration-boundary-evidence.v1','evidence-schema')
req(evidence['pass']==42 and evidence['update']=='0313','evidence-pass-update')
req(evidence['nativeSource']['retainedPromptCount']==44,'evidence-prompt-count')
req(evidence['nativeSource']['reforgedShipsNativeIni'] is False,'evidence-no-native-ini')
req(len(evidence['activeExactDefaultProjections'])==6,'evidence-six-active-projections')
req(len(evidence['remainingUnknown'])==3,'evidence-three-open')
req(evidence['parityPromotion']=={'assertion':'native.world-rules-configuration','from':'UNKNOWN','to':'STRONG','reason':evidence['parityPromotion']['reason']},'evidence-promotion-shape')
for phrase in ['a WCCMMUD.INI parser/writer in Reforged','generic Reforged world-rule ranges as native evidence','generic Reforged mutability metadata as native evidence','unmapped WCCMMUD prompt effects','automatic activation of all retained setup defaults']:
    req(phrase in evidence['notPromoted'],'not-promoted-'+re.sub(r'\W+','-',phrase)[:36])

# Runtime authority and live consumers.
for tok in ['NativeConfigurationFileName = "WCCMMUD.INI"','ReforgedUsesNativeConfigurationFile = false','NativeConfigurationPromptCount = 44','PlayerDataSaveIntervalSeconds = 60','BufferDataSaveIntervalSeconds = 300','MaximumPvpLevelDifference = 100','MaximumEvilPointsForgivenPerDay = 18','EvilPointCycleMinutes = 60','EvilPointsForgivenPerCycle = 1']:
    req(tok in config,'config-'+re.sub(r'\W+','-',tok)[:42])
req('RuntimeClassicConfigurationAuthority.PlayerDataSaveIntervalSeconds' in timing,'timing-player-projection')
req('RuntimeClassicConfigurationAuthority.BufferDataSaveIntervalSeconds' in timing,'timing-buffer-projection')
req('RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference' in pvp,'pvp-projection')
for tok in ['RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay','RuntimeClassicConfigurationAuthority.EvilPointCycleMinutes','RuntimeClassicConfigurationAuthority.EvilPointsForgivenPerCycle']:
    req(tok in alignment,'alignment-'+tok.split('.')[-1])
req('Reforged intentionally does not create or parse that legacy file' in worldrules,'worldrules-no-legacy-parser')

# Registry/smoke preserve unknown gates.
for tok in ['RetainedNativePromptCount = 44','ActiveExactDefaultProjectionCount = 6','native.setup-source','reforged.no-legacy-ini','projection.player-save-60','projection.buffer-save-300','projection.pvp-difference-100','projection.ep-forgive-day-18','projection.ep-cycle-60','projection.ep-per-cycle-1','remaining.range-contracts','remaining.mutability-restart','remaining.owner-effect-map','reforged.generic-world-rule-contract','expected-eight-proven-world-rules-rows','expected-three-open-world-rules-rows']:
    req(tok in registry,'registry-'+re.sub(r'\W+','-',tok)[:40])
for tok in ['classic-world-rules-evidence-registry-valid','reforged-does-not-use-wccmmud-ini','native-44-prompt-catalog-retained','player-save-default-60-projected','buffer-save-default-300-projected','pvp-level-difference-default-100-retained','evil-forgive-day-default-18-projected','evil-cycle-default-60-projected','evil-per-cycle-default-1-projected','exact-ranges-remain-unknown','exact-mutability-remains-unknown','remaining-owner-effect-map-unknown','generic-reforged-rule-contract-not-native-evidence']:
    req(tok in smoke,'smoke-'+tok)

# CLI/audit current ledger.
req('--smoke-pass42-classic-world-rules-configuration-boundary' in program,'pass42-cli')
req('RuntimePass42ClassicWorldRulesConfigurationBoundarySmokeReport.Run()' in program,'pass42-run')
req(program.count('--smoke-pass42-classic-world-rules-configuration-boundary')==1,'pass42-cli-single')
req('majormud_parity_assertions_0313.json' in audit,'audit-current-0313')
req('majormud_parity_assertions_0312.json' not in audit,'audit-prior-retired')

# Exactly one broad assertion promotion.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior['update']=='0312' and ledger['update']=='0313','ledger-updates')
req(len(old)==200 and len(cur)==200,'ledger-total-200')
req(old['native.world-rules-configuration']['state']=='UNKNOWN','prior-worldrules-unknown')
req(cur['native.world-rules-configuration']['state']=='STRONG','current-worldrules-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('native.world-rules-configuration','UNKNOWN','STRONG')],'only-worldrules-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,83,20),'ledger-counts-97-83-20',str(counts))
for aid in ['character.death-xp-loss-exact','native.law-bounty-guards','commands.native-player-command-table','native.save-coordination','native.world-reset-cleanup']:
    req(cur[aid]['state']=='UNKNOWN','neighbor-remains-unknown-'+aid)

# Scorecard.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,83,20,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict')
req(abs(score['EvidenceBackedPercent']-90.0)<0.01,'score-evidence')
tc=next(c for c in score['Categories'] if c['Category']=='Timers / Resets / World Rules')
req((tc['Pass'],tc['Strong'],tc['Unknown'],tc['Total'])==(6,7,2,15),'worldrules-category-counts',str(tc))
req(abs(tc['EvidenceBackedPercent']-86.7)<0.01,'worldrules-category-evidence')

# Remaining systems registry.
req(remain.get('updatedByPass')=='Pass 42 / Update 0313','remaining-update-0313')
ids={s['id']:s for s in remain['systems']}; wr=ids['world-rules-configuration']
req(wr.get('status')=='implemented-strong-classic-configuration-boundary','remaining-status')
req(wr.get('evidenceClassification')=='strongly-correlated','remaining-evidence')
b=wr.get('classicConfiguration0313',{})
req(b.get('state')=='STRONG','remaining-boundary-strong')
req(b.get('reforgedHasNativeIni') is False,'remaining-no-ini')
req(b.get('retainedPromptCount')==44,'remaining-44-prompts')
req(b.get('activeExactDefaultProjectionCount')==6,'remaining-six-projections')
req(len(b.get('remainingUnknown',[]))==3,'remaining-three-open')

# Documentation.
for token in ['UNKNOWN → STRONG','44 exact setup prompts/defaults','60 seconds','300 seconds','Maximum PvP level difference','18','60 minutes','No `WCCMMUD.INI` file was added','97 PASS / 83 STRONG / 20 UNKNOWN = 200','90.0%']:
    req(token.lower() in doc.lower() or token.lower() in evdoc.lower(),'doc-'+re.sub(r'\W+','-',token)[:38])
req('No unproven configuration effect was activated' in doc,'doc-no-unproven-effect')

# Preserve Pass 40/41 critical boundaries.
appearance=text('Apps/Source/Reforged.Server/RuntimeClassicCharacterAppearance.cs'); live=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs'); failure=text('Apps/Source/Reforged.Server/RuntimeClassicGameplayFailureEvidenceRegistry.cs')
req('ShadowLookPayload = "A shadowy figure!"' in appearance,'pass40-shadow-preserved')
req('TryResolveEquippedShadowLookOverride' in live,'pass40-shadow-consumer-preserved')
req('RejectedGameplayDecisionCode = "classic-gameplay-action-no-op"' in failure,'pass41-noop-preserved')

# Protected files remain exact and no native INI is introduced.
protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'76d6dab9d3c1384bdbe2d6bd7c7c9464ea3c99130221cbbdf1ec36bcd7d86731',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected in protected.items(): req(sha(rel)==expected,'protected-hash-'+Path(rel).name)
ini=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini,'no-wccmmud-ini',str(ini))

report={'schema':'majormud.pass42-classic-world-rules-configuration-boundary-validation.v1','update':'0313','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_world_rules_configuration_boundary_pass42_static_validation_0313.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
