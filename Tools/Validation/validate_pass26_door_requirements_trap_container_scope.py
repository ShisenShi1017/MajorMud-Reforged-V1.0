from pathlib import Path
import json,re,sys
root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''): checks.append({'name':name,'pass':bool(ok),'detail':detail})
def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))

completion=text('Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs')
living=text('Apps/Source/Reforged.Server/RuntimeLivingWorld.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0297.json')
evidence=js('Librarium/Parity/majormud_door_requirements_container_scope_evidence_0297.json')
scope=js('Librarium/Parity/majormud_ownership_scope_0297.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0297.json')

# Boolean Key strength-alternative decoder correction.
check('gate-strength-alternative-bool', 'public bool StrengthAlternative { get; init; }' in completion)
check('evidence-strength-alternative-bool', re.search(r'RuntimeClassicExitActionEvidence\([\s\S]{0,500}bool StrengthAlternative\)',completion) is not None)
check('strength-alternative-readbool', 'StrengthAlternative = ReadBool(prerequisites,"strength_alternative")' in completion)
check('strength-alternative-not-readnullableint', not re.search(r'ReadNullableInt\([^\n]*strength_alternative',completion))
check('no-strength-alternative-hasvalue', 'StrengthAlternative.HasValue' not in completion)

# Explicit action ownership and requirement projection.
for name,token in [
    ('allows-picklock-helper','public bool AllowsPicklock'),
    ('allows-bash-helper','public bool AllowsBash'),
    ('requirement-helper','public int? RequirementFor(string action)'),
    ('any-requirement-helper','public bool IsAnyRequirementFor(string action)')]:
    check(name,token in completion)
check('picklock-requirement-prefers-shared', 'return PicklocksOrStrength ?? PicklocksDifficulty;' in completion)
check('bash-key-strength-alternative-uses-picklock-difficulty', 'StrengthAlternative ? PicklocksDifficulty : null' in completion)
check('picklock-uses-native-progression-picklocks', 'RuntimeClassicCharacterProgressionFormula.PicklocksValue(character)' in living)
check('bash-uses-character-strength', 'ReadCharacterSkillValue(character,"strength")' in living)
check('numeric-requirement-pre-gate', 'evidence.RequirementFor(action) is int requirement&&actionValue<requirement' in living)
check('any-requirement-branch', 'evidence.IsAnyRequirementFor(action)' in living)
check('normal-movement-not-requirement-check', 'not an automatic traversal stat check' in completion)

# Exact roll remains unknown / no invented RNG at action boundary.
start=living.find('public bool TryExecuteClassicExitAction')
end=living.find('public string ExecuteLivingWorldCommand',start)
action_method=living[start:end] if start>=0 and end>start else ''
check('action-method-found',bool(action_method))
check('action-roll-remains-gated','native success roll' in action_method and 'evidence-gated' in action_method)
check('no-random-in-action-boundary','Random(' not in action_method and 'RandomNumberGenerator' not in action_method)
check('no-door-state-mutation-before-roll', not re.search(r'\.(Open|Locked)\s*=',action_method))

# Evidence corpus counts and user correction.
corp=evidence['doorRequirementBoundary']['projectedCorpus']
expected={
 'doorCount':1648,
 'keyExitCount':166,
 'doorNumericPicklocksOrStrength':1178,
 'doorAnyPicklocksOrStrength':109,
 'doorNoProjectedPicklockOrBashAlternative':361,
 'keyNumericPicklocksAndStrength':83,
 'keyNumericPicklocksOnly':61,
 'keyAnyPicklocksOnly':1,
 'keyNoProjectedActionAlternative':21,
}
for k,v in expected.items(): check('evidence-count:'+k,corp.get(k)==v,f"expected={v} actual={corp.get(k)}")
check('evidence-strength-storage-boolean', evidence['doorRequirementBoundary']['runtimeCorrection']['strengthAlternativeStorage']=='Boolean')
check('evidence-picklocks-owner', 'PicklocksValue' in evidence['doorRequirementBoundary']['runtimeCorrection']['picklockValueOwner'])
check('evidence-bash-owner', evidence['doorRequirementBoundary']['runtimeCorrection']['bashValueOwner']=='character Strength')
check('evidence-movement-no-auto-check', 'never applies' in evidence['doorRequirementBoundary']['runtimeCorrection']['movement'])

# Trap state remains fail-closed / unknown.
trap=evidence['trapBoundary']
check('trap-count-295',trap['decodedExitTrapCount']==295)
check('trap-five-state-family',trap['stateFamily']==['Active','Inactive','Hidden','Active movement','Inactive movement'])
check('trap-para2-unknown',str(trap['perExitPara2']).startswith('UNKNOWN'))
check('trap-status-unknown',trap['status']=='UNKNOWN')
check('trap-mdb-located','data-v1.11p.mdb' in trap['publicStockDatabaseLocated'])

# Generic world-container parity shell removed; native owners retained elsewhere.
la={a['id']:a for a in ledger['assertions']}
check('generic-world-container-assertion-removed','native.world-containers' not in la)
check('scope-excludes-curated-world-container','native.world-containers' in scope['excludedCuratedAssertionIds'])
check('scope-excludes-native-world-container','world-containers' in scope['excludedNativeDependencyIds'])
systems={s['id']:s for s in registry['systems']}
check('active-registry-world-container-removed','world-containers' not in systems)
check('evidence-generic-container-false',evidence['classicContainerScope']['genericWorldContainerService'] is False)
owners=' '.join(evidence['classicContainerScope']['nativeBehaviorsRemainOwnedBy'])
for owner in ['Character/Items','Economy','Gangs','NPC/Spawn/Loot']:
    check('container-owner:'+owner,owner in owners)

# Ledger / registry current pointers and classification.
check('ledger-update-0297',ledger['update']=='0297')
check('door-ledger-strong',la['native.doors-locks-access']['state']=='STRONG')
check('door-ledger-requirements-described','Picklocks value' in la['native.doors-locks-access']['description'] and 'Strength' in la['native.doors-locks-access']['description'])
check('trap-ledger-unknown',la['native.traps']['state']=='UNKNOWN')
check('registry-pass26-update',registry.get('updatedByPass') in {'Pass 26 / Update 0297','Pass 27 / Update 0298','Pass 30 / Update 0301','Pass 31 / Update 0302','Pass 32 / Update 0303','Pass 33 / Update 0304','Pass 34 / Update 0305','Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'})
check('registry-door-strong',systems['doors-locks-access']['classification']=='runtime-active-strong-evidence')
check('registry-pass26-key-bool',systems['doors-locks-access']['pass26NativeRequirementBoundary']['keyStrengthAlternativeStoredAsBoolean'] is True)
check('registry-pass26-success-unknown',systems['doors-locks-access']['pass26NativeRequirementBoundary']['successRoll']=='UNKNOWN')
check('registry-trap-para2-not-recovered',systems['traps']['pass26TrapStateRecovery']['perExitPara2Recovered'] is False)
check('audit-current-ledger','majormud_parity_assertions_0316.json' in audit)
check('audit-current-scope','majormud_ownership_scope_0298.json' in audit)

# Scorecard.
check('score-pass-73',score['PassCount']==73)
check('score-strong-76',score['StrongCount']==76)
check('score-unknown-28',score['UnknownCount']==28)
check('score-total-177',score['ScoredRequirementCount']==177)
check('score-strict-41.2',score['StrictPercent']==41.2)
check('score-backed-84.2',score['EvidenceBackedPercent']==84.2)
check('remaining-system-count-27',score['RemainingSystemCount']==27)

# JSON parse checks.
for rel in [
 'Librarium/Parity/majormud_door_requirements_container_scope_evidence_0297.json',
 'Librarium/Parity/majormud_ownership_scope_0297.json',
 'Librarium/Parity/majormud_parity_assertions_0297.json',
 'Librarium/RemainingSystems/remaining_systems_registry.json',
 'Reports/RuntimeVerification/majormud_parity_scorecard_0297.json']:
    try: js(rel); check('json:'+rel,True)
    except Exception as e: check('json:'+rel,False,str(e))

failed=[x for x in checks if not x['pass']]
out={'pass':not failed,'passed':len(checks)-len(failed),'failed':len(failed),'checks':checks}
outfile=root/'Reports/RuntimeVerification/door_requirements_trap_container_pass26_static_validation_0297.json'
outfile.write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(f"Pass 26 static validation: {out['passed']}/{len(checks)} PASS")
for f in failed: print('FAIL',f['name'],f['detail'])
sys.exit(1 if failed else 0)
