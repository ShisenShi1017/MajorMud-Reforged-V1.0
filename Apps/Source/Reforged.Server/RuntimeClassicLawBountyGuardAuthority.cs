namespace Reforged.Server;

public sealed record RuntimeClassicJailBand(
    int MinimumEvilPoints,
    int? MaximumEvilPoints,
    int JailTimeSpellId,
    int DurationSeconds,
    string Label);

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD law / Evil-Point / guard-jail boundary.
///
/// Dedicated WCCMMUD/WBBMMUD exports establish native ownership of Evil Point award,
/// timer, forgiveness, room-warning, robbery-law coupling, display, command, and persistence
/// families. Decoded MDB/TextBlock/monster/spell records independently establish a guard-hit
/// jail script graph. None of those sources proves a generic Reforged crime ledger, bounty
/// arithmetic, witness radius, fine/confiscation policy, or native law dispatcher cadence.
/// Those exact edges remain fail-closed.
/// </summary>
public static class RuntimeClassicLawBountyGuardAuthority
{
    public const int NativeLawBoundaryAnchorCount = 17;
    public const bool NativeEvilPointOwnerFamilyResolved = true;
    public const bool NativeEvilPointPersistenceOwnerResolved = true;
    public const bool GenericLawPolicyIsNativeClassic = false;
    public const bool GenericBountyAmountIsNativeClassic = false;
    public const bool GenericCrimeDecayDeadlineIsNativeClassic = false;

    public const int GuardJailHitSpellId = 583;
    public const int GuardJailEntryTextblockId = 1509;
    public const int GuardJailBandDispatchTextblockId = 9624;
    public const int JailTeleportSpellId = 584;
    public const int ExitJailControlSpellId = 585;
    public const int ExitJailGateTextblockId = 9113;
    public const int ExitJailTeleportSpellId = 929;
    public const int JailMapId = 1;
    public const int EncodedJailRoomMinimum = 541;
    public const int EncodedJailRoomMaximum = 545;
    public const int ExitJailMapId = 1;
    public const int ExitJailRoomId = 1076;
    public const int ExitJailRequiredRoomItemId = 1363;

    public const bool ExactCrimeActionClassificationResolved = false;
    public const bool ExactWitnessAdmissionResolved = false;
    public const bool ExactGuardAlertSpawnHostilityCadenceResolved = false;
    public const bool ExactVillainFiendExtraEnforcementResolved = false;
    public const bool ExactFineConfiscationBountyArithmeticResolved = false;
    public const bool ExactForgiveSchedulingAccountingResolved = false;
    public const bool ExactLawMessagesResolved = false;
    public const bool ExactEvilPointSerializationResolved = false;
    public const bool ExactJailExecutionOrderingResolved = false;
    public const bool ExactJailDestinationDrawResolved = false;

    public static IReadOnlyList<string> NativeLawBoundaryAnchors { get; } = new[]
    {
        "_add_evil_points",
        "_add_evil_timer",
        "_add_evil_warnings_to_room",
        "_already_evil",
        "_attempt_to_forgive",
        "_begin_clearing_evil_points",
        "_clear_evil_points_routine",
        "_cmd_forgive",
        "_cmd_guard",
        "_decrement_evil_timers",
        "_delete_evil_points",
        "_display_evil_star",
        "_display_evil_timers",
        "_evil_for_robbing",
        "_get_saved_evil_points",
        "_save_evil_points",
        "_should_give_evil"
    };

    public static IReadOnlyList<int> DecodedGuardJailNpcLegacyIds { get; } = new[] { 13, 14, 40, 757 };

    public static IReadOnlyList<RuntimeClassicJailBand> DecodedJailBands { get; } = new[]
    {
        new RuntimeClassicJailBand(210, null, 586, 200, "fiend"),
        new RuntimeClassicJailBand(120, 209, 641, 175, "villain"),
        new RuntimeClassicJailBand(80, 119, 642, 150, "criminal"),
        new RuntimeClassicJailBand(40, 79, 643, 125, "outlaw"),
        // TextBlock 9624 uses GOODALIGNED 39 for the final branch. Preserve the decoded
        // boundary data; do not generalize it into a guessed native alignment comparator.
        new RuntimeClassicJailBand(int.MinValue, 39, 644, 100, "seedy-or-better")
    };

    public static void RequireClassicCrimeActionClassification()
    {
        if (!ExactCrimeActionClassificationResolved)
            throw new InvalidOperationException("native-law-crime-action-classification-unresolved");
    }

    public static void RequireClassicWitnessAdmission()
    {
        if (!ExactWitnessAdmissionResolved)
            throw new InvalidOperationException("native-law-witness-admission-unresolved");
    }

    public static void RequireClassicGuardCadence()
    {
        if (!ExactGuardAlertSpawnHostilityCadenceResolved)
            throw new InvalidOperationException("native-law-guard-cadence-unresolved");
    }

    public static void RequireClassicExtraEnforcement()
    {
        if (!ExactVillainFiendExtraEnforcementResolved)
            throw new InvalidOperationException("native-law-extra-enforcement-unresolved");
    }

    public static void RequireClassicFineConfiscationBountyArithmetic()
    {
        if (!ExactFineConfiscationBountyArithmeticResolved)
            throw new InvalidOperationException("native-law-fine-confiscation-bounty-unresolved");
    }

    public static void RequireClassicForgiveSchedulingAccounting()
    {
        if (!ExactForgiveSchedulingAccountingResolved)
            throw new InvalidOperationException("native-law-forgive-scheduling-unresolved");
    }

    public static void RequireClassicMessages()
    {
        if (!ExactLawMessagesResolved)
            throw new InvalidOperationException("native-law-messages-unresolved");
    }

    public static void RequireClassicEvilPointSerialization()
    {
        if (!ExactEvilPointSerializationResolved)
            throw new InvalidOperationException("native-law-evil-point-serialization-unresolved");
    }

    public static void RequireClassicJailExecutionOrdering()
    {
        if (!ExactJailExecutionOrderingResolved)
            throw new InvalidOperationException("native-law-jail-execution-order-unresolved");
    }

    public static void RequireClassicJailDestinationDraw()
    {
        if (!ExactJailDestinationDrawResolved)
            throw new InvalidOperationException("native-law-jail-destination-draw-unresolved");
    }
}
