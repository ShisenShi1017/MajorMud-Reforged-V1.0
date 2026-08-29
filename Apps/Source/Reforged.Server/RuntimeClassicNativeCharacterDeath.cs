namespace Reforged.Server;

/// <summary>
/// Exact native WCCMMUD character-death contracts recovered from _check_kill_user,
/// _save_permanent_info, _update_user_with_permanent_info, and _create_player.
/// The native room-slot/stacking, DFS/trail spill, and inventory-only emergency fallback
/// are connected by the 0338 death-floor implementation.
/// </summary>
public static class RuntimeClassicNativeCharacterDeathAuthority
{
    public const int InventorySlots = 100;
    public const int SecondaryItemSlots = 50;
    public const int EquippedSlots = 20;
    public const int VisibleRoomItemSlots = 17;
    public const int HiddenRoomItemSlots = 15;
    public const int MovementTrailLength = 20;
    public const int SpillMaximumDepth = 5;
    public const int ChangeMapExitRoomType = 8;
    public const int RemoteActionExitRoomType = 12;
    public const int EmergencyInventorySpillMap = 1;
    public const int EmergencyInventorySpillRoom = 164;

    public static IReadOnlyList<string> NativeSpillDirectionOrder { get; } =
        new[] { "N", "S", "E", "W", "NE", "NW", "SE", "SW", "U", "D" };

    public static int ReadEvilPoints(RuntimeCharacterRecord character)
    {
        if (character.SemanticState?.Alignment is { } alignment) return Math.Max(0, alignment.EvilPoints);
        if (character.Conditions.TryGetValue("evil_points", out var raw) && int.TryParse(raw, out var value))
            return Math.Max(0, value);
        return 0;
    }

    public static bool IsRetainedOnOrdinaryDeath(RuntimeItemDefinition item) => item.RetainedOnDeath;

    /// <summary>
    /// _check_kill_user passes the live ItemUses field to _add_item_to_room. Classic represents
    /// unlimited-use items as -1, and _add_item_to_room uses exactly that sentinel to enable
    /// same-item room stacking. Finite-use items carry their current use count.
    /// </summary>
    public static int NativeItemUses(RuntimeItemDefinition definition, RuntimeItemInstance item) =>
        definition.MaximumUses < 0 ? -1 : item.Charges;

    public static RuntimeRoom ResolveRespawn(RuntimeSession session, RuntimeCharacterRecord character) =>
        RuntimeClassicDeathLawPvpAuthority.ResolveDeathDestination(
            session.World, session.CurrentRoom, ReadEvilPoints(character));

    public static int NativeExitDirectionIndex(string direction) =>
        RuntimeClassicRoomExitNativeFieldContract.DirectionIndex(direction);
}
