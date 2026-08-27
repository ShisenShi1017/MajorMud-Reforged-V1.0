from pathlib import Path
import json,re,sys
root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''): checks.append({'name':name,'pass':bool(ok),'detail':detail})
def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))

completion=text('Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs')
living=text('Apps/Source/Reforged.Server/RuntimeLivingWorld.cs')
combat=text('Apps/Source/Reforged.Server/RuntimeLiveCombat.cs')
commands=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
routing=text('Apps/Source/Reforged.Server/RuntimeCommandRouting.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
traps=text('Apps/Source/Reforged.Server/RuntimeClassicExitTraps.cs')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0296.json')
evidence=js('Librarium/Parity/majormud_doors_traps_room_effects_evidence_0296.json')
scope=js('Librarium/Parity/majormud_ownership_scope_0296.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0296.json')

# Traversal must not synthesize PICKLOCK/BASH checks.
check('no-auto-picklock-traversal-threshold', 'context.PicklocksValue < difficulty' not in completion)
check('no-auto-strength-traversal-threshold', 'picklocks-or-strength-failed' not in completion)
check('traversal-comments-explicit-action-boundary', 'not an automatic traversal stat check' in completion)
check('door-traversal-checks-locked', 'door.Locked?new(false,false,"door-locked"' in completion)
check('door-traversal-checks-open', '!door.Open?new(false,false,"door-closed"' in completion and 'new(true,false,"door-open"' in completion)

# Explicit native command surface.
check('picklock-registered', 'new("picklock", [], 8)' in commands)
check('disarm-registered', 'new("disarm", [], 6)' in commands)
check('picklock-help-direction', '["picklock"] = "picklock <direction>"' in commands)
check('disarm-help-native-syntax', '["disarm"] = "disarm trap <direction>"' in commands)
check('bash-help-allows-direction', '["bash"] = "bash <target|direction>"' in commands)
check('picklock-living-route', re.search(r'LivingWorldRuntime[\s\S]{0,350}"picklock"', routing) is not None)
check('disarm-living-route', re.search(r'LivingWorldRuntime[\s\S]{0,350}"disarm"', routing) is not None)
check('picklock-not-native-generic-gate', not re.search(r'NativeEvidenceGate[\s\S]{0,500}"picklock"', routing))
check('dispatcher-living-picklock-disarm', 'or "picklock" or "disarm"' in commands)
check('bash-door-first', '"bash" when TryExecuteClassicExitAction' in combat)
check('bash-combat-fallback-retained', '"bash" => BeginLiveCombat(session, command.ArgumentText, ClassicAttackType.Bash)' in combat)

# Exact formulas remain evidence gated.
check('door-action-evidence-record', 'RuntimeClassicExitActionEvidence' in completion)
check('door-action-profile-method', 'TryGetClassicExitActionEvidence' in completion)
check('picklock-skill-required', 'RuntimeClassicCharacterProgressionFormula.PicklocksValue(character)<=0' in living)
check('picklock-roll-gated', 'native success roll and exact state transition remain evidence-gated' in living)
check('disarm-roll-gated', 'native detection/disarm roll and state transition remain evidence-gated' in living)
check('no-new-door-rng', 'Random(' not in living[living.find('TryExecuteClassicExitAction'):living.find('RenderLivingWorldState')])
check('no-new-trap-rng', 'Random(' not in living[living.find('TryExecuteClassicExitAction'):living.find('RenderLivingWorldState')])

# Trap corpus remains fail closed until Para2 semantics are exact.
check('trap-native-state-projection-gate', 'NativeActivationStateProjected' in traps)
check('trap-fail-closed-comment', 'Strict Classic fail-closed rule' in traps)
check('trap-no-assumed-active', 'if (!trap.NativeActivationStateProjected) return string.Empty;' in traps)

# Scope / ledger.
la={a['id']:a for a in ledger['assertions']}
check('ledger-update-0296', ledger['update']=='0296')
check('door-ledger-strong', la['native.doors-locks-access']['state']=='STRONG')
check('door-ledger-roll-unresolved', 'success-roll' in la['native.doors-locks-access']['description'])
check('trap-ledger-unknown', la['native.traps']['state']=='UNKNOWN')
check('environmental-hazard-curated-removed', 'native.environmental-hazards' not in la)
check('scope-excludes-environmental-native-dependency', 'environmental-hazards' in scope['excludedNativeDependencyIds'])
check('scope-excludes-environmental-curated', 'native.environmental-hazards' in scope['excludedCuratedAssertionIds'])
check('audit-current-ledger', 'majormud_parity_assertions_0296.json' in audit)
check('audit-current-scope', 'majormud_ownership_scope_0296.json' in audit)

systems={x['id']:x for x in registry['systems']}
check('active-registry-no-generic-environmental-hazard', 'environmental-hazards' not in systems)
check('active-registry-door-strong', systems['doors-locks-access']['classification']=='runtime-active-strong-evidence')
check('active-registry-door-explicit-actions', systems['doors-locks-access']['pass25NativeBoundary']['picklockAndBashAreExplicitActions'] is True)
check('active-registry-no-auto-traversal', systems['doors-locks-access']['pass25NativeBoundary']['traversalDoesNotAutoApplyPicklocksOrStrength'] is True)
check('active-registry-trap-disarm-syntax', systems['traps']['pass25NativeBoundary']['nativeDisarmSyntax']=='DISARM TRAP <direction>')
check('active-registry-trap-roll-unknown', systems['traps']['pass25NativeBoundary']['exactDisarmRoll']=='UNKNOWN')

# Evidence artifact.
check('evidence-no-generic-classic-hazard', evidence['classicScope']['genericEnvironmentalHazardSubsystem'] is False)
check('evidence-door-explicit-actions', 'PICKLOCK <direction>' in evidence['doorBoundary']['explicitActions'] and 'BASH <direction>' in evidence['doorBoundary']['explicitActions'])
check('evidence-door-para3-five-minutes', '5-minute' in evidence['doorBoundary']['para3'])
check('evidence-trap-count-295', evidence['trapBoundary']['decodedExitTrapCount']==295)
check('evidence-native-disarm', evidence['trapBoundary']['nativeCommand']=='DISARM TRAP <direction>')
check('evidence-trap-formula-unknown', evidence['trapBoundary']['exactDetectionAndDisarmFormula']=='UNKNOWN')

# Scorecard.
check('score-pass-73', score['PassCount']==73)
check('score-strong-76', score['StrongCount']==76)
check('score-unknown-29', score['UnknownCount']==29)
check('score-total-178', score['ScoredRequirementCount']==178)
check('score-strict-41.0', score['StrictPercent']==41.0)
check('score-backed-83.7', score['EvidenceBackedPercent']==83.7)
check('remaining-system-count-28', score['RemainingSystemCount']==28)

# JSON load checks.
for rel in [
 'Librarium/Parity/majormud_doors_traps_room_effects_evidence_0296.json',
 'Librarium/Parity/majormud_ownership_scope_0296.json',
 'Librarium/Parity/majormud_parity_assertions_0296.json',
 'Librarium/RemainingSystems/remaining_systems_registry.json',
 'Reports/RuntimeVerification/majormud_parity_scorecard_0296.json']:
    try: js(rel); check('json:'+rel,True)
    except Exception as e: check('json:'+rel,False,str(e))

failed=[x for x in checks if not x['pass']]
out={'pass':not failed,'passed':len(checks)-len(failed),'failed':len(failed),'checks':checks}
outfile=root/'Reports/RuntimeVerification/doors_locks_traps_room_effects_pass25_static_validation_0296.json'
outfile.write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(f"Pass 25 static validation: {out['passed']}/{len(checks)} PASS")
for f in failed: print('FAIL',f['name'],f['detail'])
sys.exit(1 if failed else 0)
