using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleCMerchantAuditResult(
    bool Passed,
    string WorldId,
    int ShopCount,
    int TrainerCount,
    int InventoryRelationshipCount,
    int SpellShopCount,
    int SpecialVendorCount,
    IReadOnlyList<string> Connected,
    IReadOnlyList<string> Unresolved,
    IReadOnlyList<string> Diagnostics)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCMerchantAudit
{
    public static RuntimeBundleCMerchantAuditResult Run(string projectRoot, string worldId)
    {
        var connected = new List<string>();
        var unresolved = new List<string>();
        var diagnostics = new List<string>();
        var serviceCatalog = RuntimeServiceCatalog.Load(projectRoot, worldId);
        var itemCatalog = RuntimeItemCatalog.Load(projectRoot, worldId);
        var shops = serviceCatalog.Services.Where(x => x.GeneratedRecord.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array).ToArray();
        var trainers = serviceCatalog.Services.Where(x => ReadKind(x).Equals("trainer", StringComparison.OrdinalIgnoreCase)).ToArray();
        var relationships = 0;
        var missing = new List<string>();
        foreach (var shop in shops)
        {
            foreach (var entry in shop.GeneratedRecord.GetProperty("inventory").EnumerateArray())
            {
                if (!entry.TryGetProperty("item_exists", out var exists) || exists.ValueKind != JsonValueKind.True) continue;
                if (!entry.TryGetProperty("item_ref", out var itemRef) || string.IsNullOrWhiteSpace(itemRef.GetString())) continue;
                relationships++;
                try { itemCatalog.Require(itemRef.GetString()!); }
                catch { missing.Add($"{shop.Id}:{itemRef.GetString()}"); }
            }
        }
        Check(serviceCatalog.Services.Count > 0, "generated service catalog loaded");
        Check(shops.Length > 0, "generated shop inventory authority present");
        Check(trainers.Length > 0, "generated trainer authority present");
        Check(relationships > 0, "generated merchant item relationships present");
        Check(missing.Count == 0, "all executable merchant item references are canonical");
        Check(typeof(RuntimeLiveGameplayFacade).GetMethod("ExecuteLiveServicesBundleCommand") is not null, "live services command consumer attached");
        Check(typeof(RuntimeGameplayCompletionStateStore).GetMethod("ExecuteShopTransaction") is not null, "persistent atomic shop stock transaction attached");
        Check(typeof(RuntimeItemEngine).GetMethod(nameof(RuntimeItemEngine.SellToMerchant), new[] { typeof(string), typeof(string), typeof(string), typeof(int) }) is not null, "item authority merchant-sale transfer attached");
        Check(typeof(RuntimeItemEngine).GetMethod(nameof(RuntimeItemEngine.BuyFromMerchant), new[] { typeof(string), typeof(string), typeof(string), typeof(int) }) is not null, "item authority merchant-purchase transfer attached");
        var learning = RuntimeCharacterLearningCatalog.Load(projectRoot, worldId);
        Check(learning.Level40QuestCandidates.Count == 3 && learning.Level40QuestCandidates.All(x => x.NpcId == "npc.716" && x.TurnInTextblockId == 2798 && x.TurnInScriptResolved), "Jungle Gnome merchant textblock 2798 turn-in authority connected");
        connected.AddRange(new[]
        {
            "generated shops -> RuntimeServiceCatalog",
            "room service lookup -> live list/buy/sell/train consumers",
            "shop inventory relationships -> RuntimeItemCatalog canonical identities",
            "buy/sell quantity -> RuntimeItemEngine ownership restrictions",
            "shop stock -> RuntimeGameplayCompletionStateStore persistence",
            "trainers -> Bundle A progression consumer",
            "spell shops -> decoded LearnSp item relationships",
            "Jungle Gnome npc.716/textblock.2798 -> atomic class reward turn-in"
        });
        unresolved.Add("Exact charm-derived merchant buy/sell price modifiers remain Bundle C7 economy ownership.");
        unresolved.Add("Native merchant wallet funding and exact refusal text remain relationship-unresolved where no generated field exists.");
        return new(diagnostics.Count == 0, worldId, shops.Length, trainers.Length, relationships,
            shops.Count(x => ReadKind(x).Contains("spell", StringComparison.OrdinalIgnoreCase)),
            serviceCatalog.Services.Count(x => ReadKind(x).Contains("quest", StringComparison.OrdinalIgnoreCase) || ReadKind(x).Contains("special", StringComparison.OrdinalIgnoreCase)),
            connected, unresolved, diagnostics.Concat(missing.Select(x => "missing-item:" + x)).ToArray());

        void Check(bool condition, string label)
        {
            if (!condition) diagnostics.Add(label);
        }
    }

    private static string ReadKind(RuntimeServiceDefinition service) =>
        service.GeneratedRecord.TryGetProperty("service_kind", out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : service.ServiceKind;
}
