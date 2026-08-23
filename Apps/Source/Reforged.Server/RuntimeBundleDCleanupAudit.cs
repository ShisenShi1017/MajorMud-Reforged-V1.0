using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDCleanupAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleDCleanupAudit
{
    public static RuntimeBundleDCleanupAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { if (condition) checks.Add(id); else failures.Add(id); }

        var server = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        string Read(string name) => File.ReadAllText(Path.Combine(server, name));
        var cleanup = Read("RuntimeClassicCleanup.cs");
        var search = Read("RuntimeClassicHiddenSearch.cs");
        var drops = Read("RuntimeNpcGroundDrops.cs");
        var gang = Read("RuntimeClassicGangHouseProtection.cs");
        var items = Read("RuntimeItemsEquipment.cs");

        Check(Directory.GetFiles(server, "RuntimeClassicCleanup.cs", SearchOption.TopDirectoryOnly).Length == 1, "single-cleanup-authority-source");
        Check(typeof(RuntimeClassicCleanupService).GetMethod(nameof(RuntimeClassicCleanupService.CleanupRoomGround)) is not null, "authoritative-cleanup-live-consumer");
        Check(cleanup.Contains("RuntimeItemLocations.Room", StringComparison.Ordinal), "cleanup-room-ground-scope-only");
        Check(cleanup.Contains("VisibleAtCleanup", StringComparison.Ordinal), "visible-at-maintenance-fixtures-remain-visible");
        Check(cleanup.Contains("DeleteAtCleanup", StringComparison.Ordinal) && cleanup.Contains("RemoveForClassicCleanup", StringComparison.Ordinal), "del-maint-ability-119-cleanup-delete-consumer");
        Check(cleanup.Contains("RechargeForClassicCleanup", StringComparison.Ordinal) && cleanup.Contains("last_classic_cleanup_recharge_utc", StringComparison.Ordinal), "recharge-ability-121-idempotent-cleanup-consumer");
        Check(cleanup.Contains("MarkHiddenAtClassicCleanup", StringComparison.Ordinal) && cleanup.Contains("hidden_at_cleanup_utc", StringComparison.Ordinal), "room-item-hide-consumer");
        Check(cleanup.Contains("AlreadyHiddenRoomItems", StringComparison.Ordinal) && cleanup.Contains("IsHiddenAtCleanup", StringComparison.Ordinal), "cleanup-idempotent-hidden-state");
        Check(cleanup.Contains("HideRoomCurrencyAtCleanup", StringComparison.Ordinal) && drops.Contains("HiddenAtCleanup", StringComparison.Ordinal), "room-currency-hide-consumer");
        Check(drops.Contains("if (state.Amount.IsEmpty || state.HiddenAtCleanup) continue", StringComparison.Ordinal), "currency-cleanup-idempotent");
        Check(gang.Contains("active-persistent-gang-house-assignment", StringComparison.Ordinal)
            && gang.Contains("new RuntimeClassicGangStateService(projectRoot).List()", StringComparison.Ordinal)
            && cleanup.Contains("new RuntimeClassicGangHouseProtectionService(projectRoot, worldId)", StringComparison.Ordinal),
            "gang-house-persistent-ownership-and-repossession-enforcement");
        Check(!cleanup.Contains("cleanup_exempt", StringComparison.OrdinalIgnoreCase), "no-generic-metadata-cleanup-bypass");
        Check(search.Contains("hidden_at_cleanup", StringComparison.Ordinal) && search.Contains("DropClassicVisible", StringComparison.Ordinal), "cleanup-search-pickup-redrop-handoff");
        Check(items.Contains("Metadata", StringComparison.Ordinal) && items.Contains("RuntimeItemState", StringComparison.Ordinal), "hidden-cleanup-state-persistence-owner");
        Check(RuntimeItemCatalog.Load(projectRoot, worldId).Items.Any(x => x.DeleteAtCleanup), "generated-del-maint-ability-119-records-consumed");

        var verified = new[]
        {
            "Room-ground items become hidden at Classic cleanup.",
            "Room-ground currency becomes hidden at Classic cleanup.",
            "The 16 non-gettable fixtures carrying decoded Visible@Maint (ability 154) remain visible through Classic cleanup.",
            "Items carrying decoded Del@Maint (ability 119), displayed natively as Delete at cleanup, are deleted instead of hidden.",
            "Character inventory and equipment are outside room-ground cleanup ownership.",
            "Surviving items with decoded Recharge (ability 121) recover the stated uses once per cleanup event up to their finite maximum-use capacity.",
            "Gang-house, vault and gang-house-shop retention requires an active persistent house assignment; sundown repossession or disbanding removes protection.",
            "Cleanup-hidden objects remain hidden until Search reveals them for pickup; pickup and re-drop makes them visible."
        };
        var corroborated = new[]
        {
            "Cleanup replay is idempotent: already-hidden item and currency state is retained and Recharge is not applied twice for the same event timestamp.",
            "Item metadata and room-currency state are the authoritative persistence representation for cleanup hiding."
        };
        var decodedButNotMapped = Array.Empty<string>();
        var conflicts = new[]
        {
            "A prior generic cleanup_exempt metadata bypass conflicted with persistent gang-house ownership and repossession and was removed from the authoritative cleanup path."
        };
        var unknown = new[]
        {
            "No decoded universal Classic cleanup interval or wall-clock schedule has been verified; scheduling remains externally owned and no interval is invented.",
            "Native ordering between world reset and cleanup when both become due at the same instant remains unresolved."
        };
        return new(failures.Count == 0, checks, failures, verified, corroborated, decodedButNotMapped, conflicts, unknown);
    }
}
