import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]; failures=[]
def req(c,n): (checks if c else failures).append(n)
def text(rel): return (root/rel).read_text(encoding='utf-8')
def load(rel): return json.loads(text(rel))

evidence=load('Librarium/Parity/majormud_give_shops_transport_evidence_0288.json')
ledger=load('Librarium/Parity/majormud_parity_assertions_0288.json')
score=load('Reports/RuntimeVerification/majormud_parity_scorecard_0288.json')
part2=text('Apps/Source/Reforged.Server/RuntimeLiveGameplayPart2.cs')
world=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
cmd=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
part6=text('Apps/Source/Reforged.Server/RuntimeLiveGameplayPart6.cs')
worldsystems=text('Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs')
items=text('Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs')
lifecycle=text('Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs')
coverage=load('Reports/RuntimeVerification/classic_economy_service_coverage.json')
transport=load('Librarium/RemainingSystems/transport_service_catalog.json')
interactions=load('Librarium/Objects/triggers/mmexplorer_room_interaction_records_0140.json')
parity=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

req(evidence['official_help']['give']['syntax']=='GIVE ITEM PLAYER','official-give-syntax')
req('give <item> <player>' in part2,'give-native-syntax-runtime')
req('command.Arguments[^2].Equals("to"' in part2,'give-optional-to-compatibility')
req('IsClassicReceiveEnabled(freshTarget)' in part2,'give-receive-gate')
req('targetWeight + incomingWeight' in part2 and 'targetCapacity' in part2,'give-encumbrance-capacity-gate')
req('sourceEngine.Give(selected.InstanceId' in part2,'give-item-engine-transfer-restrictions')
req('sourceEngine.AdministrativeRemove(transferred.InstanceId)' in part2 and 'targetEngine.Add(transferred)' in part2,'give-two-save-domain-transaction')
req('ClassicReceiveEnabledKey' in world and 'ConfigureClassicReceive' in world,'set-receive-persisted-setting')
req('ConfigureClassicSetting' in cmd and 'set receive on|off' in cmd.lower(),'set-receive-command-route')
req('IsClassicExtensionCommand' in cmd and 'name is "trade" or "repair"' in cmd,'classic-help-hides-trade-repair')
req('"trade" when IsClassicWorld(session) => "Unknown command."' in part6,'classic-trade-isolated')
req('"repair" when IsClassicWorld(session) => "Unknown command."' in part6,'classic-repair-isolated')
req('EXCLUDED_REFORGED_EXTENSION' in lifecycle and 'TradeEscrowDisposition' in lifecycle,'trade-escrow-not-native-dependency')
req(coverage.get('ClassicRepairServiceProjected') is False,'no-classic-repair-projection')
req('no-native-repair-service-projected' in coverage.get('Checks',[]),'coverage-no-repair-check')
req('stock.TryGetValue(itemId' in worldsystems and 'DebitCopperValue' in worldsystems,'buy-stock-before-funds-path')
req(transport.get('classicOwnership')=='MajorMUD','transport-majormud-owned')

rug=next((x for x in interactions.get('records',[]) if x.get('id')=='interaction.map12.2248.e.271'),None)
req(rug is not None,'rug-interaction-present')
if rug:
 cs=rug.get('command_semantics',{})
 req(cs.get('state_effect')=='temporarily_reveals_bound_exit_via_shared_classic_door_timer','rug-temporary-effect')
 req(cs.get('reset_timing')=='shared_classic_door_timer_authority_exact_duration_not_yet_recovered','rug-shared-door-timer-family')
 req(set(rug.get('accepted_commands',[]))=={'move rug','pull rug'},'rug-command-aliases')

by={a['id']:a for a in ledger['assertions']}
expected={
 'transactions.classic-give-command-receive':'PASS',
 'transactions.classic-give-capacity-restrictions':'STRONG',
 'transactions.no-native-trade-escrow':'STRONG',
 'economy.no-native-repair-service':'STRONG',
 'transport.majormud-ownership':'STRONG',
 'native.transport-services':'UNKNOWN',
 'rooms.hidden-2248-command-activation':'STRONG',
 'rooms.hidden-2248-normal-door-timer':'STRONG',
 'economy.non-item-service-cost-order':'UNKNOWN',
}
for k,v in expected.items(): req(by.get(k,{}).get('state')==v,f'ledger:{k}:{v}')
req('native.player-transactions' not in by,'broad-fake-player-transactions-removed')
req('rooms.unknown-qualified-exits' not in by,'last-qualified-exit-unknown-removed')
req('economy.generic-service-cost-order' not in by,'repair-bundled-service-unknown-removed')
counts={s:sum(1 for a in ledger['assertions'] if a.get('state')==s) for s in ('PASS','STRONG','UNKNOWN')}
req(counts=={'PASS':71,'STRONG':68,'UNKNOWN':45},f'ledger-counts:{counts}')
req(score.get('PassCount')==71 and score.get('StrongCount')==68 and score.get('UnknownCount')==45 and score.get('ScoredRequirementCount')==184,'score-counts')
req(abs(score.get('StrictPercent',0)-38.6)<0.01 and abs(score.get('EvidenceBackedPercent',0)-75.5)<0.01,'score-percentages')
req('majormud_parity_assertions_0288.json' in parity,'runtime-parity-points-0288')

report={'schema':'majormud.pass18-give-shops-transport-validation.v1','update':'0288','status':'PASS' if not failures else 'FAIL','checks':len(checks)+len(failures),'passed':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/give_shops_transport_pass18_static_validation_0288.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2)); sys.exit(0 if not failures else 1)
