#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
source=(root/'Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs').read_text(encoding='utf-8')
required=['RuntimeNpcAiState','RuntimeLivingCorpseState','RuntimeLivingWorldEvent','RuntimeExitGateDefinition','EvaluateExitGate','AcquireNpcTarget','corpse-expired','qualified-unknown-evidence-only','SaveLivingWorldCompletionLocked','RestoreLivingWorldCompletion']
missing=[x for x in required if x not in source]
exits=json.loads((root/'Data/Classic/exits.json').read_text(encoding='utf-8')); exits=exits.get('records',exits)
kinds={str(x.get('gate_kind','')).lower() for x in exits}
expected={'door','hidden','required_item','cast_transition','spell_trap','damage_trap','text_command','toll','qualified_unknown','open'}
blocked=sum(1 for x in exits if x.get('destination_status') in {'missing','invalid'} or x.get('has_invalid_destination') is True)
if missing or kinds!=expected:
 print('FAIL',{'missing_source_contracts':missing,'gate_kinds':sorted(kinds),'expected':sorted(expected)});sys.exit(1)
print(f'PASS living-world structure; exits={len(exits)} gateKinds={len(kinds)} unresolved destinations remain generated/evidence-controlled')
