#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
runtime=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs').read_text()
out=ROOT/'Data/NativeDecodeReports/CodeUpdate0165CharacterLifecycle/character_lifecycle_contract_0165.json'
out.parent.mkdir(parents=True,exist_ok=True)
contract={
 'contract_id':'character.lifecycle.0165','authority':'Librarium','classification':'STRONGLY_CORRELATED','confidence':0.95,
 'source':['CodeUpdate0164 deterministic lifecycle coordinator','persistent semantic runtime markers','validated monotonic save sequence policy','active session ownership registry'],
 'assumptions':['stale session recovery is invoked only after the owning transport/session is confirmed dead','native grace timing and takeover policy remain unresolved'],
 'fallback':'reject_non_monotonic_or_incomplete_transition_and_execute_existing_compensating_cleanup',
 'runtime_impact':'monotonic complete lifecycle transitions plus explicit stale-session recovery',
 'save_impact':'sequence, transition-completion, event, cleanup and provenance markers are validated before save',
 'world_state_impact':'stale ownership recovery uses the same ordered trade/combat/room/world/session cleanup as disconnect',
 'validation_coverage':['sequence overflow','backward lifecycle timestamps','incomplete transition markers','stale provenance','detached session leakage','stale-session owner mismatch','runtime service integration'],
 'promoted_behaviors':['monotonic lifecycle sequence','complete transition receipt','backward-time rejection','stale-session deterministic cleanup','active/detached marker consistency'],
 'unknown_native_behaviors':['exact welcome and event-message ordering','reconnect grace interval and socket takeover','link-dead combat retention duration','party leadership succession ordering','trade escrow disposition beyond cancellation','offline corpse decay reconciliation'],
 'runtime_markers':['character.lifecycle.sequence','character.lifecycle.transition','character.lifecycle.phase','character.lifecycle.session','character.lifecycle.last_event','character.lifecycle.provenance','character.lifecycle.cleanup'],
 'generator_assertions':{
  'contract_version':'ContractVersion = "0165"' in runtime,
  'sequence_marker':'SequenceMarker' in runtime,
  'transition_marker':'TransitionMarker' in runtime,
  'marker_validator':'ValidateLifecycleMarkers' in runtime,
  'stale_recovery':'RecoverStaleSession' in runtime,
  'checked_sequence':'checked(previousSequence + 1L)' in runtime,
  'backward_time_rejection':'event time moved backwards' in runtime
 }
}
out.write_text(json.dumps(contract,indent=2,sort_keys=True)+'\n')
print(out)
