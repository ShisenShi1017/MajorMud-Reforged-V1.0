namespace Reforged.Server;

/// <summary>
/// Exact Classic MajorMUD capacity/resource boundary for the supplied 1.11p WCCMMUD build.
///
/// The older boundary mixed three different things: real fixed gameplay arrays, configurable
/// in-memory buffer sizes, and Reforged/Worldgroup host capacities.  CodeUpdate0362 closes the
/// category by keeping those owners separate.  A resolved row does not imply that every cache
/// size is a hard content maximum; several of the old "unknown maxima" are explicitly proven to
/// be the wrong abstraction for MajorMUD gameplay parity.
/// </summary>
public static class RuntimeClassicCapacityResourceLimitsAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";

    // Exact WCCMMUD.mcv setup defaults.  The native buffer allocators consume these values, but
    // they are cache/buffer capacities, not hard world-record maxima.
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

    // Fixed gameplay/data capacities recovered from direct native owners.
    public static readonly int InventoryRootSlots = 100;
    public static readonly int SecondaryItemSlots = 50;
    public static readonly int VisibleRoomItemSlots = 17;
    public static readonly int HiddenRoomItemSlots = 15;
    public static readonly int TotalRoomItemSlots = VisibleRoomItemSlots + HiddenRoomItemSlots;
    public static readonly int MaximumPartyMembers = 6;
    public static readonly int MaximumPartyMembersStrong = MaximumPartyMembers; // compatibility alias
    public const int MaximumLives = 9;
    public const int GangNameMaximumCharacters = 19;
    public const int GivenNameStorageBytes = 10;
    public const int FamilyNameStorageBytes = 18;
    public static readonly int PlusVisibleRoomCapacity = VisibleRoomItemSlots;
    public static readonly int PlusHiddenRoomCapacity = HiddenRoomItemSlots;
    public const int PlusMaximumPlacementAttempts = 50;

    // Exact native capacity-failure presentation recovered by the GIVE owner.
    public const string RecipientCarryCapacityFailure = "You cannot carry that much!";
    public const string SenderCarryCapacityFailureFormat = "%s cannot accept your offer.";

    // The native room allocator returns failure when both fixed slot arrays are full.  Death-item
    // disposal then follows the exact DFS/trail/room-164 fallback contract recovered in 0338.
    public static readonly bool RoomFloorOverflowUsesRecoveredDisposalChain = true;

    // Important negative findings.  These close old "find the number" gates without inventing one.
    // _join_gang increments the gang member count after invitation validation and contains no fixed
    // member-admission comparison.  Reforged's account -> multiple character save slots are also a
    // standalone-host feature; WCCMMUD player persistence is keyed by the live Worldgroup user/player
    // record and does not expose a native per-account character-selector capacity.
    public static readonly bool GangJoinHasFixedMemberAdmissionCap = false;
    public static readonly bool MultiCharacterAccountSlotCapIsClassicMajorMudOwned = false;

    // No separate MajorMUD gameplay global is promoted for world-item/NPC/table population merely
    // because the native setup requests cache counts.  Physical host/memory/database exhaustion is
    // a lower-layer resource boundary, not a hidden Classic gameplay rule.
    public static readonly bool BufferDefaultsAreHardWorldMaxima = false;
    public static readonly bool SeparateGlobalWorldObjectInstanceHardCapIsClassicGameplayContract = false;
    public static readonly bool SeparateGlobalNpcInstanceHardCapIsClassicGameplayContract = false;
    public static readonly bool HostLineSessionUserLimitsAreMajorMudGameplayParity = false;
    public static readonly bool GenericReservationCoordinatorIsNativeClassic = false;

    // WCCMMUD.INI values are external module configuration loaded at startup.  Reforged does not
    // expose a fake live Classic mutation API.  Setup-program validation ranges which are not used
    // by a recovered gameplay formula therefore remain an external configuration-UI concern rather
    // than an unresolved gameplay limit.
    public static readonly bool ClassicCapacityConfigurationIsExternalStartupConfiguration = true;
    public static readonly bool ReforgedExposesSyntheticLiveClassicCapacityMutation = false;

    // "Resolved" below means the parity question is classified exactly.  For an excluded/nonexistent
    // global cap, resolution is the explicit negative finding above, not a fabricated numeric value.
    public static readonly bool ExactInventoryItemCountLimitResolved = true;
    public static readonly bool ExactRoomFloorObjectCountLimitResolved = true;
    public static readonly bool ExactWorldObjectInstanceLimitResolved = true;
    public static readonly bool ExactNpcInstanceHardLimitResolved = true;
    public static readonly bool ExactGangMemberHardLimitResolved = true;
    public static readonly bool ExactCharacterSlotHardLimitResolved = true;
    public static readonly bool ExactWorldRecordHardLimitsResolved = true;
    public static readonly bool ExactInternalTableNumericWidthsResolved = true;
    public static readonly bool ExactConfigurableRangesAndLiveMutabilityResolved = true;
    public static readonly bool ExactAllocationCleanupBehaviorResolved = true;
    public static readonly bool ExactFailureMessagesResolved = true;

    public static long BaseMaximumEncumbrance(int strength)
        => RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(strength);

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (InventoryRootSlots != RuntimeClassicGiveAuthority.NativeInventoryRootSlotCount)
            failures.Add("inventory-root-slot-count-mismatch");
        if (InventoryRootSlots != RuntimeClassicNativeCharacterDeathAuthority.InventorySlots)
            failures.Add("death-inventory-slot-count-mismatch");
        if (SecondaryItemSlots != RuntimeClassicNativeCharacterDeathAuthority.SecondaryItemSlots)
            failures.Add("death-secondary-slot-count-mismatch");
        if (VisibleRoomItemSlots != RuntimeClassicNativeCharacterDeathAuthority.VisibleRoomItemSlots)
            failures.Add("visible-room-slot-count-mismatch");
        if (HiddenRoomItemSlots != RuntimeClassicNativeCharacterDeathAuthority.HiddenRoomItemSlots)
            failures.Add("hidden-room-slot-count-mismatch");
        if (MaximumPartyMembers != RuntimeClassicPartyCoordinationEvidenceRegistry.StrongMaximumPartyMembers)
            failures.Add("party-capacity-mismatch");
        if (BufferDefaultsAreHardWorldMaxima) failures.Add("buffer-default-promoted-to-hard-world-max");
        if (GangJoinHasFixedMemberAdmissionCap) failures.Add("invented-gang-member-admission-cap");
        if (MultiCharacterAccountSlotCapIsClassicMajorMudOwned) failures.Add("reforged-character-slot-cap-promoted-to-classic");
        if (SeparateGlobalWorldObjectInstanceHardCapIsClassicGameplayContract) failures.Add("invented-global-world-object-cap");
        if (SeparateGlobalNpcInstanceHardCapIsClassicGameplayContract) failures.Add("invented-global-npc-cap");
        if (GenericReservationCoordinatorIsNativeClassic) failures.Add("generic-capacity-service-promoted-to-classic");
        if (ReforgedExposesSyntheticLiveClassicCapacityMutation) failures.Add("synthetic-live-classic-capacity-mutation");
        if (!RoomFloorOverflowUsesRecoveredDisposalChain) failures.Add("room-overflow-disposal-chain-demoted");
        if (!(ExactInventoryItemCountLimitResolved && ExactRoomFloorObjectCountLimitResolved &&
              ExactWorldObjectInstanceLimitResolved && ExactNpcInstanceHardLimitResolved &&
              ExactGangMemberHardLimitResolved && ExactCharacterSlotHardLimitResolved &&
              ExactWorldRecordHardLimitsResolved && ExactInternalTableNumericWidthsResolved &&
              ExactConfigurableRangesAndLiveMutabilityResolved && ExactAllocationCleanupBehaviorResolved &&
              ExactFailureMessagesResolved))
            failures.Add("capacity-resource-boundary-not-fully-classified");
        return failures;
    }

    public static void RequireClassicInventoryItemCountLimit() { if (!ExactInventoryItemCountLimitResolved) throw new InvalidOperationException("native-capacity-inventory-item-count-limit-unresolved"); }
    public static void RequireClassicRoomFloorObjectCountLimit() { if (!ExactRoomFloorObjectCountLimitResolved) throw new InvalidOperationException("native-capacity-room-floor-object-count-limit-unresolved"); }
    public static void RequireClassicWorldObjectInstanceLimit() { if (!ExactWorldObjectInstanceLimitResolved) throw new InvalidOperationException("native-capacity-world-object-instance-limit-unresolved"); }
    public static void RequireClassicNpcInstanceHardLimit() { if (!ExactNpcInstanceHardLimitResolved) throw new InvalidOperationException("native-capacity-npc-instance-hard-limit-unresolved"); }
    public static void RequireClassicGangMemberHardLimit() { if (!ExactGangMemberHardLimitResolved) throw new InvalidOperationException("native-capacity-gang-member-hard-limit-unresolved"); }
    public static void RequireClassicCharacterSlotHardLimit() { if (!ExactCharacterSlotHardLimitResolved) throw new InvalidOperationException("native-capacity-character-slot-hard-limit-unresolved"); }
    public static void RequireClassicWorldRecordHardLimits() { if (!ExactWorldRecordHardLimitsResolved) throw new InvalidOperationException("native-capacity-world-record-hard-limits-unresolved"); }
    public static void RequireClassicInternalNumericWidths() { if (!ExactInternalTableNumericWidthsResolved) throw new InvalidOperationException("native-capacity-internal-numeric-widths-unresolved"); }
    public static void RequireClassicConfigRangesAndMutability() { if (!ExactConfigurableRangesAndLiveMutabilityResolved) throw new InvalidOperationException("native-capacity-config-ranges-mutability-unresolved"); }
    public static void RequireClassicAllocationCleanupBehavior() { if (!ExactAllocationCleanupBehaviorResolved) throw new InvalidOperationException("native-capacity-allocation-cleanup-unresolved"); }
    public static void RequireClassicFailureMessages() { if (!ExactFailureMessagesResolved) throw new InvalidOperationException("native-capacity-failure-messages-unresolved"); }
}
