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

evidence=js('Librarium/Parity/majormud_classic_scheduler_boundary_evidence_0309.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0309.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0308.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0309.json')
timing=js('Librarium/Parity/majormud_timers_resets_world_rules_evidence_0290.json')
regen_ev=js('Librarium/Parity/majormud_classic_regeneration_correction_evidence_0308.json')
promo=js('Librarium/PromotionReview0088/timing_execution_promotion_review_0088.json')
clock=js('Librarium/RemainingSystems/clock_scheduling_catalog.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
authority=text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs')
regen=text('Apps/Source/Reforged.Server/RuntimeCharacterRegeneration.cs')
status=text('Apps/Source/Reforged.Server/RuntimeClassicLiveStatusEffects.cs')
combat=text('Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs')
timer_audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudTimerAudit.cs')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicSchedulerEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass38ClassicSchedulerBoundarySmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0309_CLASSIC_SCHEDULER_BOUNDARY.md')

# Retained direct/strong evidence anchors.
req(timing.get('update')=='0290','pass20-timing-evidence-present')
findings=timing['native_wccmmud_configuration']['direct_findings']
req(any('Player data? 60' in f.get('text','') for f in findings),'native-player-save-60')
req(any('Buffer data? 300' in f.get('text','') for f in findings),'native-buffer-save-300')
help_find=timing['native_help_timing']['findings']
req(any(f.get('message')==201 and 'five seconds' in f.get('behavior','') for f in help_find),'native-exit-five-seconds')
req(any(f.get('message')==205 and '20 seconds' in f.get('behavior','') for f in help_find),'native-focus-20-seconds')
req(timing['spawn_regeneration']['runtime_binding'][0].startswith('RuntimeLivingWorldService.Tick no longer materializes'), 'room-entry-respawn-not-global-heartbeat')
req(any('AdmitDueNpcRespawnsForRoom' in x for x in timing['spawn_regeneration']['runtime_binding']),'room-entry-respawn-admission')
req(regen_ev['strongCadenceEvidence']['passiveSeconds']==30,'regen-passive-30')
req(regen_ev['strongCadenceEvidence']['restSeconds']==20,'regen-rest-20')
req(regen_ev['strongCadenceEvidence']['meditateSeconds']==10,'regen-meditate-10')
req(any(x['id']=='timing.model.host_driven_scheduling' and x['new_state']=='Corroborated' for x in promo['corroborated_promotions']),'host-driven-scheduling-corroborated')

# Structured Pass38 evidence is conservative and owner-scoped.
req(evidence['schema']=='majormud.classic-scheduler-boundary.v1','evidence-schema')
req(evidence['update']=='0309' and evidence['pass']==38,'evidence-pass38')
req(evidence['classification']=='STRONG_OWNER_SCOPED_CLASSIC_SCHEDULER_BOUNDARY','evidence-classification')
proven={(x['owner'],x['seconds']) for x in evidence['provenOwnerCadences']}
for pair in [('physical combat',5),('spell/status duration and periodic processing',3),('player data autosave',60),('native Buffer data setup interval',300),('EXIT/QUIT delay',5),('REST/MEDITATE focus activation',20)]: req(pair in proven,'proven-owner-'+re.sub(r'\W+','-',pair[0])[:28])
strong={(x['owner'],x.get('seconds')) for x in evidence['strongOwnerCadences'] if 'seconds' in x}
for pair in [('passive player regeneration',30),('REST HP regeneration',20),('MEDITATE mana regeneration',10)]: req(pair in strong,'strong-owner-'+re.sub(r'\W+','-',pair[0])[:28])
req(any(x['owner']=='NPC/lair regeneration admission' and x['state']=='STRONG' for x in evidence['strongOwnerCadences']),'strong-room-entry-spawn')
review=evidence['userProvidedPhaseDocumentReview']
req(review['role']=='hypothesis_only','user-document-hypothesis-only')
req(any('universal 10-second' in x for x in review['unsupportedOrUnprovenClaims']),'universal-ten-second-not-promoted')
req(len(evidence['stillUnknown'])>=7,'scheduler-open-boundaries-preserved')
req(evidence['parityPromotion']=={'assertion':'native.clock-scheduling','from':'UNKNOWN','to':'STRONG','reason':evidence['parityPromotion']['reason']},'promotion-shape')

# Runtime constants centralized, no behavioral cadence drift.
for token in ['CombatRoundSeconds = 5','SpellSystemRoundSeconds = 3','PassiveRegenerationSeconds = 30','RestRegenerationSeconds = 20','MeditateRegenerationSeconds = 10','GracefulExitDelaySeconds = 5','FocusedRegenerationActivationSeconds = 20']:
    req(token in authority,'timing-authority-'+token.split()[0].lower())
req('TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.PassiveRegenerationSeconds)' in regen,'regen-passive-centralized')
req('TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.RestRegenerationSeconds)' in regen,'regen-rest-centralized')
req('TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.MeditateRegenerationSeconds)' in regen,'regen-meditate-centralized')
req('CombatRoundSeconds = RuntimeClassicTimingAuthority.CombatRoundSeconds' in status,'status-combat-centralized')
req('SpellRoundSeconds = RuntimeClassicTimingAuthority.SpellSystemRoundSeconds' in status,'status-spell-centralized')
req('ClassicCombatRoundMilliseconds = RuntimeClassicTimingAuthority.CombatRoundSeconds * 1000' in combat,'combat-round-centralized')
req('no evidence-backed single universal 10-second Classic gameplay phase' in timer_audit,'timer-audit-rejects-universal-ten-second')

# Runtime evidence registry keeps exact open boundaries gated.
for token in ['Rows.Count != 17','expected-six-proven-scheduler-rows','expected-four-strong-scheduler-rows','expected-one-corroborated-scheduler-row','expected-one-unsupported-scheduler-hypothesis','expected-five-unknown-scheduler-rows']:
    req(token in registry,'registry-'+re.sub(r'\W+','-',token)[:28])
for token in ['UniversalTenSecondGameplayPhasePromoted = false','ExactSameCycleOrderingResolved = false','ExactHostPhaseResolved = false','ExactBackgroundCadenceResolved = false','ExactNpcPlayerFirstOrderingResolved = false','ExactSpawnNextTickActionResolved = false']:
    req(token in registry,'registry-gate-'+token.split()[0])
req('RuntimeClassicSchedulerEvidenceRegistry.Validate().Count == 0' in smoke,'smoke-registry-valid')
for token in ['classic-combat-round-5s','classic-spell-system-round-3s','classic-player-save-60s','classic-buffer-save-300s','classic-exit-delay-5s','classic-focus-activation-20s','classic-passive-regen-30s','classic-rest-regen-20s','classic-meditate-regen-10s','no-universal-ten-second-clock','same-cycle-order-remains-gated']:
    req(token in smoke,'smoke-'+token)

# CLI and current audit ledger.
req('--smoke-pass38-classic-scheduler-boundary' in program,'pass38-smoke-cli')
req('RuntimePass38ClassicSchedulerBoundarySmokeReport.Run()' in program,'pass38-smoke-run')
req(program.count('--smoke-pass38-classic-scheduler-boundary')==1,'pass38-smoke-cli-single')
req('majormud_parity_assertions_0316.json' in audit,'runtime-audit-current-ledger')
req('majormud_parity_assertions_0308.json' not in audit,'runtime-audit-old-ledger-retired')

# Only target state changes 0308 -> 0309.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior['update']=='0308','prior-ledger-0308')
req(ledger['update']=='0309','ledger-update-0309')
req(len(old)==200 and len(cur)==200,'ledger-total-stable-200')
req(old['native.clock-scheduling']['state']=='UNKNOWN','prior-clock-unknown')
req(cur['native.clock-scheduling']['state']=='STRONG','current-clock-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('native.clock-scheduling','UNKNOWN','STRONG')],'only-one-state-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,79,24),'ledger-counts-97-79-24',str(counts))
req(cur['combat.round-five-seconds']['state']=='PASS','combat-round-stays-pass')
req(cur['spells.spell-round-three-seconds']['state']=='PASS','spell-round-stays-pass')
req(cur['native.world-reset-cleanup']['state']=='UNKNOWN','world-reset-stays-unknown')
req(cur['npc.native-ai-semantics']['state']=='UNKNOWN','npc-ai-stays-unknown')

# Scorecard exact.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,79,24,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict-48.5')
req(abs(score['EvidenceBackedPercent']-88.0)<0.01,'score-evidence-88.0')
cat={c['Category']:c for c in score['Categories']}
req((cat['Timers / Resets / World Rules']['Pass'],cat['Timers / Resets / World Rules']['Strong'],cat['Timers / Resets / World Rules']['Unknown'],cat['Timers / Resets / World Rules']['Total'])==(6,6,3,15),'timer-category-counts')
req(abs(cat['Timers / Resets / World Rules']['EvidenceBackedPercent']-80.0)<0.01,'timer-category-evidence-80.0')

# Remaining-system clock boundary updated but unresolved cross-order remains.
req(clock['updatedByPass']=='Pass 38 / Update 0309','clock-catalog-update')
req(clock['status']=='implemented-strong-owner-scoped-classic-boundary','clock-catalog-status')
cb=clock['classicBoundary0309']
req(cb['provenSeconds']=={'combat':5,'spellSystem':3,'playerSave':60,'bufferSavePrompt':300,'exitDelay':5,'focusActivation':20},'clock-proven-constants')
req(cb['strongSeconds']=={'passiveRegeneration':30,'restRegeneration':20,'meditateRegeneration':10},'clock-strong-constants')
req(cb['universalTenSecondGameplayPhase']=='NOT_PROMOTED','clock-no-universal-ten-second')
req(cb['exactSameCycleOrdering']=='UNKNOWN' and cb['exactHostPhase']=='UNKNOWN','clock-phase-order-open')
req(remain['updatedByPass'] in {'Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'remaining-registry-update')
systems={s['id']:s for s in remain['systems']}
req(systems['clock-scheduling']['status']=='implemented-strong-owner-scoped-classic-boundary','remaining-clock-status')

# Documentation confidence boundary.
for token in ['UNKNOWN -> STRONG','5s combat','3s spell/system','30/20/10s','universal 10-second','97 PASS / 79 STRONG / 24 UNKNOWN = 200','simultaneous cross-owner event order']:
    req(token in doc,'doc-token-'+re.sub(r'\W+','-',token)[:28])

# Protected Borin / Custom1 / WPF sources stay byte-identical.
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
app=text('Apps/Source/Reforged.App/App.xaml.cs'); main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

ini=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini,'no-wccmmud-ini-file',str(ini))

report={'schema':'majormud.pass38-classic-scheduler-boundary-validation.v1','update':'0309','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_scheduler_boundary_pass38_static_validation_0309.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
