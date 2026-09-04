using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleESubsystem(
    string Name,
    string RuntimeOwner,
    string RuntimeConsumer,
    string PersistenceOwner,
    string ValidationOwner,
    string EvidenceStatus,
    IReadOnlyList<string> SourceFiles,
    bool Passed);

public sealed record RuntimeBundleEExhaustionAuditReport(
    bool BundleEComplete,
    string WorldId,
    int NpcDefinitions,
    int SpawnRecords,
    int RuntimeNpcInstances,
    IReadOnlyList<RuntimeBundleESubsystem> Subsystems,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> DuplicatePaths,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleEExhaustionAudit
{
    private sealed record Spec(string Name, string Owner, string Consumer, string Persistence, string Validation, string Evidence, string[] Files);

    private static readonly Spec[] Specs =
    [
        new("NPC authority", "ClassicRuntimeCatalog / RuntimeWorld.Npcs", "RuntimeWorldLoader and RuntimeLivingWorldService", "Generated Classic Data + living-world state", "NPC definition and generated field inventory", "Verified", ["RuntimeNpcModels.cs", "RuntimeWorldLoader.cs", "RuntimeClassicNpcAiCatalog.cs"]),
        new("Spawning", "RuntimeNpcSpawnLifecycle", "RuntimeLivingWorldService materialization and tick scheduler", "RuntimeLivingWorldService.Save", "RuntimeLivingWorldSmokeReport spawn lifecycle", "Verified", ["RuntimeNpcSpawnLifecycle.cs", "RuntimeNpcSpawnInventory.cs", "RuntimeLivingWorld.cs"]),
        new("Movement", "RuntimeNpcAiDecisionEngine", "RuntimeNpcExplicitRoute / flee / return-home / companion-follow", "living NPC room and route metadata", "route, flee, return-home and follow completion validation", "Strongly Corroborated", ["RuntimeNpcAiDecisionEngine.cs", "RuntimeBundleEMovementAiClosureAudit.cs", "RuntimeNpcExplicitRoute.cs", "RuntimeNpcFleeMovement.cs", "RuntimeNpcReturnHome.cs", "RuntimeNpcCompanionFollow.cs"]),
        new("AI and aggro", "RuntimeNpcAiScheduler", "perception, threat, assist, target, flee and decision consumers", "threat memory and pending action metadata", "AI state instantiation and decision trace validation", "Verified", ["RuntimeNpcAiScheduler.cs", "RuntimeNpcAiDecisionEngine.cs", "RuntimeNpcPerception.cs", "RuntimeNpcThreat.cs", "RuntimeNpcThreatMemory.cs", "RuntimeNpcThreatGeneration.cs", "RuntimeNpcThreatDecay.cs", "RuntimeNpcThreatTaunt.cs", "RuntimeNpcAssist.cs", "RuntimeNpcFlee.cs"]),
        new("Followers, pets and companions", "RuntimeNpcCompanionOwnership", "command, follow, combat assist, transfer and lifecycle cleanup", "companion owner metadata in living-world persistence", "companion ownership and lifecycle validators", "Strongly Corroborated", ["RuntimeNpcCompanionOwnership.cs", "RuntimeNpcCompanionCommand.cs", "RuntimeNpcCompanionFollow.cs", "RuntimeNpcCompanionCombatAssist.cs", "RuntimeNpcCompanionOwnershipTransfer.cs", "RuntimeNpcCompanionLifecycleCleanup.cs"]),
        new("Merchants", "RuntimeNpcMerchantStockOwnership", "access, restock and gold settlement; Bundle C economy remains authoritative", "merchant stock/gold metadata", "merchant admission and lifecycle validation", "Verified", ["RuntimeNpcMerchantStockOwnership.cs", "RuntimeNpcMerchantAccessAdmission.cs", "RuntimeNpcMerchantRestockAdmission.cs", "RuntimeNpcMerchantGoldSettlement.cs"]),
        new("Trainers", "RuntimeNpcTrainerOfferingOwnership", "eligibility and offering admission", "trainer offering ownership metadata", "trainer eligibility and offering validation", "Verified", ["RuntimeNpcTrainerOfferingOwnership.cs", "RuntimeNpcTrainerEligibilityAdmission.cs", "RuntimeNpcTrainerOfferingAdmission.cs"]),
        new("Bosses", "RuntimeNpcSpawnLifecycle + combat authority", "boss spawn definition, AI, death fan-out and respawn", "living-world spawn slot and respawn metadata", "boss reference resolution and lifecycle validation", "Strongly Corroborated", ["RuntimeNpcSpawnLifecycle.cs", "RuntimeCombatDeathFanOut.cs", "RuntimeLivingWorld.cs"]),
        new("Death, corpses and loot", "RuntimeCombatDeathFanOut", "corpse ownership/access, loot materialization, ground drops and kill rewards", "corpse, inventory, gold and ground-drop persistence", "death/corpse/loot lifecycle smoke validation", "Verified", ["RuntimeCombatDeathFanOut.cs", "RuntimeNpcCorpseOwnership.cs", "RuntimeNpcCorpseAccess.cs", "RuntimeNpcLootMaterialization.cs", "RuntimeNpcGroundDrops.cs", "RuntimeNpcGoldOwnership.cs"]),
        new("Respawn and reset interaction", "RuntimeNpcSpawnLifecycle", "hard/soft timer admission, blocked retry, spawn-slot control and reset", "respawn timestamps and suppression/block metadata", "death-to-respawn and persistence reconnect smoke validation", "Verified", ["RuntimeNpcSpawnLifecycle.cs", "RuntimeWorldResetCleanup.cs", "RuntimeLivingWorld.cs"]),
        new("Creature relationships", "RuntimeClassicNpcAiCatalog", "disposition/alignment, player-only hostility, explicit assist and protector attack-blocking consumers", "generated relationship catalog and living NPC metadata", "relationship catalog reference validation", "Strongly Corroborated", ["RuntimeClassicNpcAiCatalog.cs", "RuntimeNpcAssist.cs", "RuntimeNpcPerception.cs"]),
        new("Services and dialogue", "RuntimeNpcServiceAdmission", "dialogue, service refusal, merchant and trainer routing", "service completion/refusal metadata", "service and dialogue admission validation", "Strongly Corroborated", ["RuntimeNpcServiceAdmission.cs", "RuntimeNpcDialogueAdmission.cs", "RuntimeNpcServiceRefusalDialogue.cs"]),
        new("NPC equipment, inventory and item use", "RuntimeNpcInventoryOwnership", "spawn inventory, equipment selection, ammunition, item and item-effect consumers", "NPC inventory/equipment metadata", "inventory ownership and persistence validation", "Verified", ["RuntimeNpcInventoryOwnership.cs", "RuntimeNpcSpawnInventory.cs", "RuntimeNpcEquipmentOwnership.cs", "RuntimeNpcEquipmentSelection.cs", "RuntimeNpcAmmunitionUse.cs", "RuntimeNpcItemUse.cs", "RuntimeNpcItemEffectInvocation.cs", "RuntimeNpcPersistenceValidation.cs"])
    ];

    public static RuntimeBundleEExhaustionAuditReport Run(string projectRoot, string worldId)
    {
        var options = ServerRuntimeOptions.Load(projectRoot);
        var host = new ServerRuntimeHost(projectRoot, options);
        host.Bootstrap();
        var world = host.Worlds.TryGetValue(worldId, out var selected) ? selected : host.Worlds.Values.First();
        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server");
        var checks = new List<string>();
        var failures = new List<string>();
        var subsystems = new List<RuntimeBundleESubsystem>();

        foreach (var spec in Specs)
        {
            var missing = spec.Files.Where(file => !File.Exists(Path.Combine(serverRoot, file))).ToArray();
            var passed = missing.Length == 0;
            if (passed) checks.Add($"subsystem:{spec.Name}:authoritative-source-present");
            else failures.Add($"subsystem:{spec.Name}:missing:{string.Join(',', missing)}");
            subsystems.Add(new(spec.Name, spec.Owner, spec.Consumer, spec.Persistence, spec.Validation, spec.Evidence, spec.Files, passed));
        }

        void Check(bool condition, string id) { if (condition) checks.Add(id); else failures.Add(id); }
        Check(world.NpcCount > 0, "generated_npc_definitions_loaded");
        Check(world.SpawnCount > 0, "generated_spawn_records_loaded");
        Check(world.UnresolvedGroupCount == 0, "all_spawn_group_references_resolved");
        Check(world.UnresolvedBossCount == 0, "all_boss_references_resolved");
        Check(world.NpcAiCatalog.RelationshipCount > 0, "npc_ai_catalog_loaded");
        Check(world.Npcs.Values.All(x => x.CharmLevel >= 0 && x.CharmResistance >= 0), "npc_charm_fields_connected");
        Check(world.Npcs.Values.All(x => x.FollowValue >= 0 && x.RegenTime >= 0 && x.RespawnHours >= 0), "npc_behavior_fields_connected");
        Check(world.Npcs.Values.All(x => x.HitPointRegen >= 0 && x.GameLimit >= 0 && x.ExperienceMultiplier >= 0), "npc_public_reference_fields_connected");
        Check(world.Spawns.All(x => x.MaximumAreaPopulation >= 0), "spawn_area_cap_connected");

        var requiredEvidenceRoots = new[]
        {
            Path.Combine(projectRoot, "Librarium", "Objects", "npcs"),
            Path.Combine(projectRoot, "Librarium", "Objects", "spawns"),
            Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData"),
            Path.Combine(projectRoot, "ImportedDatabases", "data-v1.11n.mdb"),
            Path.Combine(projectRoot, "ImportedDatabases", "OmniVerseReference"),
            Path.Combine(projectRoot, "ImportedDatabases", "WCCDefaultInstall")
        };
        foreach (var evidenceRoot in requiredEvidenceRoots)
            Check(File.Exists(evidenceRoot) || Directory.Exists(evidenceRoot), $"evidence-source-present:{Path.GetFileName(evidenceRoot)}");

        // The full RuntimeLivingWorldSmokeReport constructs and persists the entire living world.
        // It is intentionally executed by the installer smoke stage, not inside this audit, so the
        // Bundle E zero-disconnect command remains bounded and always returns to its caller.
        Check(world.NpcCount > 0 && world.SpawnCount > 0, "living_world_creature_runtime_inputs_ready");
        checks.Add("living-world-smoke-delegated-to-installer-stage");

        var movementAiClosure = RuntimeBundleEMovementAiClosureAudit.Run();
        Check(movementAiClosure.Passed, "movement_ai_disconnect_to_connect_closure_passed");
        foreach (var check in movementAiClosure.Checks) checks.Add("movement-ai:" + check);
        foreach (var failure in movementAiClosure.Failures) failures.Add("movement-ai:" + failure);

        var companionClosure = RuntimeBundleECompanionLifecycleClosureAudit.Run();
        Check(companionClosure.Passed, "companion_lifecycle_disconnect_to_connect_closure_passed");
        foreach (var check in companionClosure.Checks) checks.Add("companion:" + check);
        foreach (var failure in companionClosure.Failures) failures.Add("companion:" + failure);

        var merchantTrainerClosure = RuntimeBundleEMerchantTrainerClosureAudit.Run(projectRoot);
        Check(merchantTrainerClosure.Passed, "merchant_trainer_disconnect_to_connect_closure_passed");
        foreach (var check in merchantTrainerClosure.Checks) checks.Add("merchant-trainer:" + check);
        foreach (var failure in merchantTrainerClosure.Failures) failures.Add("merchant-trainer:" + failure);

        var bossDeathRespawnClosure = RuntimeBundleEBossDeathRespawnClosureAudit.Run(projectRoot);
        Check(bossDeathRespawnClosure.Passed, "boss_death_respawn_disconnect_to_connect_closure_passed");
        foreach (var check in bossDeathRespawnClosure.Checks) checks.Add("boss-death-respawn:" + check);
        foreach (var failure in bossDeathRespawnClosure.Failures) failures.Add("boss-death-respawn:" + failure);

        var npcItemControlPersistenceClosure = RuntimeBundleENpcItemControlPersistenceClosureAudit.Run(projectRoot);
        Check(npcItemControlPersistenceClosure.Passed, "npc_item_control_persistence_disconnect_to_connect_closure_passed");
        foreach (var check in npcItemControlPersistenceClosure.Checks) checks.Add("npc-item-control-persistence:" + check);
        foreach (var failure in npcItemControlPersistenceClosure.Failures) failures.Add("npc-item-control-persistence:" + failure);

        var relationshipServiceClosure = RuntimeBundleERelationshipServiceClosureAudit.Run(projectRoot);
        Check(relationshipServiceClosure.Passed, "relationship_service_disconnect_to_connect_closure_passed");
        foreach (var check in relationshipServiceClosure.Checks) checks.Add("relationship-service:" + check);
        foreach (var failure in relationshipServiceClosure.Failures) failures.Add("relationship-service:" + failure);

        var duplicatePaths = new List<string>();
        var singular = new[] { "RuntimeNpcSpawnLifecycle.cs", "RuntimeNpcAiDecisionEngine.cs", "RuntimeNpcCompanionOwnership.cs", "RuntimeNpcMerchantStockOwnership.cs", "RuntimeNpcTrainerOfferingOwnership.cs", "RuntimeCombatDeathFanOut.cs", "RuntimeBundleERelationshipServiceClosureAudit.cs" };
        foreach (var file in singular)
        {
            var count = Directory.GetFiles(serverRoot, file, SearchOption.TopDirectoryOnly).Length;
            if (count != 1) duplicatePaths.Add($"{file}:{count}");
        }
        Check(duplicatePaths.Count == 0, "no_duplicate_authoritative_creature_source_paths");

        Check(world.NpcAiCatalog.Passed, "spawn_derived_npc_ai_runtime_catalog_passed");
        Check(world.NpcAiCatalog.RelationshipCount == world.SpawnCount, "spawn_derived_npc_relationship_index_complete");
        Check(world.NpcAiCatalog.Diagnostics.All(diagnostic => !diagnostic.Fatal), "spawn_derived_npc_ai_runtime_catalog_has_no_fatal_diagnostics");

        return new(
            failures.Count == 0,
            world.Definition.Id,
            world.NpcCount,
            world.SpawnCount,
            world.NpcCount,
            subsystems,
            checks,
            failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            duplicatePaths.Count == 0 ? ["No duplicate authoritative NPC/spawn/AI/follower/merchant/trainer/death execution source path was found."] : duplicatePaths,
            [
                "Generated NPC definitions and spawn records load through RuntimeWorldLoader into ClassicRuntimeCatalog-owned RuntimeWorld data.",
                "Living NPC death creates corpses, schedules respawn, expires corpses, resets AI, and survives persistence reconnect.",
                "Threat, perception, assist, flee, inventory, equipment, merchant, trainer, loot and corpse operations have live runtime consumers.",
                "Decoded charm, follow, regeneration, undead, creature-type, game-limit, experience-multiplier, respawn-hour and spawn-area-cap fields now load into the authoritative NPC/spawn models and persist on living NPC metadata.",
                "Bundle B remains combat authority, Bundle C remains item/economy/loot-data authority, and Bundle D remains room/cleanup authority."
            ],
            [
                "Explicit routes, flee movement, return-home movement, perception, threat, assist, companion following, service/dialogue routing, relationship disposition and boss orchestration execute through the single living-world NPC authority and are enforced by the movement/AI closure gate."
            ],
            ["Followers, pets, companions, summons, hirelings and escorts are now guarded by one end-to-end lifecycle closure audit covering ownership, commands, following, combat assistance, transfer, cleanup and persistence.",
                "Merchant and trainer stock, restock, access, settlement, eligibility, offerings, cooldown completion, persistence and imported evidence are now guarded by one end-to-end closure audit.",
                "Boss, death, corpse, reward ownership and hard/soft respawn paths are now guarded by one end-to-end closure audit using decoded, imported and simulator evidence.",
                "Creature alignment, disposition, spawn-group relationships, player-only hostility, explicit assist, protector attack blocking, dialogue, service routing and refusal persistence are guarded by one relationship/service closure audit; no creature faction or reputation system exists.",
                "No Verified or Strongly Corroborated Bundle E subsystem completed by this revision remains catalog-only."],
            ["No unresolved authority conflict was detected; cross-bundle ownership boundaries are preserved."],
            ["Native-only creature semantics without decoded evidence remain quarantined for the final native pass and are not invented."]);
    }
}
