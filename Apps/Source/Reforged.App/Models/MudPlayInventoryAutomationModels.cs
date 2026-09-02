using System.Collections.ObjectModel;

namespace Reforged.App.Models;

public enum MudPlayInventoryDisposition
{
    Keep,
    Sell,
    Stash,
    Discard,
    Buy
}

public enum MudPlayInventoryRuleMatchMode
{
    ExactName,
    NameContains,
    ItemId,
    Slot,
    ItemType
}

public sealed class MudPlayInventoryRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public MudPlayInventoryDisposition Disposition { get; set; } = MudPlayInventoryDisposition.Keep;
    public MudPlayInventoryRuleMatchMode MatchMode { get; set; } = MudPlayInventoryRuleMatchMode.ExactName;
    public string MatchValue { get; set; } = string.Empty;
    public int TargetQuantity { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;

    public string DisplayName => $"{Priority:0000} • {Disposition} • {MatchMode}: {MatchValue}";
}

public sealed class MudPlayEquipmentSetEntry
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public int Order { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Slot) ? ItemName : $"{Slot} • {ItemName}";
}

public sealed class MudPlayEquipmentSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
    public bool Enabled { get; set; } = true;
    public bool AutoApply { get; set; }
    public ObservableCollection<MudPlayEquipmentSetEntry> Items { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string DisplayName => $"{(Enabled ? "✓" : "—")} {Name} • {Items.Count} item(s)";
}

public sealed class MudPlayInventoryAutomationPreferences
{
    public bool EnableAutomaticInventoryRules { get; set; }
    public bool AutoApplyEquipmentSet { get; set; }
    public string AutoEquipmentSetId { get; set; } = string.Empty;
    public bool AutoReequipAfterInventoryChange { get; set; }
    public bool AllowAutomaticSell { get; set; }
    public bool AllowAutomaticStash { get; set; }
    public bool AllowAutomaticDiscard { get; set; }
    public bool AllowAutomaticBuy { get; set; }
    public bool AutoDepositExcessCurrency { get; set; }
    public long KeepCurrency { get; set; }
    public string DepositAllCommand { get; set; } = "deposit all";
    public string DepositExcessCommandTemplate { get; set; } = string.Empty;
    public string SellCommandTemplate { get; set; } = "sell {item}";
    public string StashCommandTemplate { get; set; } = "stash {item}";
    public string DiscardCommandTemplate { get; set; } = "drop {item}";
    public string BuyCommandTemplate { get; set; } = "buy {item} {quantity}";
    public string RemoveCommandTemplate { get; set; } = "remove {item}";
    public string EquipCommandTemplate { get; set; } = "equip {item}";
    public string RecoveryCorpseCommand { get; set; } = "get all corpse";
    public string RecoveryCorpseNameContains { get; set; } = string.Empty;
    public int StateMaximumAgeSeconds { get; set; } = 30;
    public int CommandCooldownSeconds { get; set; } = 5;
    public int MaximumCommandsPerPass { get; set; } = 40;
}

public sealed class MudPlayInventoryAutomationProfile
{
    public int Version { get; set; } = 1;
    public string ProfileKey { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string ServerProfileName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public MudPlayInventoryAutomationPreferences Preferences { get; set; } = new();
    public ObservableCollection<MudPlayEquipmentSet> EquipmentSets { get; set; } = [];
    public ObservableCollection<MudPlayInventoryRule> Rules { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record MudPlayInventoryObservedItem(
    string DisplayText,
    string Name,
    string ItemId,
    string Slot,
    string ItemType,
    int Quantity,
    bool Equipped);

public sealed class MudPlayInventoryAutomationPlan
{
    public string Action { get; init; } = string.Empty;
    public IReadOnlyList<string> Commands { get; init; } = [];
    public IReadOnlyList<string> MissingItems { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string Fingerprint { get; init; } = string.Empty;
    public bool RequiresInventoryRefresh { get; init; }
    public bool RequiresEquipmentRefresh { get; init; }
    public bool HasCommands => Commands.Count > 0;

    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Action)) parts.Add(Action);
            if (Commands.Count > 0) parts.Add($"{Commands.Count} command(s)");
            if (MissingItems.Count > 0) parts.Add($"{MissingItems.Count} missing item(s)");
            if (Warnings.Count > 0) parts.Add($"{Warnings.Count} warning(s)");
            return parts.Count == 0 ? "No action required." : string.Join(" • ", parts);
        }
    }
}
