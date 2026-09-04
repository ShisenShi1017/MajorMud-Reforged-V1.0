namespace RealmGate.Server;

public static class RuntimeClassicGiveAuthority
{
    public const int NativeCommandRva = 0x67744;
    public const int NativeAddItemRva = 0x1B111;
    public const int NativeInventoryRootSlotCount = 100;

    public const string ItemCannotBeGivenMessage = "You may not give that item away!";
    public const string RecipientCapacityMessage = "You cannot carry that much!";
    public const string ReceiveOffMarker = "__classic_give_receive_off__";
    public const string CapacityMarker = "__classic_give_capacity__";
    public const string ItemRestrictedMarker = "__classic_give_item_restricted__";

    public static string RenderReceiveOff(string targetName) => $"{targetName} refuses your offer.";
    public static string RenderCannotAccept(string targetName) => $"{targetName} cannot accept your offer.";
    public static string RenderSender(string itemName, string targetName) => $"You just gave {itemName} to {targetName}.";
    public static string RenderRecipient(string sourceName, string itemName) => $"{sourceName} just gave you {itemName}.";
    public static string RenderObserver(string sourceName, string targetName) => $"{sourceName} just gave {targetName} something.";

    public static bool IsPreTargetRestricted(RuntimeItemInstance item) =>
        MetadataTrue(item, "no_give");

    public static bool IsTransferRestrictionFailure(string? message)
    {
        var text = message ?? string.Empty;
        return text.Contains("restriction prevents give", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Bound item restriction prevents give", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Quest item ownership restriction prevents give", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MetadataTrue(RuntimeItemInstance item, string key) =>
        item.Metadata?.TryGetValue(key, out var raw) == true
        && bool.TryParse(raw, out var value)
        && value;
}
