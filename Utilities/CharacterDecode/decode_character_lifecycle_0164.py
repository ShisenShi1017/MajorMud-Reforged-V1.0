#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'Data/NativeDecodeReports/CodeUpdate0164CharacterLifecycle/character_lifecycle_contract_0164.json'
runtime=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs').read_text()
steps_login=[
'resolve_canonical_character_id','reject_duplicate_active_ownership','load_validated_save','validate_inventory_equipment_ownership',
'restore_ability_ownership','restore_flags_and_status_effects','restore_timers','rebuild_statistics','validate_death_and_corpse_state',
'validate_room_and_world_placement','register_world_membership','register_room_membership','persist_lifecycle_provenance']
steps_logout=['cancel_trade','detach_combat','detach_movement_and_resting','remove_room_membership','remove_world_membership','detach_party_session','clear_transient_references','final_validated_save','release_active_ownership']
contract={
 'contract_id':'character.lifecycle.0164','authority':'Librarium','classification':'STRONGLY_CORRELATED','confidence':0.93,
 'source':['RuntimeCharacterPersistencePolicy validated primary/backup load and monotonic save sequence','RuntimeSession active ownership and room presence usage','RuntimeCharacterDeathResolver persistent death/corpse validation','Runtime item ownership/location model'],
 'assumptions':['native event-message ordering remains unresolved','external trade/combat/party registries are supplied through deterministic lifecycle hooks'],
 'fallback':'fail_closed_with_compensating_registry_cleanup','runtime_impact':'deterministic atomic login/logout ownership pipeline',
 'save_impact':'lifecycle provenance and cleanup markers are persisted before final save','world_state_impact':'room/world/session ownership is registered only after persistent restoration and removed before final save',
 'validation_coverage':['duplicate ownership','canonical ids','item and nested-container ownership','corpse linkage','placement','partial-login rollback','logout cleanup','orphan registry references'],
 'login_order':steps_login,'logout_order':steps_logout,
 'unknown_native_behaviors':[
  'exact native welcome/login text and event emission order','native reconnect grace interval and socket takeover policy',
  'native link-dead combat retention duration','native party leader succession and invitation cleanup ordering',
  'native trade escrow disposition beyond fail-closed cancellation','native corpse decay reconciliation across offline world ticks'
 ],
 'runtime_markers':['character.lifecycle.phase','character.lifecycle.session','character.lifecycle.last_event','character.lifecycle.provenance','character.lifecycle.reconnect','character.lifecycle.cleanup'],
 'generator_assertions':{
   'login_pipeline_present':'public RuntimeCharacterRecord Login(' in runtime,
   'logout_pipeline_present':'public RuntimeCharacterRecord Logout(' in runtime,
   'rollback_present':'catch' in runtime and 'ClearTransientReferences' in runtime,
   'ownership_validator_present':'ValidatePersistentOwnership' in runtime,
   'orphan_validator_present':'ValidateNoOrphans' in runtime
 }
}
out.write_text(json.dumps(contract,indent=2,sort_keys=True)+'\n')
print(out)
