from pathlib import Path
import json, re, sys

root = Path(__file__).resolve().parents[2]
checks=[]
def check(name, ok, detail=''):
    checks.append({'name':name,'pass':bool(ok),'detail':detail})

def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))

progress=text('Apps/Source/Reforged.Server/RuntimeCharacterProgressionAuthority.cs')
formula=text('Apps/Source/Reforged.Server/RuntimeClassicCharacterProgressionFormula.cs')
party=text('Apps/Source/Reforged.Server/RuntimePartyCoordination.cs')
party_live=text('Apps/Source/Reforged.Server/RuntimeClassicPartyGameplay.cs')
combat=text('Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs')
decode_audit=text('Apps/Source/Reforged.Server/RuntimeCharacterDecodeBundleAudit.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
share=text('Apps/Source/Reforged.Server/RuntimeClassicShare.cs')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
evidence=js('Librarium/Parity/majormud_experience_party_reward_evidence_0295.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0295.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0295.json')
partycat=js('Librarium/RemainingSystems/party_catalog.json')

check('no-earned-xp-race-class-modifier-function', 'ApplyDecodedExperienceModifier' not in progress)
check('no-race-class-award-overload', not re.search(r'AwardExperience\s*\([^\)]*RuntimeCharacterDefinition\s+race[^\)]*RuntimeCharacterDefinition\s+characterClass', progress, re.S))
check('raw-award-adds-amount', 'normalized.Experience + amount' in progress)
check('raw-class-ledger-adds-amount', 'GetValueOrDefault(activeClass) + amount' in progress)
check('experience-chart-helper', 'ExperienceChartPercent' in formula)
check('class-chart-expstored-plus-100', 'characterClass.ExperienceStored + 100' in formula)
check('race-chart-added', 'classChart + race.ExperienceAddPercent' in formula)
check('required-exp-uses-chart-helper', 'RequiredExperienceForChart(level, ExperienceChartPercent(race, characterClass))' in formula)
check('decode-audit-uses-chart-not-award-scaling', 'RuntimeClassicCharacterProgressionFormula.ExperienceChartPercent' in decode_audit and 'ApplyDecodedExperienceModifier' not in decode_audit)
check('decode-audit-chart-marker', 'runtime.progression.decoded_race_class_experience_chart' in decode_audit)
check('chart-positive-gate', 'Race/class experience chart must be positive' in formula)

xp_assert=next(x for x in ledger['assertions'] if x['id']=='character.experience-race-class-modifiers')
check('xp-assertion-pass', xp_assert['state']=='PASS')
check('xp-assertion-chart-wording', 'progression chart' in xp_assert['description'].lower())
check('xp-assertion-no-award-multiplier', 'not multipliers' in xp_assert['description'].lower())
check('xp-assertion-0295-evidence', any('0295' in x for x in xp_assert['evidence']))

check('classic-party-policy-exists', 'ClassicStrongEvidence' in party)
check('classic-party-cap-six', 'MaximumMembers: 6' in party)
check('classic-invite-lifetime-unset', re.search(r'ClassicStrongEvidence[\s\S]{0,500}InviteLifetime:\s*null', party) is not None)
check('classic-no-leader-transfer-guess', re.search(r'ClassicStrongEvidence[\s\S]{0,600}LeaderMayTransfer:\s*false', party) is not None)
check('live-classic-party-uses-strong-policy', 'RuntimePartyPolicy.ClassicStrongEvidence' in party_live)
check('party-full-text-remains-gated', party_live.count('Native party-full failure text is not yet recovered.') >= 2)

check('xp-active-exact-npc-participants', 'active.NpcInstanceId.Equals(defeatedEncounter.NpcInstanceId' in combat)
check('xp-not-party-membership-query', 'FindByMember' not in combat[combat.find('ResolveNpcExperienceParticipants'):combat.find('DistributeNpcDefeatExperience')])
check('xp-integer-quotient', 'totalExperience / activeCombatants' in combat)
check('xp-remainder-calculated', 'totalExperience % activeCombatants' in combat)
check('xp-remainder-still-unresolved', 'native-recipient-unresolved-unassigned' in combat)
check('earned-xp-direct-award', re.search(r'AwardExperience\(\s*character,\s*split\.Share,', combat, re.S) is not None)

check('share-is-explicit-command-owner', 'SHARE' in share.upper())
check('party-evidence-six', any(f.get('id')=='party.maximum-size' and f.get('state')=='STRONG' for f in evidence['findings']))
check('party-evidence-loot-room', any(f.get('id')=='party.loot-no-automatic-split' for f in evidence['findings']))
check('xp-evidence-chart-pass', any(f.get('id')=='experience.race-class-chart-purpose' and f.get('state')=='PASS' for f in evidence['findings']))
check('mudcentral-source-retained', any('mudcentral.org' in s.get('url','') for s in evidence['sources']))
check('party-limit-source-retained', any('mudinfo.net' in s.get('url','') for s in evidence['sources']))
check('official-help-source-retained', any('majormud.com/mudhelp_races' in s.get('url','') for s in evidence['sources']))

# locate party system in registry regardless top-level schema
systems=registry.get('systems') if isinstance(registry,dict) else registry
if systems is None:
    systems=next((v for v in registry.values() if isinstance(v,list) and v and isinstance(v[0],dict) and 'id' in v[0]), [])
pr=next(x for x in systems if x['id']=='party-coordination')
check('registry-party-cap-six', pr.get('partyCapacity0295',{}).get('maximumMembers')==6)
check('registry-party-cap-strong', pr.get('partyCapacity0295',{}).get('classification')=='STRONG')
check('registry-no-max-party-unknown', not any('maximum party size' in x.lower() for x in pr.get('nativeDependencies',[])))
check('registry-no-party-loot-share-unknown', not any('loot' in x.lower() and 'sharing' in x.lower() for x in pr.get('nativeDependencies',[])))
check('registry-xp-remainder-remains-unknown', any('remainder' in x.lower() for x in pr.get('nativeDependencies',[])))
check('party-catalog-cap-six', partycat.get('strongNativeFindings',{}).get('maximumMembers')==6)
check('party-catalog-no-auto-loot', 'not auto-split' in partycat.get('integrations',{}).get('economy','').lower())

check('runtime-audit-current-ledger', 'majormud_parity_assertions_0313.json' in audit)
check('score-pass-73', score['PassCount']==73)
check('score-strong-75', score['StrongCount']==75)
check('score-unknown-31', score['UnknownCount']==31)
check('score-total-179', score['ScoredRequirementCount']==179)
check('score-strict-40.8', score['StrictPercent']==40.8)
check('score-backed-82.7', score['EvidenceBackedPercent']==82.7)

for rel in [
 'Librarium/Parity/majormud_experience_party_reward_evidence_0295.json',
 'Librarium/Parity/majormud_parity_assertions_0295.json',
 'Librarium/RemainingSystems/remaining_systems_registry.json',
 'Librarium/RemainingSystems/party_catalog.json',
 'Reports/RuntimeVerification/majormud_parity_scorecard_0295.json']:
    try: js(rel); check('json:'+rel, True)
    except Exception as e: check('json:'+rel, False, str(e))

failed=[x for x in checks if not x['pass']]
out={'pass':not failed,'passed':len(checks)-len(failed),'failed':len(failed),'checks':checks}
outfile=root/'Reports/RuntimeVerification/experience_party_reward_pass24_static_validation_0295.json'
outfile.write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(f"Pass 24 static validation: {out['passed']}/{len(checks)} PASS")
for f in failed: print('FAIL',f['name'],f['detail'])
sys.exit(1 if failed else 0)
