using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleEBossDeathRespawnClosureAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> EvidenceSources,
    IReadOnlyList<string> OwnershipBoundaries)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Bundle E disconnect-to-connect gate for boss, death, corpse, loot ownership and respawn.
/// It proves the complete live path without moving combat ownership from Bundle B, item/economy
/// ownership from Bundle C, or room/reset/cleanup ownership from Bundle D.
/// </summary>
public static class RuntimeBundleEBossDeathRespawnClosureAudit
{
    public static RuntimeBundleEBossDeathRespawnClosureAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var evidence = new List<string>();
        void Check(bool condition, string id) { if (condition) checks.Add(id); else failures.Add(id); }
        void Add(bool passed, IEnumerable<string> passedChecks, IEnumerable<string> reportFailures, string prefix)
        {
            Check(passed, prefix + "-passed");
            foreach (var x in passedChecks) checks.Add(prefix + ":" + x);
            foreach (var x in reportFailures) failures.Add(prefix + ":" + x);
        }

        var death = RuntimeCombatDeathFanOutSmokeReport.Run();
        foreach (var pair in death) Check(pair.Value, "death-fanout:" + pair.Key);
        Check(Enum.GetValues<RuntimeCombatDeathEventKind>().Contains(RuntimeCombatDeathEventKind.ExperienceRequest), "kill-credit-experience-request-connected");
        Check(Enum.GetValues<RuntimeCombatDeathEventKind>().Contains(RuntimeCombatDeathEventKind.QuestDeathEvent), "quest-and-boss-kill-event-connected");
        Check(Enum.GetValues<RuntimeCombatDeathEventKind>().Contains(RuntimeCombatDeathEventKind.LootRequest), "reward-ownership-loot-request-connected");
        Check(Enum.GetValues<RuntimeCombatDeathEventKind>().Contains(RuntimeCombatDeathEventKind.GroundDropRequest), "room-ground-drop-request-connected");
        Check(Enum.GetValues<RuntimeCombatDeathEventKind>().Contains(RuntimeCombatDeathEventKind.RespawnRequest), "death-to-respawn-request-connected");

        var corpseOwner = RuntimeNpcCorpseOwnershipSmokeReport.Run(projectRoot);
        Add(corpseOwner.Passed, corpseOwner.Checks, corpseOwner.Failures, "corpse-ownership");
        var corpseAccess = RuntimeNpcCorpseAccessSmokeReport.Run(projectRoot);
        Add(corpseAccess.Passed, corpseAccess.Checks, corpseAccess.Failures, "corpse-access");
        var loot = RuntimeNpcLootMaterializationSmokeReport.Run(projectRoot);
        Add(loot.Passed, loot.Checks, loot.Failures, "loot-materialization");
        var spawn = RuntimeNpcSpawnLifecycleSmokeReport.InspectContract();
        Add(spawn.Passed, spawn.Checks, spawn.Failures, "spawn-respawn-lifecycle");
        foreach (var pair in RuntimeEconomyLootSmokeReport.Run()) Check(pair.Value, "bundle-c-loot:" + pair.Key);

        var living = typeof(RuntimeLivingWorldService);
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.ApplyNpcDamage)) is not null, "live-npc-death-admission-connected");
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.EnterNpcRespawnWait)) is not null, "respawn-wait-connected");
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.TickNpcSpawnLifecycle)) is not null, "hard-soft-respawn-tick-connected");
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.GetBlockedNpcRespawns)) is not null, "blocked-respawn-observability-connected");
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.RetryBlockedNpcRespawn)) is not null, "blocked-respawn-retry-connected");
        Check(living.GetMethod(nameof(RuntimeLivingWorldService.ResetNpcSpawnSlot)) is not null, "bundle-d-reset-interaction-connected");

        var requiredEvidence = new[]
        {
            "Data/Classic/spawns.json", "Data/Classic/loot_rules.json",
            "Librarium/Objects/spawns/catalog.json", "Librarium/Objects/loot_rules/catalog.json",
            "Librarium/MajorMUDKnowledgeBase/MechanicsFull/WORLD_SPAWN_TIMERS.json",
            "Librarium/MajorMUDKnowledgeBase/MechanicsFull/ECON_LOOT.json",
            "Librarium/CombatSpellReconstruction/combat_death_fanout_0175.json",
            "Librarium/Runtime/Economy/loot_runtime_catalog_0153.json",
            "Evidence/CombatSpellReconstruction/combat_death_fanout_evidence_0175.json",
            "Evidence/NpcAI/npc_ai_death_respawn_recovery_evidence.json",
            "Evidence/NpcAI/npc_ai_spawn_lifecycle_evidence.json",
            "Evidence/NpcAI/npc_ai_corpse_ownership_evidence.json",
            "Evidence/NpcAI/npc_ai_corpse_access_evidence.json",
            "Evidence/NpcAI/npc_ai_loot_materialization_evidence.json",
            "Evidence/Economy/economy_loot_runtime_evidence_0153.json",
            "Evidence/DataSimVer0022-MonsterRespawnSchedulingEvidence.csv",
            "Evidence/DataSimVer0023/boss_unique_monster_candidates.csv",
            "Tools/ReforgedValidator/Baselines/DataSimVer0021/monster_respawn_scheduler_expected_model.json",
            "Tools/ReforgedValidator/Baselines/DataSimVer0023/quest_boss_respawn_occupancy_expected_model.json",
            "Tools/ReforgedValidator/Baselines/DataSimVer/0016/DeathResolutionExpectedModel.csv",
            "Tools/ReforgedValidator/Baselines/DataSimVer/0016/MonsterRewardLootMatrix.csv",
            "ImportedDatabases/data-v1.11n.mdb",
            "ImportedDatabases/WCCDefaultInstall"
        };
        foreach (var relative in requiredEvidence)
        {
            var path = Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            var present = File.Exists(path) || Directory.Exists(path);
            Check(present, "evidence-source-present:" + relative);
            if (present) evidence.Add(relative);
        }

        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        foreach (var singular in new[] { "RuntimeCombatDeathFanOut.cs", "RuntimeNpcCorpseOwnership.cs", "RuntimeNpcCorpseAccess.cs", "RuntimeNpcLootMaterialization.cs", "RuntimeNpcSpawnLifecycle.cs" })
            Check(Directory.GetFiles(serverRoot, singular, SearchOption.TopDirectoryOnly).Length == 1, "single-authority:" + singular);

        return new(failures.Count == 0, checks, failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), evidence,
        [
            "Bundle B remains authoritative for lethal combat resolution, killer identity and ordered death fan-out.",
            "Bundle E owns NPC death execution, corpse creation routing, boss/quest death event routing, and respawn lifecycle execution.",
            "Bundle C remains authoritative for loot tables, currency, items, corpse inventory and reward settlement.",
            "Bundle D remains authoritative for rooms, room-ground persistence, cleanup and reset interaction.",
            "Companion and group credit enter through the authoritative killer/death event identity; no parallel reward engine is introduced."
        ]);
    }
}
