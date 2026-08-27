#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name):
    checks.append(name)
    if not cond: failures.append(name)
def text(rel): return (root/rel).read_text(encoding='utf-8')
policy=json.loads(text('Librarium/Parity/majormud_ownership_scope_0284.json'))
ledger=json.loads(text('Librarium/Parity/majormud_parity_assertions_0284.json'))
score=json.loads(text('Reports/RuntimeVerification/majormud_parity_scorecard_0284.json'))
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
req(policy.get('update')=='0284','scope-update-0284')
req('worldgroup.semantic-boundary' in policy['excludedCuratedAssertionIds'],'exclude-curated-worldgroup-boundary')
req('worldgroup-integration' in policy['excludedNativeDependencyIds'],'exclude-native-worldgroup-integration')
req(all(a['id']!='worldgroup.semantic-boundary' for a in ledger['assertions']),'curated-worldgroup-removed-ledger')
req(ledger.get('update')=='0284','ledger-update-0284')
req(('majormud_parity_assertions_0284.json' in audit or 'majormud_parity_assertions_0285.json' in audit or 'majormud_parity_assertions_0286.json' in audit or 'majormud_parity_assertions_0287.json' in audit or 'majormud_parity_assertions_0288.json' in audit),'audit-points-current-ledger')
req('majormud_ownership_scope_0284.json' in audit,'audit-loads-ownership-scope')
req('ExcludedNativeDependencyIds.Contains(id)' in audit,'audit-skips-worldgroup-native')
req('MixedNativeDependencyOverrides.TryGetValue' in audit,'audit-scopes-mixed-native')
req(score['PassCount']==69,'score-pass-69')
req(score['StrongCount']==55,'score-strong-55')
req(score['UnknownCount']==47,'score-unknown-47')
req(score['ScoredRequirementCount']==171,'score-total-171')
req(abs(score['StrictPercent']-40.4)<0.01,'score-strict-40.4')
req(abs(score['EvidenceBackedPercent']-72.5)<0.01,'score-evidence-72.5')
ids={a['Id'] for a in score['Assertions']}
req('native.worldgroup-integration' not in ids,'score-excludes-worldgroup-integration')
req('worldgroup.semantic-boundary' not in ids,'score-excludes-worldgroup-semantic')
# Mixed dependency descriptions must no longer demand the host-only pieces removed in 0284.
byid={a['Id']:a for a in score['Assertions']}
req('Worldgroup account and authentication structures' not in byid['native.account-session-ownership']['Description'],'account-host-auth-removed')
req('Worldgroup admission and shutdown hooks' not in byid['native.startup-shutdown']['Description'],'startup-host-hooks-removed')
req('Worldgroup authentication and authorization interfaces' not in byid['native.security-permissions']['Description'],'security-host-auth-removed')
req('maximum users and module capacity ownership' not in byid['native.world-rules-configuration']['Description'],'world-rules-host-capacity-removed')
req('native MajorMUD character slot limit' in byid['native.account-session-ownership']['Description'],'account-majormud-character-boundary-retained')
req('MajorMUD module initialization and teardown ordering' in byid['native.startup-shutdown']['Description'],'startup-majormud-boundary-retained')
report={'schema':'majormud.pass14-worldgroup-ownership-validation.v1','update':'0284','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/worldgroup_ownership_pass14_static_validation_0284.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
sys.exit(1 if failures else 0)
