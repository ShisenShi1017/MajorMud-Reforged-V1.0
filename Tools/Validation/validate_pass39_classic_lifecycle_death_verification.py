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

evidence=js('Librarium/Parity/majormud_classic_lifecycle_death_verification_evidence_0310.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0310.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0309.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0310.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicLifecycleDeathEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass39ClassicLifecycleDeathVerificationSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
combat=text('Apps/Source/Reforged.Server/ClassicCombatResolver.cs')
death=text('Apps/Source/Reforged.Server/RuntimeCharacterDeath.cs')
party=text('Apps/Source/Reforged.Server/RuntimeClassicPartyGameplay.cs')
commands=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
host=text('Apps/Source/Reforged.Server/ServerNetworkHost.cs')
doc=text('Docs/CODE_UPDATE_0310_CLASSIC_LIFECYCLE_DEATH_VERIFICATION.md')

# Evidence document is explicitly hypothesis-only and conservative.
req(evidence['schema']=='majormud.classic-lifecycle-death-verification.v1','evidence-schema')
req(evidence['pass']==39 and evidence['update']=='0310','evidence-pass-update')
req(evidence['noGameplayGuessing'] is True,'evidence-no-gameplay-guessing')
for key in ['userProvidedCombatPage','userProvidedLinkdeathPage','userProvidedDeathXpPage']:
    req(evidence['sourceReview'][key]['role']=='hypothesis_only','hypothesis-only-'+key)
req(evidence['combatContradiction']['nativeParityState']=='REJECTED','priority-queue-rejected')
req(evidence['deathXpBoundary']['state']=='UNKNOWN','death-xp-boundary-unknown')
req(evidence['deathXpBoundary']['assertion']=='character.death-xp-loss-exact','death-xp-assertion-id')
for claim in ['level < 10 immunity','10 percent of total experience','integer truncation rule for the death penalty','2,000,000 XP cap','exact de-level regression count','post-delevel HP/mana recomputation ordering']:
    req(claim in evidence['deathXpBoundary']['notPromoted'],'death-xp-not-promoted-'+re.sub(r'\W+','-',claim)[:30])
req('same username/IP reconnect requirement' in evidence['worldgroupBoundary']['excluded'],'worldgroup-reconnect-excluded')
req(len(evidence['stillUnknown'])>=9,'open-lifecycle-death-boundaries-retained')
req(evidence['parityPromotion']['assertion']=='character.native-lifecycle-boundary','promotion-target')
req(evidence['parityPromotion']['from']=='UNKNOWN' and evidence['parityPromotion']['to']=='STRONG','promotion-unknown-to-strong')

# Runtime evidence registry has no promoted guesses.
for token in ['Rows.Count != 21','expected-four-proven-lifecycle-rows','expected-one-strong-lifecycle-row','expected-three-unsupported-hypotheses','expected-one-worldgroup-exclusion','expected-twelve-open-lifecycle-death-rows']:
    req(token in registry,'registry-'+re.sub(r'\W+','-',token)[:34])
for token in ['PriorityQueueFormulaPromoted = false','FixedThreeMinuteLinkdeadTimerPromoted = false','SameUserIpReconnectRulePromoted = false','PlayerJsonNativeSavePromoted = false','DeathXpUnderLevelTenImmunityPromoted = false','DeathXpTenPercentPromoted = false','DeathXpTwoMillionCapPromoted = false','ExactLeaderExitSuccessionResolved = false','ExactLinkdeadGameplayResolved = false','ExactCrossOwnerExitOrderingResolved = false']:
    req(token in registry,'registry-gate-'+token.split()[0])
for rid in ['exit.delay-save','party.follower-explicit-exit','death.suicide-normal-death','death.final-life-reroll','combat.player-priority-queue','death.xp-ten-percent','death.xp-two-million-cap','party.leader-exit-successor']:
    req(f'new("{rid}"' in registry,'registry-row-'+rid)

# Existing PASS attack-energy scheduler contradicts the proposed ActionDelay queue.
for token in ['var available = checked(carriedEnergy + roundEnergy);','var swings = Math.Min(5, available / attackEnergyCost);','ResolveNormalWeaponAttackSchedule(0, 100).Swings == 5']:
    req(token in combat,'combat-energy-'+re.sub(r'\W+','-',token)[:32])
req('ActionDelay = WeaponSpeed' not in combat,'combat-no-actiondelay-formula')
req('HasteSkill / 10' not in combat,'combat-no-haste-priority-formula')
req('stock-attack-energy-scheduler-not-priority-queue' in smoke,'smoke-stock-energy-anchor')
req('stock-five-swing-cap-retained' in smoke,'smoke-five-swing-cap')

# Graceful exit / explicit follower cleanup anchors.
req('GracefulExitDelaySeconds' in host and 'Task.Delay(TimeSpan.FromSeconds(RuntimeClassicDeathLawPvpAuthority.GracefulExitDelaySeconds)' in host,'host-five-second-exit')
req('_characters.SaveSession(session)' in host,'host-exit-save-finalizer')
req('public void HandleExplicitClassicSessionExit(RuntimeSession session)' in party,'explicit-exit-party-hook')
req('_gameplay?.HandleExplicitClassicSessionExit(session)' in commands,'command-routes-explicit-exit-hook')
for token in ['graceful-exit-delay-remains-5s','explicit-classic-exit-party-hook-present','session-exit-routing-present','priority-queue-hypothesis-rejected','death-xp-ten-percent-remains-gated','leader-exit-succession-remains-gated','linkdead-gameplay-remains-gated','exit-cleanup-order-remains-gated']:
    req(token in smoke,'smoke-'+token)

# Exact death-XP arithmetic remains absent from the death resolver.
req('Experience' not in death,'death-resolver-does-not-guess-xp-arithmetic')
for bad in ['2000000','2_000_000','/ 10','* 0.10','0.10','Level < 10','Level <= 9']:
    req(bad not in death,'death-no-guessed-'+re.sub(r'\W+','-',bad)[:24])

# CLI and current audit ledger.
req('--smoke-pass39-classic-lifecycle-death-verification' in program,'pass39-smoke-cli')
req('RuntimePass39ClassicLifecycleDeathVerificationSmokeReport.Run()' in program,'pass39-smoke-run')
req(program.count('--smoke-pass39-classic-lifecycle-death-verification')==1,'pass39-smoke-cli-single')
req('majormud_parity_assertions_0316.json' in audit,'runtime-audit-current-ledger')
req('majormud_parity_assertions_0309.json' not in audit,'runtime-audit-prior-retired')

# Exactly one assertion promotion from Pass38 to Pass39.
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior['update']=='0309','prior-ledger-0309')
req(ledger['update']=='0310','ledger-update-0310')
req(len(old)==200 and len(cur)==200,'ledger-total-stable-200')
req(old['character.native-lifecycle-boundary']['state']=='UNKNOWN','prior-lifecycle-unknown')
req(cur['character.native-lifecycle-boundary']['state']=='STRONG','current-lifecycle-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('character.native-lifecycle-boundary','UNKNOWN','STRONG')],'only-lifecycle-state-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,80,23),'ledger-counts-97-80-23',str(counts))
req(cur['character.death-xp-loss-exact']['state']=='UNKNOWN','death-xp-stays-unknown')
req(cur['combat.attack-energy-scheduling']['state']=='PASS','attack-energy-stays-pass')
req(cur['combat-spells.native-boundary']['state']=='UNKNOWN','broader-combat-boundary-stays-unknown')
req(cur['native.party-coordination']['state']=='UNKNOWN','party-coordination-stays-unknown')

# Scorecard exact.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,80,23,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict-48.5')
req(abs(score['EvidenceBackedPercent']-88.5)<0.01,'score-evidence-88.5')
cat={c['Category']:c for c in score['Categories']}
cc=cat['Character & Status']
req((cc['Pass'],cc['Strong'],cc['Unknown'],cc['Total'])==(24,18,4,46),'character-category-counts')
req(abs(cc['EvidenceBackedPercent']-91.3)<0.01,'character-category-evidence-91.3')
req(remain.get('updatedByPass') in {'Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'remaining-registry-update')

# Documentation states the confidence boundary, not the screenshot as truth.
for token in ['UNKNOWN -> STRONG','ActionDelay = WeaponSpeed','rejected','character.death-xp-loss-exact','remains **UNKNOWN**','97 PASS / 80 STRONG / 23 UNKNOWN = 200','Worldgroup host plumbing']:
    req(token in doc,'doc-'+re.sub(r'\W+','-',token)[:30])

# Protected Borin / Custom1 / WPF sources remain byte-identical.
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

report={'schema':'majormud.pass39-classic-lifecycle-death-verification-validation.v1','update':'0310','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_lifecycle_death_verification_pass39_static_validation_0310.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
