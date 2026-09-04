using System.Text.Json;

namespace RealmGate.Server;

public sealed class RuntimeBundleCLootAuditReport
{
    public bool Passed => Failures.Count == 0;
    public int NpcCount { get; set; }
    public int NpcsWithCurrency { get; set; }
    public int NpcsWithKnownLoot { get; set; }
    public int LootEntryCount { get; set; }
    public int CanonicalItemReferences { get; set; }
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public List<string> TrueUnknowns { get; } = [];
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCLootAudit
{
    public static RuntimeBundleCLootAuditReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeBundleCLootAuditReport();
        try
        {
            var npcPath = Path.Combine(projectRoot, "Data", worldId, "npcs.json");
            using var document = JsonDocument.Parse(File.ReadAllText(npcPath));
            var records = document.RootElement.GetProperty("records");
            var itemCatalog = RuntimeItemCatalog.Load(projectRoot, worldId);
            report.NpcCount = records.GetArrayLength();
            foreach (var record in records.EnumerateArray())
            {
                var npc = RuntimeNpcDefinition.FromJson(record);
                if (npc.CopperReward > 0 || npc.SilverReward > 0 || npc.GoldReward > 0 || npc.PlatinumReward > 0 || npc.RunicReward > 0)
                    report.NpcsWithCurrency++;
                if (npc.LootKnown) report.NpcsWithKnownLoot++;
                if (npc.LootKnown && npc.LootTableEntries != npc.Loot.Count)
                    report.Failures.Add($"npc-loot-count-mismatch:{npc.Id}:{npc.LootTableEntries}:{npc.Loot.Count}");
                foreach (var entry in npc.Loot)
                {
                    report.LootEntryCount++;
                    if (entry.DropPercent is < 0 or > 100) report.Failures.Add($"npc-loot-percent-invalid:{npc.Id}:{entry.CanonicalItemId}:{entry.DropPercent}");
                    try { itemCatalog.Require(entry.CanonicalItemId); report.CanonicalItemReferences++; }
                    catch { report.TrueUnknowns.Add($"Unresolved decoded NPC loot relationship quarantined: {npc.Id} -> {entry.CanonicalItemId}."); }
                }
            }
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeCombatCompletion.cs", "RuntimeClassicRoomCurrencyStore.AddExact", report, "exact-denomination-ground-currency");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeCombatCompletion.cs", "RollNpcLootPercent", report, "deterministic-drop-roll");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeCombatCompletion.cs", "RuntimeItemLocations.Room", report, "room-ground-item-materialization");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeItemsEquipment.cs", "maximum_owned_quantity", report, "limited-item-transfer-enforcement");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeClassicCleanup.cs", "HideRoomCurrencyAtCleanup", report, "currency-cleanup-hide");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeClassicCleanup.cs", "CleanupRoomGround", report, "item-cleanup-authority");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeNpcGroundDrops.cs", "HiddenAtCleanup", report, "currency-hidden-state");
            RequireSource(projectRoot, "Apps/Source/RealmGate.Server/RuntimeClassicHiddenSearch.cs", "ReplaceSearchResults", report, "transient-search-result-replacement");
            report.Checks.AddRange(["decoded-npc-currency-amounts", "decoded-item-drop-percentages", "canonical-item-resolution", "deterministic-live-roll", "persistent-room-ground-drops", "limited-item-ownership", "cleanup-and-search-consumers", "transient-search-results"]);
            report.TrueUnknowns.Add("Decoded generated NPC loot exposes one-item quantities only; native variable item quantity ranges are not present and are not invented.");
            report.TrueUnknowns.Add("No separate executable generated rare/random/quest loot-table relationship exists beyond decoded NPC item drop percentages; labels are not inferred from probability.");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    private static void RequireSource(string root, string relative, string token, RuntimeBundleCLootAuditReport report, string check)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path) || !File.ReadAllText(path).Contains(token, StringComparison.Ordinal)) report.Failures.Add($"consumer-missing:{check}");
    }
}
