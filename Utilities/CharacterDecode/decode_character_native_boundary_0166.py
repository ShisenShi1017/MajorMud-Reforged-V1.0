#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
runtime=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs').read_text()
out=ROOT/'Data/NativeDecodeReports/CodeUpdate0166CharacterNativeBoundary/character_native_dependency_boundary_0166.json'
out.parent.mkdir(parents=True,exist_ok=True)
unknowns=[
 ('welcome_event_ordering','native executable message-dispatch trace','use deterministic Reforged lifecycle completion without asserting native welcome ordering'),
 ('reconnect_grace_and_socket_takeover','native transport timeout and ownership-transfer trace','reject takeover; permit only exact-owner stale-session cleanup followed by a fresh login'),
 ('link_dead_combat_retention','native combat/world-tick trace across link loss','detach combat through the established ungraceful-disconnect cleanup pipeline'),
 ('party_leadership_succession','native party dispatcher trace','detach the Character without electing or inventing a successor'),
 ('trade_escrow_disposition','native trade escrow and disconnect trace','cancel trade and preserve validated persistent ownership; do not redistribute escrow'),
 ('offline_corpse_decay_reconciliation','native corpse timer/world-tick trace while the owner is offline','preserve validated corpse state and reject speculative offline decay mutation')]
contract={
 'contract_id':'character.native-dependency-boundary.0166',
 'authority':'Librarium',
 'classification':'PROVEN',
 'confidence':1.0,
 'source':['CodeUpdate0165 Character closure assessment','Librarium unresolved native lifecycle evidence','RuntimeCharacterLifecycleCoordinator fail-closed lifecycle pipeline'],
 'assumptions':['no native behavior may be promoted without executable or world-tick evidence','Reforged safe fallback behavior remains distinct from claimed native behavior'],
 'fallback':'deny unsupported native-dependent execution and return the queryable safe fallback decision',
 'runtime_impact':'all six unresolved Character native dependencies are represented by typed non-executable decisions and cannot be invoked accidentally',
 'save_impact':'none; boundary decisions do not mutate Character persistence',
 'world_state_impact':'prevents speculative takeover, combat retention, party succession, escrow redistribution, corpse decay, or message-order side effects',
 'validation_coverage':['six dependency enum values','six UNKNOWN decisions','all decisions non-executable','evidence requirement present','fail-closed behavior present','service exposure','runtime rejection'],
 'dependencies':[{'id':i,'classification':'UNKNOWN','executable':False,'evidence_requirement':e,'fail_closed_behavior':f} for i,e,f in unknowns],
 'generator_assertions':{
   'contract_version':'ContractVersion = "0166"' in runtime,
   'typed_dependency':'RuntimeCharacterNativeDependency' in runtime,
   'typed_decision':'RuntimeCharacterNativeDependencyDecision' in runtime,
   'six_unknown_decisions':runtime.count('"UNKNOWN", false')==6,
   'query_api':'GetNativeDependencyDecision' in runtime,
   'execution_gate':'RequireExecutableNativeDependency' in runtime,
   'not_supported':'throw new NotSupportedException' in runtime
 }
}
out.write_text(json.dumps(contract,indent=2,sort_keys=True)+'\n')
print(out)
