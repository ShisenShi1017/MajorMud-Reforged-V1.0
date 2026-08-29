namespace Reforged.Server;

/// <summary>
/// Evidence gate for recovered Classic MajorMUD capacity/resource-limit facts.
///
/// This authority deliberately distinguishes exact retained configuration buffer defaults and
/// already-recovered gameplay/data caps from Reforged's generic reservation coordinator.
/// A buffer default is not promoted as a hard world maximum. Unknown inventory/object/NPC/world
/// hard limits, integer widths, cleanup behavior, and exact failure rendering remain fail-closed.
/// </summary>
public static class RuntimeClassicCapacityResourceLimitsAuthority
{
    // Exact WCCMMUD.mcv retained configuration defaults. These are buffer capacities/defaults,
    // not proof of hard gameplay maxima.
    public const int RoomsBufferedDefault = 1500;
    public const int ItemsBufferedDefault = 750;
    public const int ActiveMonstersBufferedDefault = 2000;
    public const int KnownMonstersBufferedDefault = 300;
    public const int SpellsBufferedDefault = 150;
    public const int RacesBufferedDefault = 12;
    public const int ClassesBufferedDefault = 16;
    public const int BankbooksBufferedDefault = 32;
    public const int MessageBufferDefaultA = 250;
    public const int MessageBufferDefaultB = 50;
    public const int MessageBufferDefaultC = 10;
    public const int TopTenEntriesDefaultMaximum = 30;
    public const int MaximumItemsLostOnHangupDefault = 0;

    // Narrow recovered gameplay/data caps and widths from their owning subsystems.
    public const int MaximumLives = 9;
    public const int MaximumPartyMembersStrong = 6;
    public const int GangNameMaximumCharacters = 19;
    public const int GivenNameStorageBytes = 10;
    public const int FamilyNameStorageBytes = 18;
    public const int PlusVisibleRoomCapacity = 17;
    public const int PlusHiddenRoomCapacity = 15;
    public const int PlusMaximumPlacementAttempts = 50;

    // Exact stock encumbrance boundary recovered elsewhere; this is a weight capacity formula,
    // not an inventory-slot count.
    public static long BaseMaximumEncumbrance(int strength)
        => RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(strength);

    public const bool GenericReservationCoordinatorIsNativeClassic = false;
    public const bool ConfigurationBufferDefaultsAreHardWorldMaxima = false;
    public const bool HostLineSessionUserLimitsAreMajorMudGameplayParity = false;

    public const bool ExactInventoryItemCountLimitResolved = false;
    public const bool ExactRoomFloorObjectCountLimitResolved = false;
    public const bool ExactWorldObjectInstanceLimitResolved = false;
    public const bool ExactNpcInstanceHardLimitResolved = false;
    public const bool ExactGangMemberHardLimitResolved = false;
    public const bool ExactCharacterSlotHardLimitResolved = false;
    public const bool ExactWorldRecordHardLimitsResolved = false;
    public const bool ExactInternalTableNumericWidthsResolved = false;
    public const bool ExactConfigurableRangesAndLiveMutabilityResolved = false;
    public const bool ExactAllocationCleanupBehaviorResolved = false;
    public const bool ExactFailureMessagesResolved = false;

    public static void RequireClassicInventoryItemCountLimit()
    {
        if (!ExactInventoryItemCountLimitResolved)
            throw new InvalidOperationException("native-capacity-inventory-item-count-limit-unresolved");
    }

    public static void RequireClassicRoomFloorObjectCountLimit()
    {
        if (!ExactRoomFloorObjectCountLimitResolved)
            throw new InvalidOperationException("native-capacity-room-floor-object-count-limit-unresolved");
    }

    public static void RequireClassicWorldObjectInstanceLimit()
    {
        if (!ExactWorldObjectInstanceLimitResolved)
            throw new InvalidOperationException("native-capacity-world-object-instance-limit-unresolved");
    }

    public static void RequireClassicNpcInstanceHardLimit()
    {
        if (!ExactNpcInstanceHardLimitResolved)
            throw new InvalidOperationException("native-capacity-npc-instance-hard-limit-unresolved");
    }

    public static void RequireClassicGangMemberHardLimit()
    {
        if (!ExactGangMemberHardLimitResolved)
            throw new InvalidOperationException("native-capacity-gang-member-hard-limit-unresolved");
    }

    public static void RequireClassicCharacterSlotHardLimit()
    {
        if (!ExactCharacterSlotHardLimitResolved)
            throw new InvalidOperationException("native-capacity-character-slot-hard-limit-unresolved");
    }

    public static void RequireClassicWorldRecordHardLimits()
    {
        if (!ExactWorldRecordHardLimitsResolved)
            throw new InvalidOperationException("native-capacity-world-record-hard-limits-unresolved");
    }

    public static void RequireClassicInternalNumericWidths()
    {
        if (!ExactInternalTableNumericWidthsResolved)
            throw new InvalidOperationException("native-capacity-internal-numeric-widths-unresolved");
    }

    public static void RequireClassicConfigRangesAndMutability()
    {
        if (!ExactConfigurableRangesAndLiveMutabilityResolved)
            throw new InvalidOperationException("native-capacity-config-ranges-mutability-unresolved");
    }

    public static void RequireClassicAllocationCleanupBehavior()
    {
        if (!ExactAllocationCleanupBehaviorResolved)
            throw new InvalidOperationException("native-capacity-allocation-cleanup-unresolved");
    }

    public static void RequireClassicFailureMessages()
    {
        if (!ExactFailureMessagesResolved)
            throw new InvalidOperationException("native-capacity-failure-messages-unresolved");
    }
}
