using System.Collections.ObjectModel;

namespace RealmGate.Client.Inventory;

public enum ClientInventoryDisposition
{
    Keep,
    Sell,
    Stash,
    Discard,
    Buy
}

public enum ClientInventoryRuleMatchMode
{
    ExactName,
    NameContains,
    ItemId,
    Slot,
    ItemType
}

public sealed class ClientInventoryRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public ClientInventoryDisposition Disposition { get; set; } = ClientInventoryDisposition.Keep;
    public ClientInventoryRuleMatchMode MatchMode { get; set; } = ClientInventoryRuleMatchMode.ExactName;
    public string MatchValue { get; set; } = string.Empty;
    public int TargetQuantity { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;
    public string DisplayName => $"{Priority:0000} - {Disposition} - {MatchMode}: {MatchValue}";
}

public sealed class ClientEquipmentSetEntry
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public int Order { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(Slot) ? ItemName : $"{Slot} - {ItemName}";
}

public sealed class ClientEquipmentSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Equipment Set";
    public bool Enabled { get; set; } = true;
    public ObservableCollection<ClientEquipmentSetEntry> Items { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string DisplayName => $"{Name} - {Items.Count} item(s)";
}

public sealed class ClientInventoryAutomationPreferences
{
    public bool AllowAutomaticSell { get; set; }
    public bool AllowAutomaticStash { get; set; }
    public bool AllowAutomaticDiscard { get; set; }
    public bool AllowAutomaticBuy { get; set; }
    public bool AutoDepositExcessCurrency { get; set; }
    public long KeepCurrency { get; set; }
    public string DepositExcessCommandTemplate { get; set; } = "deposit {amount}";
    public string SellCommandTemplate { get; set; } = "sell {item}";
    public string StashCommandTemplate { get; set; } = "stash {item}";
    public string DiscardCommandTemplate { get; set; } = "drop {item}";
    public string BuyCommandTemplate { get; set; } = "buy {item} {quantity}";
    public string RemoveCommandTemplate { get; set; } = "remove {item}";
    public string EquipCommandTemplate { get; set; } = "equip {item}";
    public int MaximumCommandsPerPass { get; set; } = 40;
}

public sealed class ClientInventoryAutomationProfile
{
    public int Version { get; set; } = 1;
    public string ProfileKey { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public ClientInventoryAutomationPreferences Preferences { get; set; } = new();
    public ObservableCollection<ClientEquipmentSet> EquipmentSets { get; set; } = [];
    public ObservableCollection<ClientInventoryRule> Rules { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClientInventoryAutomationCatalog
{
    public int Version { get; set; } = 1;
    public ObservableCollection<ClientInventoryAutomationProfile> Profiles { get; set; } = [];
}

public sealed record ClientInventoryItemState(
    string InstanceId,
    string ItemId,
    string Name,
    string ItemType,
    string Slot,
    int Quantity,
    int Charges,
    int? Durability,
    bool Equipped)
{
    public string DisplayName
    {
        get
        {
            var quantity = Quantity > 1 ? $"{Quantity} " : string.Empty;
            var slot = Equipped && Slot.Length > 0 ? $" ({Slot})" : string.Empty;
            return quantity + Name + slot;
        }
    }
}

public sealed record ClientInventoryStateSnapshot(
    string WorldId,
    string CharacterId,
    string CharacterName,
    string RoomId,
    long CurrencyCopper,
    bool ShopAvailable,
    bool BankAvailable,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<ClientInventoryItemState> Inventory,
    IReadOnlyList<ClientInventoryItemState> Equipment);

public sealed record ClientInventoryAutomationContext(bool AtSavedStash);

public sealed class ClientInventoryAutomationPlan
{
    public string Action { get; init; } = string.Empty;
    public IReadOnlyList<string> Commands { get; init; } = [];
    public IReadOnlyList<string> MissingItems { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool HasCommands => Commands.Count > 0;
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Action.Length > 0) parts.Add(Action);
            if (Commands.Count > 0) parts.Add($"{Commands.Count} command(s)");
            if (MissingItems.Count > 0) parts.Add($"{MissingItems.Count} missing item(s)");
            if (Warnings.Count > 0) parts.Add($"{Warnings.Count} warning(s)");
            return parts.Count == 0 ? "No action required." : string.Join("; ", parts);
        }
    }
}
