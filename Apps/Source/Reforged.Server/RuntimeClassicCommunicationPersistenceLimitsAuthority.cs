namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD communication-profile and paging-limit boundary.
///
/// Native WCCMMUD.DLL ownership now proves the stored communication-profile defaults, persisted
/// record offsets, TELEPATH polarity, TALK FAST/SLOW + RESPONSE persistence,
/// MAXPAGES/MAXGOSSIP/MAXAUCTION byte semantics, reconnect load/save mapping, and three-second receive counters. PAGE itself is a live-session toggle and
/// is not in the persisted profile block. Worldgroup page/telegram transport, host presentation queues, mail storage, and host logging are not Reforged parity requirements because Reforged does not host Worldgroup.
/// Only MajorMUD-owned player-visible state/text and gameplay filtering remain parity targets; CodeUpdate0345 closes parser-visible TELEPATH/GOSSIP/AUCTION channel rendering. Reforged's generic communication flood shell receives no
/// Classic parity credit.
/// </summary>
public enum RuntimeClassicRoomMessagePriority : byte
{
    High = 0,
    Medium = 10,
    Low = 20
}

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


    // Fresh-character values written by the native initializer at 0x004165FB..0x00416643.
    public const bool DefaultPageEnabled = true;
    public const bool DefaultTelepathReceiveEnabled = true;
    public const bool DefaultGossipReceiveEnabled = true;
    public const bool DefaultAuctionReceiveEnabled = true;
    public const bool DefaultEntrancesEnabled = true;
    // Native fresh-character initializer: live byte +0x5F4 = 1 (FAST), flags word +0x700 = 0x0010
    // so the 0x0100 brief-response bit starts clear (VERBOSE).
    public const bool DefaultTalkFast = true;
    public const bool DefaultTalkResponseBrief = false;
    public const string DefaultMessagesLevel = "high";
    public const byte DefaultMaxPages = 3;
    public const byte DefaultMaxGossip = 3;
    public const byte DefaultMaxAuction = 3;

    // Native character-record offsets recovered from the save/load owner. PAGE itself is a
    // live-session byte and is deliberately absent from this persisted block.
    public const int RecordMessagesThresholdOffset = 0x3B;
    public const int RecordCommunicationFlagsOffset = 0x3C;
    public const int RecordMaxPagesOffset = 0x3E;
    public const int RecordDisplayFlagsOffset = 0x6A;
    public const int RecordTalkModeOffset = 0x45;
    public const int RecordMaxGossipOffset = 0xA9;
    public const int RecordMaxAuctionOffset = 0xAA;
    public const int TelepathBlockMask = 0x20;
    public const int TalkResponseBriefMask = 0x0100;
    public const int EntrancesBlockMask = 0x01;
    public const int GossipBlockMask = 0x02;
    public const int AuctionBlockMask = 0x04;

    /// <summary>Matches native SET MAX* semantics: atol, cap only values above 255, then store AL.</summary>
    public static byte NormalizeNativeLimit(long value) => unchecked((byte)(value > byte.MaxValue ? byte.MaxValue : value));

    /// <summary>Native receive owners compare current &gt; max before incrementing.</summary>
    public static bool NativePreIncrementReceiveAllowed(byte current, byte maximum) => current <= maximum;

    public static string CycleMessagesLevel(string current) => current.ToLowerInvariant() switch
    {
        "high" => "low",
        "medium" => "high",
        _ => "medium"
    };

    // Native _cmd_set writes the observer threshold byte directly: High=0, Medium=10, Low=20.
    // Native _tell_room always admits priority 0; priorities 1..10 require a threshold <=10;
    // priorities 11..20 require threshold 0. Values above 20 are rejected by that room-output path.
    public static byte NativeMessagesThreshold(string level) => level.Trim().ToLowerInvariant() switch
    {
        "medium" => (byte)RuntimeClassicRoomMessagePriority.Medium,
        "low" => (byte)RuntimeClassicRoomMessagePriority.Low,
        _ => (byte)RuntimeClassicRoomMessagePriority.High
    };

    public static bool ShouldDeliverRoomMessage(string level, RuntimeClassicRoomMessagePriority priority)
    {
        var nativePriority = (byte)priority;
        if (nativePriority == 0) return true;
        if (nativePriority > (byte)RuntimeClassicRoomMessagePriority.Low) return false;
        return nativePriority <= 10
            ? NativeMessagesThreshold(level) <= 10
            : NativeMessagesThreshold(level) == 0;
    }

    public const bool GenericCommunicationFloodPolicyIsNativeClassic = false;
    public const bool GenericMaximumMessageLengthIsNativeClassic = false;
    public const bool ReforgedCharacterConditionsAreNativeProfileSerialization = false;
    public const bool MaxPagesIsGeneralChatFloodControl = false;
    public const bool WorldgroupMailAndNativeLoggingAreMajorMudGameplayParity = false;
    public const bool WorldgroupPageTelegramHostHandoffIsMajorMudGameplayParity = false;
    public const bool WorldgroupPageTelegramPresentationIsMajorMudGameplayParity = false;

    public const bool ExactNativeProfileDefaultsResolved = true;
    public const bool ExactNativeProfileSerializationResolved = true;
    public const bool ExactTelepathExplicitPolarityResolved = true;
    public const bool ExactMaxPagesDefaultResolved = true;
    public const bool ExactMaxPagesAllowedRangeResolved = true;
    public const bool ExactMaxPagesOverflowBehaviorResolved = true;
    // These remain false as historical/native-research facts, but are not parity gates for Reforged.
    public const bool ExactPageTelegramHostHandoffResolved = false;
    public const bool ExactPageTelegramRenderingResolved = false;
    public const bool ExactChannelRenderingResolved = true;
    public const bool ExactMessagePriorityMembershipResolved = true;
    public const bool ExactEntrancesDefaultPersistenceResolved = true;
    public const bool ExactReconnectLoadSaveOrderingResolved = true;
    public const bool ExactTalkModeDefaultResolved = true;
    public const bool ExactTalkModePersistenceResolved = true;
    public const bool ExactTalkResponsePersistenceResolved = true;
    public const bool ExactTalkResolverModeResolved = true;

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
