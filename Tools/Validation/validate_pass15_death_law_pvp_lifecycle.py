#!/usr/bin/env python3
import json, pathlib, re, sys
root=pathlib.Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name):
    checks.append(name)
    if not cond: failures.append(name)
def text(rel): return (root/rel).read_text(encoding='utf-8')

authority=text('Apps/Source/Reforged.Server/RuntimeClassicDeathLawPvpAuthority.cs')
alignment=text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')
pvp=text('Apps/Source/Reforged.Server/RuntimeLivePvp.cs')
commands=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
network=text('Apps/Source/Reforged.Server/ServerNetworkHost.cs')
lifecycle=text('Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs')
regen=text('Apps/Source/Reforged.Server/RuntimeCharacterRegeneration.cs')
sysop=text('Apps/Source/Reforged.Server/RuntimeWccSysopCommands.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
evidence=json.loads(text('Librarium/Parity/majormud_death_law_pvp_lifecycle_evidence_0285.json'))
ledger=json.loads(text('Librarium/Parity/majormud_parity_assertions_0285.json'))
score=json.loads(text('Reports/RuntimeVerification/majormud_parity_scorecard_0285.json'))
support=text('Librarium/Evidence/native_support_catalog_0020.json')
messages=text('Librarium/Evidence/native_message_catalog_correlation_0022.json')
host=json.loads(text('Librarium/Evidence/native_host_service_ownership_0004.json'))

timing=text('Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs')
req('GracefulExitDelaySeconds = 5' in timing and 'GracefulExitDelaySeconds = RuntimeClassicTimingAuthority.GracefulExitDelaySeconds' in authority,'exit-delay-5s-authority')
req('Task.Delay(TimeSpan.FromSeconds(RuntimeClassicDeathLawPvpAuthority.GracefulExitDelaySeconds)' in network,'exit-delay-live-network')
req('_characters.SaveSession(session)' in network,'exit-final-session-save-connected')
req(('RestCadence = TimeSpan.FromSeconds(20)' in regen or 'RestCadence = TimeSpan.FromSeconds(RuntimeClassicTimingAuthority.RestRegenerationSeconds)' in regen) and 'FocusActivationDelay = RuntimeClassicTimingAuthority.FocusedRegenerationActivationDelay' in regen,'rest-20s-live-authority')
req('NotifyCharacterAction' in regen and '_focusedRegeneration.TryRemove(actor.Id' in regen,'rest-action-interrupt-live')
req('LawfulAttackPenaltyMultiplier = 3' in authority,'lawful-x3-authority')
req('contract.LawfulAttackPenaltyMultiplier' in alignment,'lawful-x3-alignment-wired')
for term in ['GuardsAttackUntilDowned','GuardsAttackUntilDeath','DivineInterventionEligible','AttackInitiationLocked']:
    req(term in authority,f'law-consequence-{term}')
req('BountyHuntersAttack' not in authority,'no-speculative-classic-bounty-hunter-consequence')
req('\"VILLAIN\" or \"VILLIAN\" => RuntimeClassicLawConsequence.GuardsAttackUntilDeath | RuntimeClassicLawConsequence.DivineInterventionEligible' in authority,'villain-native-help-contract')
req('FiendAttackLockoutEvilPoints = 300' in authority,'fiend-lockout-300')
req('MaximumLevelDifference = 0' in pvp,'pvp-level-range-persisted')
req('IsLevelRangeAllowed(attacker.Level, defender.Level, pvpConfiguration.MaximumLevelDifference)' in pvp,'pvp-level-range-enforced')
req('SetMaximumLevelDifference' in pvp and 'SYSOP PVP RANGE' in sysop,'pvp-level-range-sysop-config')
req('PracticeArenaRoomName = "Arena Practice Room"' in authority,'practice-arena-decoded-name-authority')
req('CaptureClassicPracticeArenaEntry(session, previousRoom)' in commands,'practice-arena-entry-captured-on-movement')
req('practiceArena' in pvp and 'if (!practiceArena)' in pvp,'practice-arena-no-attack-ep')
req('CompletePracticeArenaPlayerDefeat' in pvp and 'classic.practice-arena.defeat' in pvp,'practice-arena-death-path')
req('RequirePracticeArenaEntryHitPoints' in authority and 'fails closed' in authority,'practice-arena-missing-entry-fail-closed')
req('HallsOfTheDeadRoom = 2189' in authority and 'HallsOfTheDeadMap = 1' in authority,'practice-arena-halls-destination')
req('EXCLUDED_WORLDGROUP' in lifecycle and 'ReconnectGraceAndSocketTakeover' in lifecycle,'worldgroup-reconnect-excluded')
# retained native evidence
for phrase in ['Maximum level difference for PVP? 100','Keep some experience after final death? YES','Max EPs to forgive per day? 18','Minutes per Evil Point Cycle? 60','EPs to forgive per cycle? 1','Enable the choice of Lawful? YES']:
    req(phrase in support,f'native-support:{phrase}')
for phrase in ['Practice Arena colliseum','no fear of evil points or loss of life','Halls Of The Dead','Attacking a lawful player will give thrice the evil points']:
    req(phrase in messages,f'native-help:{phrase[:32]}')
# host ownership
host_domains={x.get('domain'):x for x in host.get('domains',[])}
req('host_session_user_state' in host_domains,'host-session-domain-proven')
req(host_domains.get('host_session_user_state',{}).get('direct_callsite_count',0)>0,'host-session-callsites-proven')
# exact arena room count from direct MDB-derived object corpus
arena=[]
for f in (root/'Librarium/Objects/rooms/mmexplorer_room_records_0140.parts').glob('*.json'):
    d=json.loads(f.read_text(encoding='utf-8')); arr=d if isinstance(d,list) else d.get('records',[])
    arena += [r.get('id') for r in arr if r.get('name','').lower()=='arena practice room']
req(len(set(arena))==11,'decoded-practice-arena-room-count-11')
# no guessed normal death XP arithmetic introduced in new authority/PvP/law path
req('normal_death_xp_loss' in json.dumps(evidence) and any(f.get('id')=='normal_death_xp_loss' and f.get('state')=='UNKNOWN' for f in evidence.get('facts',[])),'normal-death-xp-remains-unknown')
req('character.Experience -' not in authority, 'no-guessed-normal-death-xp-subtraction')
req('character.Experience *' not in authority, 'no-guessed-normal-death-xp-multiplication')
req('character.Experience /' not in authority, 'no-guessed-normal-death-xp-division')
# ledger/score reconciliation
req(ledger.get('update')=='0285','ledger-update-0285')
req(len(ledger.get('assertions',[]))==176,'ledger-reconciled-176')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-points-current-ledger')
req(score.get('PassCount')==71,'score-pass-71')
req(score.get('StrongCount')==58,'score-strong-58')
req(score.get('UnknownCount')==47,'score-unknown-47')
req(score.get('ScoredRequirementCount')==176,'score-total-176')
req(abs(score.get('StrictPercent',0)-40.3)<0.01,'score-strict-40.3')
req(abs(score.get('EvidenceBackedPercent',0)-73.3)<0.01,'score-evidence-73.3')
byid={a['id']:a for a in ledger['assertions']}
for aid,state in [('character.graceful-exit-save','PASS'),('character.rest-activation-interrupt-contract','PASS'),('law.reputation-enforcement-contract','STRONG'),('pvp.realm-level-range-configuration','STRONG'),('pvp.practice-arena-death-exception','STRONG')]:
    req(aid in byid and byid[aid]['state']==state,f'ledger-new:{aid}:{state}')
for aid in ['character.death-xp-loss-exact','character.native-lifecycle-boundary','native.law-bounty-guards','native.pvp-policy','native.rest-inn-recovery-policy']:
    req(aid in byid and byid[aid]['state']=='UNKNOWN',f'residual-unknown-preserved:{aid}')

report={'schema':'majormud.pass15-death-law-pvp-lifecycle-validation.v1','update':'0285','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/death_law_pvp_lifecycle_pass15_static_validation_0285.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
sys.exit(1 if failures else 0)
