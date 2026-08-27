#!/usr/bin/env python3
import json, sys
from collections import Counter
from pathlib import Path

root=Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def check(ok,name,detail=''):
    (checks if ok else failures).append(name if not detail else f'{name}: {detail}')
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))

ledger=js('Librarium/Parity/majormud_parity_assertions_0291.json')
counts=Counter(a['state'] for a in ledger['assertions'])
check(len(ledger['assertions'])==179,'ledger-total')
check(counts==Counter({'PASS':73,'STRONG':70,'UNKNOWN':36}),'ledger-score',str(counts))
ids={a['id'] for a in ledger['assertions']}
for removed in ['native.administrative-policy','native.security-permissions','native.logging-diagnostics','native.mail-offline-messaging','native.account-session-ownership','native.weather-day-night','native.startup-shutdown']:
    check(removed not in ids,'scope-removed:'+removed)
org=next(a for a in ledger['assertions'] if a['id']=='native.organizations.classic-model')
check(org['state']=='PASS','gang-model-pass')
check('gang' in org['description'].lower(),'gang-model-description')

scope=js('Librarium/Parity/majormud_ownership_scope_0291.json')
for removed in ['administrative-policy','security-permissions','logging-diagnostics','mail-offline-messaging','account-session-ownership','weather-day-night','startup-shutdown']:
    check(removed in scope['excludedNativeDependencyIds'],'dependency-excluded:'+removed)
check('worldgroup-integration' in scope['excludedNativeDependencyIds'],'worldgroup-boundary-excluded')

registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
regids={x['id'] for x in registry['systems']}
check(len(registry['systems'])==29,'registry-scoped-count')
for removed in ['administrative-policy','security-permissions','logging-diagnostics','mail-offline-messaging','account-session-ownership','weather-day-night','startup-shutdown']:
    check(removed not in regids,'registry-removed:'+removed)

live=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
check('var toggled = !IsClassicReceiveEnabled(character);' in live,'set-receive-bare-toggle')
check('var enabled = !CommunicationFlagEnabled(character, conditionKey);' in live,'global-channel-bare-toggle')
check('ConfigureClassicTelepathReceive' in live and 'majormud-telepath-toggle' in live,'telepath-bare-toggle')
check('raw.Length != 0' in live and 'explicit ON/OFF polarity remains evidence-gated' in live,'telepath-explicit-polarity-gated')

cmd=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
check('TryExecuteDecodedWorldCommand(session, command, out var decodedRoomCommand)' in cmd,'decoded-room-command-precedes-generic-route')
check('topten | top <n> <class> | topten gangs | top <n> gangs' in cmd,'top-usage-surface')

gang=text('Apps/Source/Reforged.Server/RuntimeClassicGangGameplay.cs')
check('classFilter = args[1]' in gang,'class-filtered-top-recognized')
check('exact ranking order and output format are not yet recovered' in gang,'top-formula-gated')

world=text('Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs')
check('BUY HEALING is available here' in world,'buy-healing-recognized')
check('exact native healer price/debit formula is not yet recovered' in world,'healer-price-gated')

completion=text('Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs')
check('"ticket_item"' in completion,'ticket-gate-supported')
check('case "ticket_item"' in completion,'ticket-gate-evaluated')
check('ticket consumption is native-unknown' in completion.lower() or 'do not invent ticket destruction' in completion.lower() or 'consumption remains native-unknown' in completion.lower(),'ticket-consumption-not-invented')

audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
check('majormud_parity_assertions_0291.json' in audit,'runtime-audit-ledger-pointer')
check('majormud_ownership_scope_0291.json' in audit,'runtime-audit-scope-pointer')

ev=js('Librarium/Parity/majormud_settings_healers_access_rankings_evidence_0291.json')
check(ev['ticketAccessEvidence']['requiredItemId']==924,'ticket-924-evidence')
check(ev['ticketAccessEvidence']['gateKind']=='ticket_item','ticket-gate-evidence')
check(any(x.get('messageNumber')==207 for x in ev['nativeHelpEvidence']),'healer-help-207')
check(any(x.get('messageNumber')==261 for x in ev['nativeHelpEvidence']),'ganghouse-help-261')
check(any(x.get('messageNumber')==135 for x in ev['nativeHelpEvidence']),'arena-help-135')

score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0291.json')
check((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(73,70,36,179),'scorecard-counts')
check(score['StrictPercent']==40.8,'scorecard-strict')
check(score['EvidenceBackedPercent']==79.9,'scorecard-backed')

# Source evidence existence checks.
native=text('Librarium/Evidence/native_message_catalog_correlation_0022.json')
for phrase,name in [
    ('Will display the top ten adventurers in the game.','native-topten-help'),
    ('SET GOSSIP   (to toggle it on/off)','native-gossip-toggle-help'),
    ('SET AUCTION  (to toggle it on/off)','native-auction-toggle-help'),
    ('Type buy healing in the appropriate room to heal all your wounds for a price.','native-buy-healing-help'),
    ('Every sundown','native-ganghouse-sundown-help'),
    ('Silvermere bank account','native-ganghouse-bank-help'),
    ('practice arena','native-practice-arena-help')]:
    check(phrase.lower() in native.lower(),name)

ticket=text('Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0183/exits_decoded_semantic.parts/exits_decoded_semantic.part0026.json')
check('"id": "exit.map2.2552.u"' in ticket and '"required_item_id": 924' in ticket,'native-ticket-exit-record')
healer=text('Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142/textblocks_decoded.json')
check('buy minor healing:monsters:price 50 318:cast 219:adddelay 5' in healer,'decoded-healer-price-cast-delay')

# Structural JSON check for all Pass21-authored JSON files.
for rel in ['Librarium/Parity/majormud_parity_assertions_0291.json','Librarium/Parity/majormud_ownership_scope_0291.json','Librarium/Parity/majormud_settings_healers_access_rankings_evidence_0291.json','Librarium/RemainingSystems/remaining_systems_registry.json','Reports/RuntimeVerification/majormud_parity_scorecard_0291.json']:
    try: js(rel); check(True,'json:'+rel)
    except Exception as e: check(False,'json:'+rel,str(e))

report={'Passed':not failures,'CheckCount':len(checks)+len(failures),'PassedCount':len(checks),'FailureCount':len(failures),'Checks':checks,'Failures':failures}
out=root/'Reports/RuntimeVerification/settings_healers_access_rankings_pass21_static_validation_0291.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
sys.exit(0 if not failures else 1)
