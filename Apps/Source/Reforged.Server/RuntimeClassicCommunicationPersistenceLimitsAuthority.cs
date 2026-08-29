namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD communication-profile and paging-limit boundary.
///
/// Native help proves user-scoped profile controls, PAGE toggling, and the exact SET MAXPAGES
/// three-second window. It does not prove the stored default bits, explicit TELEPATH polarity,
/// native record serialization, page overflow/backpressure behavior, or exact channel rendering.
/// Reforged's generic 512-character / 10-second / 6-message communication policy is an extension
/// shell and receives no Classic parity credit.
/// </summary>
public static class RuntimeClassicCommunicationPersistenceLimitsAuthority
{
    public const int ProfileHelpMessageNumber = 37;
    public const int SetHelpMessageNumber = 117;
    public const int MessagesHelpMessageNumber = 121;
    public const int GossipHelpMessageNumber = 175;
    public const int AuctionHelpMessageNumber = 177;

    public const int MaxPagesWindowSeconds = 3;
    public const bool MaxPagesAppliesToPages = true;
    public const bool MaxPagesAppliesToTelegrams = true;
    public const bool PageCommandTogglesInGamePaging = true;
    public const bool ProfileCommandDisplaysCurrentProfileSettings = true;
    public const bool BareNativeSetOptionsToggleCurrentState = true;
    public const bool ForgetTogglesPerSenderTelepathBlock = true;
    public const bool GossipExplicitReceiptPolarityResolved = true;
    public const bool AuctionExplicitReceiptPolarityResolved = true;
    public const bool MessagesThresholdMeaningsResolved = true;
    public const bool EntrancesSelectionContractResolved = true;

    public const string NativeSetCommandExport = "_cmd_set";
    public const string NativeForgetCommandExport = "_cmd_forget";
    public const string NativeGossipCommandExport = "_cmd_gossip";

    public const bool GenericCommunicationFloodPolicyIsNativeClassic = false;
    public const bool GenericMaximumMessageLengthIsNativeClassic = false;
    public const bool ReforgedCharacterConditionsAreNativeProfileSerialization = false;
    public const bool MaxPagesIsGeneralChatFloodControl = false;
    public const bool WorldgroupMailAndNativeLoggingAreMajorMudGameplayParity = false;

    public const bool ExactNativeProfileDefaultsResolved = false;
    public const bool ExactNativeProfileSerializationResolved = false;
    public const bool ExactTelepathExplicitPolarityResolved = false;
    public const bool ExactMaxPagesDefaultResolved = false;
    public const bool ExactMaxPagesAllowedRangeResolved = false;
    public const bool ExactMaxPagesOverflowBehaviorResolved = false;
    public const bool ExactPageTelegramHostHandoffResolved = false;
    public const bool ExactPageTelegramRenderingResolved = false;
    public const bool ExactChannelRenderingResolved = false;
    public const bool ExactMessagePriorityMembershipResolved = false;
    public const bool ExactEntrancesDefaultPersistenceResolved = false;
    public const bool ExactReconnectLoadSaveOrderingResolved = false;

    public static void RequireClassicNativeProfileDefaults()
    {
        if (!ExactNativeProfileDefaultsResolved)
            throw new InvalidOperationException("native-communications-profile-defaults-unresolved");
    }

    public static void RequireClassicNativeProfileSerialization()
    {
        if (!ExactNativeProfileSerializationResolved)
            throw new InvalidOperationException("native-communications-profile-serialization-unresolved");
    }

    public static void RequireClassicTelepathExplicitPolarity()
    {
        if (!ExactTelepathExplicitPolarityResolved)
            throw new InvalidOperationException("native-communications-telepath-explicit-polarity-unresolved");
    }

    public static void RequireClassicMaxPagesDefault()
    {
        if (!ExactMaxPagesDefaultResolved)
            throw new InvalidOperationException("native-communications-maxpages-default-unresolved");
    }

    public static void RequireClassicMaxPagesAllowedRange()
    {
        if (!ExactMaxPagesAllowedRangeResolved)
            throw new InvalidOperationException("native-communications-maxpages-range-unresolved");
    }

    public static void RequireClassicMaxPagesOverflowBehavior()
    {
        if (!ExactMaxPagesOverflowBehaviorResolved)
            throw new InvalidOperationException("native-communications-maxpages-overflow-unresolved");
    }

    public static void RequireClassicPageTelegramHostHandoff()
    {
        if (!ExactPageTelegramHostHandoffResolved)
            throw new InvalidOperationException("native-communications-page-host-handoff-unresolved");
    }

    public static void RequireClassicPageTelegramRendering()
    {
        if (!ExactPageTelegramRenderingResolved)
            throw new InvalidOperationException("native-communications-page-rendering-unresolved");
    }

    public static void RequireClassicChannelRendering()
    {
        if (!ExactChannelRenderingResolved)
            throw new InvalidOperationException("native-communications-channel-rendering-unresolved");
    }

    public static void RequireClassicMessagePriorityMembership()
    {
        if (!ExactMessagePriorityMembershipResolved)
            throw new InvalidOperationException("native-communications-message-priority-membership-unresolved");
    }

    public static void RequireClassicEntrancesDefaultPersistence()
    {
        if (!ExactEntrancesDefaultPersistenceResolved)
            throw new InvalidOperationException("native-communications-entrances-default-persistence-unresolved");
    }

    public static void RequireClassicReconnectLoadSaveOrdering()
    {
        if (!ExactReconnectLoadSaveOrderingResolved)
            throw new InvalidOperationException("native-communications-reconnect-load-save-order-unresolved");
    }
}
