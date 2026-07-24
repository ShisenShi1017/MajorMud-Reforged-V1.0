#!/usr/bin/env python3
import json, py_compile, sys
from pathlib import Path
root=Path(sys.argv[1] if len(sys.argv)>1 else '.')
json_paths=[root/'Evidence/NpcAI/npc_ai_decision_runtime_evidence.json',root/'Evidence/NpcAI/npc_ai_scheduler_ownership_evidence.json',root/'Evidence/NpcAI/npc_ai_tick_lifecycle_evidence.json',root/'Evidence/NpcAI/npc_ai_action_handoff_evidence.json',root/'Evidence/NpcAI/npc_ai_action_lifecycle_evidence.json',root/'Evidence/NpcAI/npc_ai_action_consumer_ownership_evidence.json',root/'Evidence/NpcAI/npc_ai_action_claim_recovery_evidence.json',root/'Evidence/NpcAI/npc_ai_action_failure_recovery_evidence.json',root/'Evidence/NpcAI/npc_ai_spawn_lifecycle_evidence.json',root/'Evidence/NpcAI/npc_ai_death_respawn_recovery_evidence.json',root/'Evidence/NpcAI/npc_ai_spawn_slot_control_evidence.json',root/'Evidence/NpcAI/npc_ai_spawn_slot_reset_evidence.json',root/'Evidence/NpcAI/npc_ai_perception_target_acquisition_evidence.json',root/'Evidence/NpcAI/npc_ai_explicit_threat_ranking_evidence.json',root/'Evidence/NpcAI/npc_ai_threat_generation_evidence.json',root/'Evidence/NpcAI/npc_ai_threat_decay_evidence.json',root/'Evidence/NpcAI/npc_ai_threat_memory_evidence.json',root/'Evidence/NpcAI/npc_ai_threat_taunt_evidence.json',root/'Evidence/NpcAI/npc_ai_runtime_inspection_evidence.json',root/'Evidence/NpcAI/npc_ai_administrative_control_evidence.json',root/'Evidence/NpcAI/npc_ai_administrative_control_provenance.json',root/'Evidence/NpcAI/npc_ai_defensive_profile_evidence.json',root/'Evidence/NpcAI/npc_ai_defensive_profile_provenance.json',root/'Evidence/NpcAI/npc_ai_dialogue_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_dialogue_admission_provenance.json',root/'Evidence/NpcAI/npc_ai_service_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_service_admission_provenance.json',root/'Evidence/NpcAI/npc_ai_inventory_ownership_evidence.json',root/'Evidence/NpcAI/npc_ai_inventory_ownership_provenance.json',root/'Evidence/NpcAI/npc_ai_corpse_ownership_evidence.json',root/'Evidence/NpcAI/npc_ai_corpse_ownership_provenance.json',root/'Evidence/NpcAI/npc_ai_corpse_access_evidence.json',root/'Evidence/NpcAI/npc_ai_corpse_access_provenance.json',root/'Evidence/NpcAI/npc_ai_spawn_inventory_evidence.json',root/'Evidence/NpcAI/npc_ai_spawn_inventory_provenance.json',root/'Evidence/NpcAI/npc_ai_equipment_ownership_evidence.json',root/'Evidence/NpcAI/npc_ai_equipment_ownership_provenance.json',root/'Evidence/NpcAI/npc_ai_item_use_evidence.json',root/'Evidence/NpcAI/npc_ai_item_use_provenance.json',root/'Evidence/NpcAI/npc_ai_ammunition_evidence.json',root/'Evidence/NpcAI/npc_ai_ammunition_provenance.json',root/'Evidence/NpcAI/npc_ai_item_effect_evidence.json',root/'Evidence/NpcAI/npc_ai_item_effect_provenance.json',root/'Evidence/NpcAI/npc_ai_equipment_selection_evidence.json',root/'Evidence/NpcAI/npc_ai_equipment_selection_provenance.json',root/'Evidence/NpcAI/npc_ai_explicit_assist_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_trainer_offering_evidence.json',root/'Evidence/NpcAI/npc_ai_trainer_offering_provenance.json',root/'Evidence/NpcAI/npc_ai_trainer_offering_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_trainer_offering_admission_provenance.json',root/'Evidence/NpcAI/npc_ai_explicit_flee_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_flee_movement_completion_evidence.json',root/'Evidence/NpcAI/npc_ai_return_home_evidence.json',root/'Generated/NPCReconstruction/npc_ai_runtime_catalog.json',root/'Data/Classic/npc_ai_runtime.json',root/'Evidence/NpcAI/npc_ai_merchant_restock_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_merchant_restock_admission_provenance.json',root/'Evidence/NpcAI/npc_ai_trainer_eligibility_admission_evidence.json',root/'Evidence/NpcAI/npc_ai_trainer_eligibility_admission_provenance.json',root/'Evidence/NpcAI/npc_ai_companion_follow_evidence.json',root/'Evidence/NpcAI/npc_ai_companion_follow_provenance.json',root/'Evidence/NpcAI/npc_ai_companion_combat_assist_evidence.json',root/'Evidence/NpcAI/npc_ai_companion_combat_assist_provenance.json',root/'Evidence/NpcAI/npc_ai_companion_lifecycle_cleanup_evidence.json',root/'Evidence/NpcAI/npc_ai_companion_lifecycle_cleanup_provenance.json',root/'Evidence/NpcAI/npc_ai_explicit_route_evidence.json',root/'Evidence/NpcAI/npc_ai_explicit_route_provenance.json',root/'Evidence/NpcAI/npc_ai_service_refusal_dialogue_evidence.json',root/'Evidence/NpcAI/npc_ai_service_refusal_dialogue_provenance.json',root/'Evidence/NpcAI/npc_ai_administrative_service_control_evidence.json',root/'Evidence/NpcAI/npc_ai_administrative_service_control_provenance.json']
for path in json_paths: json.loads(path.read_text(encoding='utf-8-sig'))
for path in [root/'Tools/NpcAi/generate_npc_ai_catalog.py',root/'Tools/NpcAi/validate_npc_ai_pass.py']: py_compile.compile(str(path),doraise=True)
engine=(root/'Apps/Source/Reforged.Server/RuntimeNpcAiDecisionEngine.cs').read_text(encoding='utf-8')
scheduler=(root/'Apps/Source/Reforged.Server/RuntimeNpcAiScheduler.cs').read_text(encoding='utf-8')
program=(root/'Apps/Source/Reforged.Server/Program.cs').read_text(encoding='utf-8')
lifecycle=(root/'Apps/Source/Reforged.Server/RuntimeNpcSpawnLifecycle.cs').read_text(encoding='utf-8')
living=(root/'Apps/Source/Reforged.Server/RuntimeLivingWorld.cs').read_text(encoding='utf-8')
perception=(root/'Apps/Source/Reforged.Server/RuntimeNpcPerception.cs').read_text(encoding='utf-8')
threat=(root/'Apps/Source/Reforged.Server/RuntimeNpcThreat.cs').read_text(encoding='utf-8')
threat_generation=(root/'Apps/Source/Reforged.Server/RuntimeNpcThreatGeneration.cs').read_text(encoding='utf-8')
threat_decay=(root/'Apps/Source/Reforged.Server/RuntimeNpcThreatDecay.cs').read_text(encoding='utf-8')
threat_memory=(root/'Apps/Source/Reforged.Server/RuntimeNpcThreatMemory.cs').read_text(encoding='utf-8')
threat_taunt=(root/'Apps/Source/Reforged.Server/RuntimeNpcThreatTaunt.cs').read_text(encoding='utf-8')
assist=(root/'Apps/Source/Reforged.Server/RuntimeNpcAssist.cs').read_text(encoding='utf-8')
flee=(root/'Apps/Source/Reforged.Server/RuntimeNpcFlee.cs').read_text(encoding='utf-8')
flee_movement=(root/'Apps/Source/Reforged.Server/RuntimeNpcFleeMovement.cs').read_text(encoding='utf-8')
return_home=(root/'Apps/Source/Reforged.Server/RuntimeNpcReturnHome.cs').read_text(encoding='utf-8')
inspection=(root/'Apps/Source/Reforged.Server/RuntimeNpcInspection.cs').read_text(encoding='utf-8')
administration=(root/'Apps/Source/Reforged.Server/RuntimeNpcAdministration.cs').read_text(encoding='utf-8')
defensive=(root/'Apps/Source/Reforged.Server/RuntimeNpcDefensiveProfiles.cs').read_text(encoding='utf-8')
dialogue=(root/'Apps/Source/Reforged.Server/RuntimeNpcDialogueAdmission.cs').read_text(encoding='utf-8')
service_admission=(root/'Apps/Source/Reforged.Server/RuntimeNpcServiceAdmission.cs').read_text(encoding='utf-8')
inventory_ownership=(root/'Apps/Source/Reforged.Server/RuntimeNpcInventoryOwnership.cs').read_text(encoding='utf-8')
corpse_ownership=(root/'Apps/Source/Reforged.Server/RuntimeNpcCorpseOwnership.cs').read_text(encoding='utf-8')
corpse_access=(root/'Apps/Source/Reforged.Server/RuntimeNpcCorpseAccess.cs').read_text(encoding='utf-8')
spawn_inventory=(root/'Apps/Source/Reforged.Server/RuntimeNpcSpawnInventory.cs').read_text(encoding='utf-8')
equipment_ownership=(root/'Apps/Source/Reforged.Server/RuntimeNpcEquipmentOwnership.cs').read_text(encoding='utf-8')
item_use=(root/'Apps/Source/Reforged.Server/RuntimeNpcItemUse.cs').read_text(encoding='utf-8')
ammunition=(root/'Apps/Source/Reforged.Server/RuntimeNpcAmmunitionUse.cs').read_text(encoding='utf-8')
item_effect=(root/'Apps/Source/Reforged.Server/RuntimeNpcItemEffectInvocation.cs').read_text(encoding='utf-8')
equipment_selection=(root/'Apps/Source/Reforged.Server/RuntimeNpcEquipmentSelection.cs').read_text(encoding='utf-8')
loot_materialization=(root/'Apps/Source/Reforged.Server/RuntimeNpcLootMaterialization.cs').read_text(encoding='utf-8')
merchant_stock=(root/'Apps/Source/Reforged.Server/RuntimeNpcMerchantStockOwnership.cs').read_text(encoding='utf-8')
trainer_offerings=(root/'Apps/Source/Reforged.Server/RuntimeNpcTrainerOfferingOwnership.cs').read_text(encoding='utf-8')
trainer_offering_admission=(root/'Apps/Source/Reforged.Server/RuntimeNpcTrainerOfferingAdmission.cs').read_text(encoding='utf-8')
merchant_access_admission=(root/'Apps/Source/Reforged.Server/RuntimeNpcMerchantAccessAdmission.cs').read_text(encoding='utf-8')
merchant_restock_admission=(root/'Apps/Source/Reforged.Server/RuntimeNpcMerchantRestockAdmission.cs').read_text(encoding='utf-8')
trainer_eligibility_admission=(root/'Apps/Source/Reforged.Server/RuntimeNpcTrainerEligibilityAdmission.cs').read_text(encoding='utf-8')
gameplay_state=(root/'Apps/Source/Reforged.Server/RuntimeGameplayCompletionState.cs').read_text(encoding='utf-8')
items_runtime=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text(encoding='utf-8')
world_bundle=(root/'Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs').read_text(encoding='utf-8')
required_engine=['RuntimeNpcDecisionTrace','safe-fallback-native-ai-unresolved','deterministic-replay','RuntimeNpcAiDecisionSmokeReport','AlreadyProcessed','persisted-monotonic-ai-tick','duplicate-tick-suppressed','dead-runtime-state-cleanup','AwaitingActionCompletion','pending-action-blocks-new-decision']
required_scheduler=['RuntimeNpcAiScheduler','TickNpcAi','SetNpcExplicitTarget','QueueNpcInteraction','stable-instance-order','scheduler-deterministic-replay','persisted-duplicate-tick-suppressed','dead-state-cleanup-scheduled','ReadLong','RuntimeNpcActionIntent','ActionIntents','AcknowledgeNpcAction','stable-action-intent','pending_ai_action_id','GetPendingNpcActions','CompleteNpcAction','pending-action-prevents-overwrite','last_ai_action_outcome','RuntimeNpcActionClaim','ClaimNpcAction','GetClaimedNpcActions','ReleaseNpcActionClaim','CompleteClaimedNpcAction','pending_ai_action_claim_consumer','pending_ai_action_claim_id','pending_ai_action_delivery_count','claim-contract-exposed','RenewNpcActionClaim','RecoverExpiredNpcActionClaims','pending_ai_action_claimed_at_tick','RuntimeNpcActionQuarantine','FailClaimedNpcAction','GetQuarantinedNpcActions','RetryQuarantinedNpcAction','pending_ai_action_quarantined','pending_ai_action_last_failure','pending_ai_action_failed_at_tick']
required_lifecycle=['RuntimeNpcSpawnSlotSnapshot','RuntimeNpcLifecycleTransition','CaptureNpcSpawnSlots','EnterNpcRespawnWait','TickNpcSpawnLifecycle','TickNpcSpawnLifecycleLocked','npc_spawn_generation','world-runtime-spawn-slot','home-room-unresolved','ClearTransientNpcExecutionMetadata','RuntimeNpcSpawnLifecycleSmokeReport','RuntimeNpcRespawnBlock','GetBlockedNpcRespawns','RetryBlockedNpcRespawn','EnterNpcRespawnWaitLocked','npc_respawn_block_reason','npc_respawn_retry_requested_at_utc','RuntimeNpcSpawnSlotControl','CaptureNpcSpawnSlotControls','SetNpcSpawnSlotEnabled','RespawnSuppressed','npc_spawn_slot_disabled','npc_respawn_suppressed_at_utc','RuntimeNpcSpawnSlotResetResult','ResetNpcSpawnSlot','npc_spawn_slot_reset_requested_at_utc','npc_spawn_slot_reset_reason']
required_perception=['RuntimeNpcPerceptionCandidate','RuntimeNpcPerceptionSnapshot','RuntimeNpcTargetAcquisitionResult','RuntimeNpcPerceptionService','ApplyNpcPerceptionSnapshot','last_perception_tick','explicit-same-room-detectable-hostile-candidate','safe-fallback-no-explicit-perception-candidate','stable-explicit-hostile-order','retain-valid-prior-target','clear-invalid-target','deterministic-perception-replay']
required_assist=['RuntimeNpcAssistSnapshot','RuntimeNpcAssistResult','RuntimeNpcAssistService','ApplyNpcAssistSnapshot','last_assist_tick','explicit-host-supplied-assist-target','safe-fallback-assist-rejected','explicit-assist-accepted','invalid-assist-does-not-overwrite-target','deterministic-assist-replay']
required_flee=['RuntimeNpcFleeSnapshot','RuntimeNpcFleeResult','RuntimeNpcFleeService','ApplyNpcFleeSnapshot','last_flee_tick','explicit-host-supplied-flee-destination','safe-fallback-flee-rejected','explicit-flee-accepted','unreachable-flee-rejected','deterministic-flee-replay']
required_flee_movement=['RuntimeNpcFleeMovementSnapshot','RuntimeNpcFleeMovementResult','RuntimeNpcFleeMovementService','CompleteNpcFleeMovement','last_flee_movement_tick','explicit-owning-movement-subsystem-completion','safe-fallback-flee-movement-rejected','destination-does-not-match-admitted-flee-destination','exact-admitted-flee-movement-completes','deterministic-flee-movement-replay']
required_administration=['RuntimeNpcAdministrationResult','SetNpcAiEnabled','SetNpcFrozen','MoveNpc','RuntimeNpcAdministrationSmokeReport','disabled-ai-blocks-decision','frozen-npc-holds-state','destination-room-is-not-canonical','dead-instance-cannot-be-moved']

required_dialogue=['RuntimeNpcDialogueAdmission','AdmitNpcDialogue','CompleteNpcDialogue','npc_dialogue_active_request_id','dialogue-already-active','room-ownership-mismatch','RuntimeNpcDialogueAdmissionSmokeReport']
required_inventory_ownership=['RuntimeNpcInventorySnapshot','RuntimeNpcInventoryTransferResult','RuntimeNpcInventoryOwnershipService','NpcInventory','MoveFromNpc','DropFromNpc','room-ownership-enforced','exact-give-to-npc','stable-npc-inventory-inspection','return-authority-enforced','exact-return-from-npc','RuntimeNpcInventoryOwnershipSmokeReport']
required_corpse_ownership=['RuntimeNpcCorpseTransferResult','RuntimeNpcCorpseOwnershipService','TransferDeathInventory','CreateNpcCorpse','living-npc-rejected','exact-death-inventory-transfer','duplicate-corpse-rejected','RuntimeNpcCorpseOwnershipSmokeReport']
required_item_use=['RuntimeNpcItemUseService','UseNpc','RuntimeNpcItemUseSmokeReport','exact-owned-item-use','authority-enforced']
required_equipment_selection=['RuntimeNpcEquipmentSelectionService','AdmitNpcEquipmentSelection','CompleteNpcEquipmentSelection','RuntimeNpcEquipmentSelectionSmokeReport']
required_loot_materialization=['RuntimeNpcLootMaterializationService','RuntimeNpcLootMaterializationRequest','CreateCorpseItem','npc_loot_request_id','already-applied','RuntimeNpcLootMaterializationSmokeReport']
required_merchant_stock=['RuntimeNpcMerchantStockOwnershipService','RuntimeNpcMerchantStockMutation','RuntimeGameplayCompletionStateStore','SetExact','stock-set-exactly','RuntimeNpcMerchantStockOwnershipSmokeReport']
required_item_effect=['RuntimeNpcItemEffectAdmission','RuntimeNpcItemEffectCompletion','RuntimeNpcItemEffectService','AdmitNpcItemEffect','CompleteNpcItemEffect','item-effect-already-active','request-id-mismatch','failed-effect-preserves-item','successful-effect-consumes-item','RuntimeNpcItemEffectSmokeReport']
required_ammunition=['RuntimeNpcAmmunitionRequest','RuntimeNpcAmmunitionService','ConsumeNpcQuantity','RuntimeNpcAmmunitionSmokeReport','compatibility-confirmation-enforced','exact-ammunition-consumed','exact-quantity-remains']
required_equipment_ownership=['RuntimeNpcEquipmentResult','RuntimeNpcEquipmentOwnershipService','NpcEquipment','EquipNpc','UnequipNpc','authority-enforced','exact-npc-equip','exact-npc-unequip','RuntimeNpcEquipmentOwnershipSmokeReport']
required_spawn_inventory=['RuntimeNpcSpawnInventoryRequest','RuntimeNpcSpawnInventoryService','spawn_inventory_request_id','authority-enforced','exact-manifest-applied','request-replay-suppressed','RuntimeNpcSpawnInventorySmokeReport']
required_corpse_access=['RuntimeNpcCorpseAccessResult','RuntimeNpcCorpseAccessService','CorpseInventory','CleanupCorpseExact','access-authority-enforced','exact-item-loot','exact-gold-loot','exact-due-cleanup','RuntimeNpcCorpseAccessSmokeReport']
required_service_admission=['RuntimeNpcServiceAdmission','AdmitNpcService','CompleteNpcService','npc_service_active_request_id','service-already-active','service-kind-not-supported-by-npc','RuntimeNpcServiceAdmissionSmokeReport']
required_defensive=['RuntimeNpcDefensiveProfile','RuntimeNpcDefensiveProfileResolver','generated-npc-armor-class-and-damage-resistance','DefenseValue: 0','DodgeBonus: 0','RuntimeNpcDefensiveProfileSmokeReport','all-npc-defensive-profiles-ready','no-synthetic-npc-defense-value','no-synthetic-npc-dodge-value','classic-combatant-mapping']
required_inspection=['RuntimeNpcInspectionSnapshot','RuntimeNpcInspectionQuery','InspectNpcs','InspectNpc','RuntimeNpcInspectionSmokeReport','all-runtime-npcs-visible','stable-instance-order','unknown-instance-safe-null']
required_return_home=['RuntimeNpcReturnHomeSnapshot','RuntimeNpcReturnHomeResult','RuntimeNpcReturnHomeService','ApplyNpcReturnHomeSnapshot','RuntimeNpcReturnHomeMovementSnapshot','RuntimeNpcReturnHomeMovementService','CompleteNpcReturnHomeMovement','explicit-host-supplied-return-home-admission','safe-fallback-return-home-rejected','explicit-owning-movement-subsystem-return-home-completion','destination-does-not-match-admitted-home-room','explicit-return-home-admitted','exact-return-home-movement-completes','deterministic-return-home-replay']

required_threat_generation=['RuntimeNpcThreatGenerationEvent','RuntimeNpcThreatGenerationSnapshot','RuntimeNpcThreatGenerationResult','RuntimeNpcThreatGenerationService','ApplyNpcThreatGenerationSnapshot','npc_explicit_threat_ledger_json','last_threat_generation_tick','explicit-host-supplied-threat-deltas','duplicate-threat-generation-tick','regressive-threat-generation-tick','explicit-threat-deltas-accumulate','deterministic-threat-generation-replay']
required_threat_decay=['RuntimeNpcThreatDecayEvent','RuntimeNpcThreatDecaySnapshot','RuntimeNpcThreatDecayResult','RuntimeNpcThreatDecayService','ApplyNpcThreatDecaySnapshot','last_threat_decay_tick','explicit-host-supplied-threat-reductions','duplicate-threat-decay-tick','regressive-threat-decay-tick','explicit-threat-reductions-apply','deterministic-threat-decay-replay']
required_threat_memory=['RuntimeNpcThreatForgetEvent','RuntimeNpcThreatMemorySnapshot','RuntimeNpcThreatMemoryResult','RuntimeNpcThreatMemoryService','ApplyNpcThreatMemorySnapshot','last_threat_memory_tick','explicit-host-authorized-threat-forgetting','duplicate-threat-memory-tick','regressive-threat-memory-tick','explicit-threat-forgetting-applies','deterministic-threat-memory-replay']
required_threat_taunt=['RuntimeNpcThreatTauntEvent','RuntimeNpcThreatTauntSnapshot','RuntimeNpcThreatTauntResult','RuntimeNpcThreatTauntService','ApplyNpcThreatTauntSnapshot','last_threat_taunt_tick','explicit-host-authorized-taunt-target-and-threat-floor','duplicate-threat-taunt-tick','regressive-threat-taunt-tick','explicit-taunt-target-override-applies','deterministic-threat-taunt-replay']
required_trainer_offerings=['RuntimeNpcTrainerOfferingSnapshot','RuntimeNpcTrainerOfferingMutation','RuntimeNpcTrainerOfferingResult','RuntimeNpcTrainerOfferingOwnershipService','Inspect','SetExact','offering-set-exactly','RuntimeNpcTrainerOfferingOwnershipSmokeReport','ExecuteTrainerTransaction','ReadTrainer','RuntimeTrainerState','RuntimeTrainerOfferingEntry']
required_threat=['RuntimeNpcThreatCandidate','RuntimeNpcThreatSnapshot','RuntimeNpcThreatSelectionResult','RuntimeNpcThreatService','ApplyNpcThreatSnapshot','last_threat_tick','explicit-host-supplied-threat-ranking','safe-fallback-no-explicit-threat-candidate','highest-explicit-threat-selected','stable-threat-tie-break','negative-threat-rejected','deterministic-threat-replay']
missing=[x for x in required_engine if x not in engine]+[x for x in required_scheduler if x not in scheduler]+[x for x in required_lifecycle if x not in lifecycle]+[x for x in required_perception if x not in perception]+[x for x in required_threat if x not in threat]+[x for x in required_threat_generation if x not in threat_generation]+[x for x in required_threat_decay if x not in threat_decay]+[x for x in required_threat_memory if x not in threat_memory]+[x for x in required_threat_taunt if x not in threat_taunt]+[x for x in required_assist if x not in assist]+[x for x in required_flee if x not in flee]+[x for x in required_flee_movement if x not in flee_movement]+[x for x in required_return_home if x not in return_home]+[x for x in required_inspection if x not in inspection]+[x for x in required_administration if x not in administration]+[x for x in required_defensive if x not in defensive]+[x for x in required_dialogue if x not in dialogue]+[x for x in required_service_admission if x not in service_admission]+[x for x in required_inventory_ownership if x not in inventory_ownership and x not in items_runtime]+[x for x in required_corpse_ownership if x not in corpse_ownership and x not in items_runtime]+[x for x in required_corpse_access if x not in corpse_access and x not in items_runtime]+[x for x in required_spawn_inventory if x not in spawn_inventory and x not in program]+[x for x in required_equipment_ownership if x not in equipment_ownership and x not in items_runtime and x not in program]+[x for x in required_item_use if x not in item_use and x not in items_runtime and x not in program]+[x for x in required_ammunition if x not in ammunition and x not in items_runtime and x not in program]+[x for x in required_item_effect if x not in item_effect and x not in items_runtime and x not in program]+[x for x in required_equipment_selection if x not in equipment_selection and x not in program]+[x for x in required_loot_materialization if x not in loot_materialization and x not in items_runtime and x not in program]+[x for x in required_merchant_stock if x not in merchant_stock and x not in program]+[x for x in required_trainer_offerings if x not in trainer_offerings and x not in gameplay_state and x not in program]+[x for x in ['AdmitLivingNpcDialogue','CompleteLivingNpcDialogue','ResolveServiceNpc','AdmitNpcService','CompleteNpcService'] if x not in world_bundle and x not in service_admission]
if missing: raise SystemExit('missing-csharp-symbols:'+','.join(missing))
if '--smoke-npc-trainer-offerings' not in program or '--smoke-npc-merchant-stock' not in program or '--smoke-npc-loot-materialization' not in program or '--smoke-npc-equipment-selection' not in program or '--smoke-npc-item-effect' not in program or '--smoke-npc-ammunition' not in program or '--smoke-npc-equipment-ownership' not in program or '--smoke-npc-corpse-access' not in program or '--smoke-npc-administration' not in program or '--smoke-npc-corpse-ownership' not in program or '--smoke-npc-inventory-ownership' not in program or '--smoke-npc-inspection' not in program or '--smoke-npc-threat-taunt' not in program or '--smoke-npc-threat-memory' not in program or '--smoke-npc-threat-decay' not in program or '--smoke-npc-threat-generation' not in program or '--smoke-npc-flee-movement' not in program or '--smoke-npc-flee' not in program or '--smoke-npc-threat' not in program or '--smoke-npc-assist' not in program or '--smoke-npc-perception' not in program or '--smoke-npc-ai-decision' not in program or '--smoke-npc-ai-scheduler' not in program or '--smoke-npc-spawn-lifecycle' not in program or '--smoke-npc-explicit-route' not in program: raise SystemExit('missing-program-smoke-switch')
for path,text in [('engine',engine),('scheduler',scheduler),('lifecycle',lifecycle),('living',living),('program',program),('perception',perception),('threat',threat),('assist',assist),('flee',flee),('flee_movement',flee_movement),('return_home',return_home),('inspection',inspection),('administration',administration),('defensive',defensive),('dialogue',dialogue),('service_admission',service_admission),('inventory_ownership',inventory_ownership),('corpse_ownership',corpse_ownership),('corpse_access',corpse_access),('spawn_inventory',spawn_inventory),('equipment_ownership',equipment_ownership),('item_use',item_use),('ammunition',ammunition),('item_effect',item_effect),('equipment_selection',equipment_selection),('loot_materialization',loot_materialization),('merchant_stock',merchant_stock),('trainer_offerings',trainer_offerings),('merchant_access_admission',merchant_access_admission),('gameplay_state',gameplay_state),('items_runtime',items_runtime),('world_bundle',world_bundle),('threat_generation',threat_generation),('threat_decay',threat_decay),('threat_memory',threat_memory)]:
    if text.count('{') != text.count('}'): raise SystemExit('csharp-brace-imbalance:'+path)
cat=json.loads((root/'Data/Classic/npc_ai_runtime.json').read_text())
if cat['schema_version']!='npc-ai-catalog.v53': raise SystemExit('catalog-schema-mismatch')
if cat['npc_count'] != 1100 or cat['spawn_count'] != 962: raise SystemExit('catalog-count-mismatch')
if cat['unresolved_references']: raise SystemExit('unresolved-canonical-npc-references')
for key in ['runtime_instance','respawn_timer','explicit_target','interaction_request','decision_trace_summary','last_ai_tick','pending_action','action_completion','action_claim','action_delivery_count','action_claim_lease','action_failure','action_quarantine','spawn_slot','spawn_generation','respawn_admission','blocked_respawn','spawn_slot_control','suppressed_respawn','spawn_slot_reset','death_admission','perception_snapshot','target_acquisition','threat_snapshot','threat_selection','threat_generation_snapshot','threat_generation','threat_decay_snapshot','threat_decay','threat_memory_snapshot','threat_memory','assist_snapshot','assist_admission','flee_snapshot','flee_admission','flee_movement_completion','return_home_admission','return_home_movement_completion','runtime_inspection','administrative_ai_control','administrative_freeze_control','administrative_move','npc_defensive_profile','npc_dialogue_admission','npc_dialogue_completion','npc_service_admission','npc_service_completion','npc_inventory','npc_inventory_transfer','npc_corpse_inventory_transfer','npc_corpse_access','npc_corpse_cleanup','npc_spawn_inventory','npc_equipment','npc_item_use','npc_ammunition_use','npc_item_effect_request','npc_item_effect_completion','npc_equipment_selection_request','npc_equipment_selection_completion','npc_loot_materialization','npc_merchant_stock','npc_merchant_access','npc_merchant_restock','npc_trainer_eligibility','npc_trainer_offerings','npc_companion_ownership','npc_companion_follow','npc_companion_combat_assist','npc_companion_lifecycle_cleanup','npc_companion_command','npc_explicit_route','npc_persistence_validation','npc_service_refusal_dialogue']:
    if key not in cat['ownership_catalog']: raise SystemExit('missing-ownership:'+key)
print(json.dumps({'passed':True,'json_files':len(json_paths),'npc_count':cat['npc_count'],'spawn_count':cat['spawn_count'],'checks':required_engine+required_scheduler+required_lifecycle+required_perception+required_threat+required_threat_generation+required_threat_decay+required_threat_memory+required_trainer_offerings+required_assist+required_flee+required_flee_movement+required_return_home+required_inspection+required_administration+required_defensive+required_dialogue+required_service_admission+['program-smoke-switch','ownership-catalog','canonical-boss-references']}))


required_trainer_offering_admission=['RuntimeNpcTrainerOfferingAdmissionService','AdmitExact','CompleteExact','offering-cooldown-active','active-request-mismatch','next-availability-precedes-completion','offering-completed-and-cooldown-committed','RuntimeNpcTrainerOfferingAdmissionSmokeReport']
for token in required_trainer_offering_admission:
    assert token in trainer_offering_admission, f'missing trainer offering admission token: {token}'
assert '--smoke-npc-trainer-offering-admission' in program

required_merchant_access_admission=['RuntimeNpcMerchantAccessAdmissionService','AdmitExact','CompleteExact','shop-closed','actor-ineligible','currency-not-authorized','merchant-access-admitted','active-request-mismatch','RuntimeNpcMerchantAccessAdmissionSmokeReport']
for token in required_merchant_access_admission:
    assert token in merchant_access_admission, f'missing merchant access admission token: {token}'
assert '--smoke-npc-merchant-access-admission' in program
assert 'npc_merchant_access' in cat['ownership_catalog']

required_merchant_restock_admission=['RuntimeNpcMerchantRestockAdmissionService','ApplyExact','exact-restock-applied','restock-request-already-applied','ExecuteExactShopStateTransaction','AppliedRestockRequestIds','RuntimeNpcMerchantRestockAdmissionSmokeReport']
for token in required_merchant_restock_admission:
    assert token in (merchant_restock_admission + gameplay_state), f'missing merchant restock admission token: {token}'
assert '--smoke-npc-merchant-restock-admission' in program
assert 'npc_merchant_restock' in cat['ownership_catalog']

print('npc-ai-pass-validation-ok')

required_trainer_eligibility=['RuntimeNpcTrainerEligibilityAdmissionService','AdmitExact','actor-ineligible','resources-not-authorized','progression-not-authorized','trainer-eligibility-admitted','RuntimeNpcTrainerEligibilityAdmissionSmokeReport']
for token in required_trainer_eligibility:
    assert token in trainer_eligibility_admission, f'missing trainer eligibility token: {token}'
assert '--smoke-npc-trainer-eligibility-admission' in program
assert 'npc_trainer_eligibility' in cat['ownership_catalog']

companion_ownership=(root/'Apps/Source/Reforged.Server/RuntimeNpcCompanionOwnership.cs').read_text()
for token in ['AssignNpcCompanionOwnership','ReleaseNpcCompanionOwnership','InspectNpcCompanionOwnership','companion-owner-already-assigned','companion-owner-mismatch','RuntimeNpcCompanionOwnershipSmokeReport']:
    assert token in companion_ownership, f'missing companion ownership token: {token}'
assert '--smoke-npc-companion-ownership' in program
assert 'npc_companion_ownership' in cat['ownership_catalog']
print('npc-companion-ownership-validation-ok')

companion_follow=(root/'Apps/Source/Reforged.Server/RuntimeNpcCompanionFollow.cs').read_text()
for token in ['AdmitNpcCompanionFollow','CompleteNpcCompanionFollow','companion-follow-admitted','destination-does-not-match-leader-room','destination-does-not-match-admitted-follow','RuntimeNpcCompanionFollowSmokeReport']:
    assert token in companion_follow, f'missing companion follow token: {token}'
assert '--smoke-npc-companion-follow' in program
assert 'npc_companion_follow' in cat['ownership_catalog']
assert companion_follow.count('{') == companion_follow.count('}'), 'csharp-brace-imbalance:companion-follow'
print('npc-companion-follow-validation-ok')

companion_combat=(root/'Apps/Source/Reforged.Server/RuntimeNpcCompanionCombatAssist.cs').read_text()
for token in ['ApplyNpcCompanionCombatAssist','assistance-not-explicitly-authorized','companion-owner-kind-mismatch','target-not-explicitly-hostile','current_target_id']:
    assert token in companion_combat, f'missing companion combat assist token: {token}'
assert companion_combat.count('{') == companion_combat.count('}'), 'csharp-brace-imbalance:companion-combat-assist'


companion_cleanup=(root/'Apps/Source/Reforged.Server/RuntimeNpcCompanionLifecycleCleanup.cs').read_text()
for token in ['CleanupNpcCompanionLifecycle','cleanup-reason-not-supported','companion-death-requires-dead-instance','npc_companion_last_cleanup_reason','threat_ledger_json','RuntimeNpcCompanionLifecycleCleanupSmokeReport']:
    assert token in companion_cleanup, f'missing companion lifecycle cleanup token: {token}'
assert '--smoke-npc-companion-lifecycle-cleanup' in program
assert 'npc_companion_lifecycle_cleanup' in cat['ownership_catalog']
assert companion_cleanup.count('{') == companion_cleanup.count('}'), 'csharp-brace-imbalance:companion-lifecycle-cleanup'
print('npc-companion-lifecycle-cleanup-validation-ok')

companion_command=(root/'Apps/Source/Reforged.Server/RuntimeNpcCompanionCommand.cs').read_text()
for token in ['ApplyNpcCompanionCommand','command-not-supported','companion-owner-kind-mismatch','npc_companion_command_mode','companion-command-applied','RuntimeNpcCompanionCommandSmokeReport']:
    assert token in companion_command, f'missing companion command token: {token}'
assert '--smoke-npc-companion-command' in program
assert 'npc_companion_command' in cat['ownership_catalog']
assert companion_command.count('{') == companion_command.count('}'), 'csharp-brace-imbalance:companion-command'
print('npc-companion-command-validation-ok')


explicit_route=(root/'Apps/Source/Reforged.Server/RuntimeNpcExplicitRoute.cs').read_text()
for token in ['AdmitNpcExplicitRoute','CompleteNpcExplicitRouteStep','CancelNpcExplicitRoute','route-origin-mismatch','route-step-mismatch','traversal-not-explicitly-completed','RuntimeNpcExplicitRouteSmokeReport']:
    assert token in explicit_route, f'missing explicit route token: {token}'
assert '--smoke-npc-explicit-route' in program
assert 'npc_explicit_route' in cat['ownership_catalog']
assert explicit_route.count('{') == explicit_route.count('}'), 'csharp-brace-imbalance:explicit-route'
print('npc-explicit-route-validation-ok')


persistence_validation=(root/'Apps/Source/Reforged.Server/RuntimeNpcPersistenceValidation.cs').read_text()
for token in ['ValidateNpcPersistenceState','repair-authority-required','orphaned-action-claim','dead-npc-retains-transient-state','active-route-state-invalid','RuntimeNpcPersistenceValidationSmokeReport']:
    assert token in persistence_validation, f'missing NPC persistence validation token: {token}'
assert '--smoke-npc-persistence-validation' in program
assert 'npc_persistence_validation' in cat['ownership_catalog']
assert persistence_validation.count('{') == persistence_validation.count('}'), 'csharp-brace-imbalance:npc-persistence-validation'
print('npc-persistence-validation-ok')

service_refusal=(root/'Apps/Source/Reforged.Server/RuntimeNpcServiceRefusalDialogue.cs').read_text(encoding='utf-8')
for token in ['RuntimeNpcServiceRefusalDialogueService','AdmitExact','CompleteExact','textblock-not-canonical','service-refusal-dialogue-admitted','RuntimeNpcServiceRefusalDialogueSmokeReport']:
    assert token in service_refusal, f'missing service refusal token: {token}'
assert '--smoke-npc-service-refusal-dialogue' in program
assert 'npc_service_refusal_dialogue' in cat['ownership_catalog']
print('npc-service-refusal-dialogue-validation-ok')

admin_service_control=(root/'Apps/Source/Reforged.Server/RuntimeNpcAdministrativeServiceControl.cs').read_text(encoding='utf-8')
assert 'CancelNpcTransientInteraction' in admin_service_control
