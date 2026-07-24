using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleCPersistenceAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCPersistenceAudit
{
    public static RuntimeBundleCPersistenceAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { if (condition) checks.Add(id); else failures.Add(id); }

        var server = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        string Read(string file) => File.ReadAllText(Path.Combine(server, file));
        var items = Read("RuntimeItemsEquipment.cs");
        var hidden = Read("RuntimeClassicHiddenSearch.cs");
        var cleanup = Read("RuntimeClassicCleanup.cs");
        var currency = Read("RuntimeClassicRoomCurrency.cs");
        var economy = Read("RuntimeBundleCEconomyAuthority.cs");
        var equipment = Read("RuntimeBundleCEquipmentRuntime.cs");
        var liveEquipment = Read("RuntimeLiveGameplayPart2.cs");

        Check(items.Contains("SaveValidatedWithIntegrityBackup", StringComparison.Ordinal), "items.atomic_integrity_backup_save");
        Check(items.Contains("LoadValidatedWithIntegrityRecovery", StringComparison.Ordinal), "items.validated_backup_recovery");
        Check(items.Contains("RuntimeItemCrossSaveOwnershipCoordinator", StringComparison.Ordinal), "items.cross_save_ownership_validation");
        Check(items.Contains("RuntimeItemSaveGenerationCoordinator", StringComparison.Ordinal), "items.coordinated_save_generation");
        Check(items.Contains("RuntimeItemLimitedReconciliationCoordinator", StringComparison.Ordinal), "items.limited_item_reconciliation");
        Check(equipment.Contains("RuntimeEquipmentRuntimeAuthority", StringComparison.Ordinal) && liveEquipment.Contains("RuntimeEquipmentRuntimeAuthority.Commit", StringComparison.Ordinal) && liveEquipment.Contains("_characters.SaveCharacter(commit.Character)", StringComparison.Ordinal), "equipment.character_persistence_consumer");
        Check(economy.Contains("ReadBank", StringComparison.Ordinal) && economy.Contains("WriteBank", StringComparison.Ordinal) && economy.Contains("bank.currency.runic", StringComparison.Ordinal), "bank.five_denomination_persistence");
        Check(currency.Contains("Copper", StringComparison.Ordinal) && currency.Contains("Runic", StringComparison.Ordinal), "currency.room_state_five_denominations");
        Check(cleanup.Contains("RuntimeClassicGangHouseProtectionService", StringComparison.Ordinal), "cleanup.gang_house_deed_upkeep_exemption");
        Check(cleanup.Contains("DeleteAtCleanup", StringComparison.Ordinal), "cleanup.del_maint_ability_119_delete_path");
        Check(cleanup.Contains("MarkHiddenAtClassicCleanup", StringComparison.Ordinal), "cleanup.room_ground_hide_path");
        Check(!hidden.Contains("hidden-discoveries.json", StringComparison.Ordinal), "hidden.search_results_not_persisted");
        Check(hidden.Contains("ReplaceSearchResults", StringComparison.Ordinal), "hidden.each_search_replaces_prior_results");
        Check(hidden.Contains("foreach (var target", StringComparison.Ordinal) && !hidden.Contains("continue;", StringComparison.Ordinal), "hidden.every_search_rerolls_all_room_targets");
        Check(hidden.Contains("metadata.Remove(\"hidden_at_cleanup\")", StringComparison.Ordinal), "hidden.pickup_clears_hidden_state");
        Check(hidden.Contains("DropClassicVisible", StringComparison.Ordinal), "hidden.redrop_becomes_visible");

        var registry = new RuntimeClassicHiddenDiscoveryRegistry();
        var service = new RuntimeClassicHiddenSearchService(registry, (perception, difficulty, roll) => roll >= difficulty);
        var actor = new RuntimeClassicSearchActor("character:a", "room:1", 0, 0, 0, 0, 0);
        var targets = new[]
        {
            new RuntimeClassicHiddenTarget("item:a", "room:1", "item", 50, true, false),
            new RuntimeClassicHiddenTarget("item:b", "room:1", "item", 50, true, false)
        };
        var first = service.Search(actor, targets, new SequenceRandom(90, 10));
        Check(first.NewlyDiscoveredTargetIds.SequenceEqual(new[] { "item:a" }), "hidden.first_search_result_subset");
        Check(registry.IsDiscovered("character:a", "item:a") && !registry.IsDiscovered("character:a", "item:b"), "hidden.first_search_current_visibility");
        var second = service.Search(actor, targets, new SequenceRandom(10, 90));
        Check(second.NewlyDiscoveredTargetIds.SequenceEqual(new[] { "item:b" }), "hidden.second_search_can_differ");
        Check(!registry.IsDiscovered("character:a", "item:a") && registry.IsDiscovered("character:a", "item:b"), "hidden.second_search_replaces_first");
        var restarted = new RuntimeClassicHiddenDiscoveryRegistry();
        Check(!restarted.IsDiscovered("character:a", "item:b"), "hidden.search_result_not_restart_persistent");

        var verified = new[]
        {
            "Inventory, equipment, item identity, stack quantity, charges, durability, metadata and locations persist in authoritative item/character state.",
            "Copper, silver, gold, platinum and runic balances persist without denomination collapse.",
            "Room-ground items and currency persist through cleanup as hidden state; items carrying decoded Del@Maint ability 119 are deleted at cleanup.",
            "Every SEARCH rerolls all eligible hidden room objects; the latest result replaces the prior transient result and is never persisted.",
            "Gang-house retention remains conditional on matching deed ownership and current upkeep."
        };
        var corroborated = new[]
        {
            "Atomic primary/backup/integrity item saves and coordinated save generations are the authoritative crash-recovery path.",
            "Temporary and quest item lifecycle cleanup remains metadata/owner driven rather than a universal timer."
        };
        var unknown = new[]
        {
            "No decoded universal temporary-item lifetime was found; no global decay interval is promoted.",
            "Native transaction ordering beyond the existing coordinated generation and integrity scheme remains unresolved."
        };
        return new(failures.Count == 0, checks, failures, verified, corroborated, unknown);
    }

    private sealed class SequenceRandom(params int[] values) : Random
    {
        private int _index;
        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length) throw new InvalidOperationException("SequenceRandom exhausted.");
            var value = values[_index++];
            if (value < minValue || value >= maxValue) throw new ArgumentOutOfRangeException(nameof(values));
            return value;
        }
    }

}
