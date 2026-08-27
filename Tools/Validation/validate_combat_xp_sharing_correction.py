#!/usr/bin/env python3
from pathlib import Path
import json, re, sys
ROOT=Path(__file__).resolve().parents[2]
checks=[]
def req(cond,name):
    checks.append((name,bool(cond)))
combat=(ROOT/'Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs').read_text()
rounds=(ROOT/'Apps/Source/Reforged.Server/RuntimeLiveCombatRounds.cs').read_text()
registry=json.loads((ROOT/'Librarium/RemainingSystems/remaining_systems_registry.json').read_text())
evidence=json.loads((ROOT/'Librarium/Parity/majormud_combat_xp_sharing_evidence_0294.json').read_text())
req('ResolveNpcExperienceParticipants' in combat,'active-combat-participant-resolver')
req('active.NpcInstanceId.Equals(defeatedEncounter.NpcInstanceId' in combat,'exact-npc-instance-eligibility')
req('SplitClassicNpcExperience(npc.ExperienceReward, participants.Count)' in combat,'split-by-active-combatant-count')
req('return (totalExperience / activeCombatants, totalExperience % activeCombatants);' in combat,'integer-quotient-and-remainder')
req('RuntimeCharacterProgressionResolver.AwardExperience(\n                character,\n                split.Share,' in combat,'direct-share-award')
req('native-recipient-unresolved-unassigned' in combat,'remainder-evidence-gate')
req('participant.EnqueueMessage($"You gain {split.Share} experience.\\n*Combat Off*")' in combat,'participant-native-xp-message')
req('CompleteNpcDefeat(session, encounter, before, npc, playerHp)' in rounds,'death-handler-receives-encounter')
req('_liveCombats.TryRemove(session.Id, out _);\n                messages.Add(RuntimeClassicCombatAnsi.Victory' not in rounds,'no-premature-killer-removal')
req('AwardExperience(before, npc.ExperienceReward' not in combat,'killer-only-full-xp-removed')
req('525, 2' in combat and '262' in combat,'525-two-way-regression')
req('125, 2' in combat and '62' in combat,'125-two-way-regression')
req(evidence['findings'][2]['classification']=='directly disproven candidate','highest-damage-remainder-disproven-recorded')
req(evidence['findings'][3]['classification']=='UNKNOWN','remainder-stays-unknown')
records=registry if isinstance(registry,list) else registry.get('systems',registry.get('records',[]))
party=next(x for x in records if x.get('id')=='party-coordination')
req('xpSharing0294' in party,'party-registry-narrowed')
req('party membership irrelevant' in party['xpSharing0294']['eligibility'],'party-membership-not-xp-authority')
failed=[n for n,v in checks if not v]
out={'code_update':'0294','passed':not failed,'checks':len(checks),'failures':failed,'results':[{'name':n,'passed':v} for n,v in checks]}
outp=ROOT/'Reports/RuntimeVerification/combat_xp_sharing_correction_0294.json'
outp.parent.mkdir(parents=True,exist_ok=True); outp.write_text(json.dumps(out,indent=2)+'\n')
print(f"PASS {len(checks)-len(failed)}/{len(checks)}" if not failed else f"FAIL {len(failed)}: {failed}")
sys.exit(1 if failed else 0)
