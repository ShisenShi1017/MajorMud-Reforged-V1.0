from pathlib import Path
import json,re,sys
root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''): checks.append({'name':name,'pass':bool(ok),'detail':detail})
def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))

living=text('Apps/Source/Reforged.Server/RuntimeLivingWorld.cs')
traps=text('Apps/Source/Reforged.Server/RuntimeClassicExitTraps.cs')
generic=text('Apps/Source/Reforged.Server/RuntimeTraps.cs')
field=text('Apps/Source/Reforged.Server/RuntimeClassicRoomExitNativeFieldContract.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
evidence=js('Librarium/Parity/majormud_trap_state_reset_boundary_evidence_0298.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0298.json')
scope=js('Librarium/Parity/majormud_ownership_scope_0298.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0298.json')
abilities=js('Librarium/Objects/abilities/ability_records.json')

# Native mixed-width RoomRec and trap state contract.
check('field-contract-roomtype-short','public enum RoomExitType : short' in field)
check('field-contract-trapstate-short','public enum TrapState : short' in field)
for name,val in [('Active',0),('Inactive',1),('Hidden',2),('ActiveMovement',3),('InactiveMovement',4)]:
    check('trap-state-'+name, re.search(rf'\b{name}\s*=\s*{val}\b',field) is not None)
check('trap-state-validation','value is >= 0 and <= 4' in field)
contract=evidence['trapRecordContract']
check('evidence-room-exit-slots-10',contract['roomExitSlots']==10)
check('evidence-para2-signed16',contract['fieldWidths']['Para2']=='signed 16-bit')
check('evidence-type9-damage',contract['trapTypes']['9']=='damage trap')
check('evidence-type24-spell',contract['trapTypes']['24']=='spell trap')
check('evidence-five-states',contract['para2StateEncoding']=={'0':'Active','1':'Inactive','2':'Hidden','3':'Active movement','4':'Inactive movement'})

# Disarm ability identity and command admission.
records=abilities.get('records',[])
by_legacy={str(r.get('legacy_id')):r for r in records}
check('ability40-findtraps',by_legacy.get('40',{}).get('name')=='FindTraps')
check('ability41-disarmtraps',by_legacy.get('41',{}).get('name')=='DisarmTraps')
check('ability179-findtrapsvalue',by_legacy.get('179',{}).get('name')=='FindTrapsValue')
check('disarm-helper-present','HasClassicDisarmTrapsAbility' in living)
check('disarm-recognizes-ability41','abilityId==41' in living)
check('disarm-recognizes-traps-name','key.Equals("traps"' in living)
check('disarm-skill-gate-message','DISARM TRAP requires the Traps skill.' in living)
start=living.find('if(action.Equals("disarm"')
end=living.find('if(!evidence.GateKind.Equals("door"',start)
block=living[start:end] if start>=0 and end>start else ''
check('disarm-block-found',bool(block))
check('disarm-roll-remains-gated','native SEARCH discovery' in block and 'state transition remain evidence-gated' in block)
check('no-random-in-disarm-boundary','Random.' not in block and 'Random(' not in block)
check('no-trap-state-mutation-in-disarm',not re.search(r'NativeActivationState\s*=|Armed\s*=|Revealed\s*=',block))

# Exact per-exit state remains fail closed.
check('classic-trap-owner-file', 'RuntimeClassicExitTrapCatalog' in traps)
check('para2-future-projection-key','native_trap_state' in traps)
check('no-infer-active-comment','never infer the state from the display qualifier' in traps)
check('wccmp002-boundary-comment','WCCMP002 RoomRec Para2' in traps)
apply_start=traps.find('public string ApplyExitTrap')
apply_end=traps.find('private string ApplyDecodedDamageExitTrap',apply_start)
apply=traps[apply_start:apply_end]
check('apply-exit-trap-failclosed','if (!trap.NativeActivationStateProjected) return string.Empty;' in apply)
check('projected-state-still-failclosed','return string.Empty;' in apply)

# Generic extension trap service must not count as Classic authority.
check('generic-trap-extension-comment','Generic Reforged trap service for Custom/AI/extensible content' in generic)
check('generic-not-classic-comment','not the Classic' in generic and 'RuntimeClassicExitTrapCatalog' in generic)
systems={s['id']:s for s in registry['systems']}
tr=systems['traps']
check('registry-current-pass',registry.get('updatedByPass') in {'Pass 27 / Update 0298','Pass 30 / Update 0301','Pass 31 / Update 0302','Pass 32 / Update 0303','Pass 33 / Update 0304','Pass 34 / Update 0305','Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313'})
check('registry-classic-owner','RuntimeClassicExitTrapCatalog' in tr['runtimeOwner'])
check('registry-generic-not-classic',tr['pass27NativeTrapBoundary']['genericRuntimeTrapServiceCountsForClassic'] is False)
check('registry-no-generic-trap-timers',tr['timers']==[])
check('registry-wccmp002-required','WCCMP002' in tr['pass27NativeTrapBoundary']['requiredRecoverySource'])
check('registry-para2-not-recovered',tr['pass27NativeTrapBoundary']['perExitPara2Recovered'] is False)

# Message ownership conflict retained.
conf=evidence['messageOwnershipConflict']
check('message-conflict-unresolved',conf['status']=='UNRESOLVED')
check('message-docs-para3-para4','Para3' in conf['docsRoomsPara'] and 'Para4' in conf['docsRoomsPara'])
check('message-editor-conflict','reverse' in conf['roomEditorUi'])
check('message-no-binding-policy','do not bind' in conf['policy'])

# Correct data recovery boundary.
recover=evidence['perExitStateRecovery']
check('mme-mdb-insufficient','not sufficient' in recover['incorrectPriorBlocker'])
check('recovery-source-wccmp002','WCCMP002' in recover['correctSourceBoundary'])
check('recovery-status-unknown',recover['status']=='UNKNOWN')
check('fallback-failclosed','fail-closed' in recover['fallback'])

# Ledger/audit/score remain conservative.
la={a['id']:a for a in ledger['assertions']}
check('ledger-update-0298',ledger['update']=='0298')
check('trap-ledger-unknown',la['native.traps']['state']=='UNKNOWN')
check('trap-ledger-signed16','signed 16-bit' in la['native.traps']['description'])
check('trap-ledger-wccmp002','WCCMP002' in la['native.traps']['description'])
check('trap-ledger-skill-gate','Traps-skill admission gate' in la['native.traps']['description'])
check('audit-current-ledger','majormud_parity_assertions_0313.json' in audit)
check('audit-current-scope','majormud_ownership_scope_0298.json' in audit)
check('scope-update-0298',scope['update']=='0298')
check('score-pass-73',score['PassCount']==73)
check('score-strong-76',score['StrongCount']==76)
check('score-unknown-28',score['UnknownCount']==28)
check('score-total-177',score['ScoredRequirementCount']==177)
check('score-strict-41.2',score['StrictPercent']==41.2)
check('score-backed-84.2',score['EvidenceBackedPercent']==84.2)

# No invented reset semantics promoted.
reset=evidence['resetBoundary']
check('generic-trap-reset-not-native','not evidence for Classic' in reset['genericReforgedTrapService'])
check('classic-reset-unknown','UNKNOWN' in reset['classicTrapReset'])
check('world-reset-order-not-promoted','No generic reset action ordering is promoted' in reset['worldReset'])

# JSON parse/current artifacts.
for rel in [
 'Librarium/Parity/majormud_trap_state_reset_boundary_evidence_0298.json',
 'Librarium/Parity/majormud_ownership_scope_0298.json',
 'Librarium/Parity/majormud_parity_assertions_0298.json',
 'Librarium/RemainingSystems/remaining_systems_registry.json',
 'Reports/RuntimeVerification/majormud_parity_scorecard_0298.json']:
    try: js(rel); check('json:'+rel,True)
    except Exception as exc: check('json:'+rel,False,str(exc))

failed=[c for c in checks if not c['pass']]
out={'pass':not failed,'passed':len(checks)-len(failed),'failed':len(failed),'checks':checks}
outfile=root/'Reports/RuntimeVerification/trap_state_reset_boundary_pass27_static_validation_0298.json'
outfile.write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(f"Pass 27 static validation: {out['passed']}/{len(checks)} PASS")
for f in failed: print('FAIL',f['name'],f['detail'])
sys.exit(1 if failed else 0)
