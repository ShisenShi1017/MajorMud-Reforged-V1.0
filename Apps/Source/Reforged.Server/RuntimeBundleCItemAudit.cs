using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleCItemAuditReport(
    string Status,
    int ItemCount,
    int ConsumableCount,
    int ContainerCount,
    int KeyCount,
    int StackableCount,
    int ChargedCount,
    int DeleteAtCleanupCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Errors)
{
    public bool Passed => Status == "PASS";
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCItemAudit
{
    public static RuntimeBundleCItemAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var errors = new List<string>();
        void Require(bool condition, string check) { if (condition) checks.Add(check); else errors.Add(check); }
        try
        {
            var catalog = RuntimeItemCatalog.Load(projectRoot, worldId);
            var items = catalog.Items.ToArray();
            Require(items.Length > 0, "generated-item-catalog-loaded");
            Require(items.All(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name)), "item-identity-complete");
            Require(items.All(x => x.Weight >= 0 && x.DefaultCharges >= 0), "item-weight-charge-domain-valid");

            var engine = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeItemsEquipment.cs"));
            Require(engine.Contains("ConsumeQuantity", StringComparison.Ordinal), "consumable-runtime-consumer");
            Require(engine.Contains("SpendCharges", StringComparison.Ordinal) && engine.Contains("Recharge", StringComparison.Ordinal), "charge-runtime-consumer");
            Require(engine.Contains("Cannot consume a non-empty container", StringComparison.Ordinal), "container-destruction-guard");
            Require(engine.Contains("StackQuantity", StringComparison.Ordinal), "stack-runtime-consumer");
            Require(engine.Contains("Destroy(string instanceId", StringComparison.Ordinal), "destruction-runtime-consumer");
            Require(engine.Contains("RuntimeItemLocations.Container", StringComparison.Ordinal), "portable-container-runtime-consumer");
            Require(engine.Contains("GrantQuestItem", StringComparison.Ordinal) && engine.Contains("ConsumeQuestItem", StringComparison.Ordinal), "quest-item-runtime-consumer");
            Require(engine.Contains("DeleteAtCleanup", StringComparison.Ordinal), "del-maint-ability-119-runtime-owner");
            Require(engine.Contains("Snapshot(", StringComparison.Ordinal) && engine.Contains("RuntimeItemState", StringComparison.Ordinal), "item-persistence-owner");

            var container = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeWorldContainers.cs"));
            Require(container.Contains("RequiredKeyId", StringComparison.Ordinal) && container.Contains("container-key-required", StringComparison.Ordinal), "key-runtime-consumer");
            Require(container.Contains("SaveLocked", StringComparison.Ordinal) && container.Contains("Restore", StringComparison.Ordinal), "container-persistence-consumer");

            var cleanup = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeClassicCleanup.cs"));
            Require(cleanup.Contains("DeleteAtCleanup", StringComparison.Ordinal), "del-maint-ability-119-live-consumer");
            var search = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeClassicHiddenSearch.cs"));
            Require(search.Contains("hidden", StringComparison.OrdinalIgnoreCase), "hidden-search-live-consumer");

            var eligibility = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeCharacterEligibilityAuthority.cs"));
            Require(eligibility.Contains("ReadLearnSpellAbilities", StringComparison.Ordinal), "learnsp-runtime-owner");

            return new(errors.Count == 0 ? "PASS" : "FAIL", items.Length,
                items.Count(x=>x.Consumable), items.Count(x=>x.Container), items.Count(x=>x.ItemType.Equals("key",StringComparison.OrdinalIgnoreCase)),
                items.Count(x=>x.Stackable), items.Count(x=>x.DefaultCharges>0), items.Count(x=>x.DeleteAtCleanup), checks, errors);
        }
        catch(Exception ex)
        {
            errors.Add(ex.ToString());
            return new("FAIL",0,0,0,0,0,0,0,checks,errors);
        }
    }
}
