namespace RealmGate.Server;

/// <summary>
/// Recovered Classic MajorMUD NPC-AI ownership and behavior boundary (CodeUpdate0364).
///
/// Native exhaustion corrected several early category assumptions. _find_action_target and
/// _find_any_action_target are player command/parser helpers, not autonomous monster perception.
/// The "known monster" family is the loaded monster-definition cache, not a hate-memory ledger.
/// The fast/medium/slow/energy monster lanes are now recovered directly enough to separate
/// actual Classic behavior from Reforged extension services.
/// </summary>
public static class RuntimeClassicNpcAiSemanticsAuthority
{
    public const int NativeAiBoundaryAnchorCount = 43;
    public const int NativeUpdateLaneAnchorCount = 14;
    public const int NativeTargetSelectionAnchorCount = 9;
    public const int NativeMovementTrackingAnchorCount = 7;
    public const int NativeKnownMonsterMemoryAnchorCount = 8;
    public const int NativeCombatActionAnchorCount = 5;

    public const bool NativeAiOwnerFamiliesResolved = true;
    public const bool GenericSchedulerOrderingIsNativeClassic = false;
    public const bool GenericDecisionPriorityIsNativeClassic = false;
    public const bool GenericThreatLedgerIsNativeClassic = false;
    public const bool GenericPerceptionRadiusIsNativeClassic = false;
    public const bool GenericLowHpFleePolicyIsNativeClassic = false;
    public const bool GenericPatrolPathIsNativeClassic = false;

    // Resolved means the native question itself is closed. Some are closed by proving that the
    // earlier proposed generic behavior is absent from the native background lanes.
    public const bool ExactFleeThresholdAndTriggerResolved = true;
    public const bool ExactPerceptionAggroAcquisitionResolved = true;
    public const bool ExactThreatTargetSwitchOrderingResolved = true;
    public const bool ExactPatrolPathingResolved = true;
    public const bool ExactTrackingSemanticsResolved = true;
    public const bool ExactAssistPriorityPropagationResolved = true;
    public const bool ExactNativeUpdateCadencePriorityResolved = true;
    public const bool ExactRandomRoamSemanticsResolved = true;
    public const bool KnownMonsterCacheOwnershipResolved = true;
    public const bool MonsGuardsSpellTargetSubstitutionResolved = true;

    public const bool AutonomousLowHpFleePresent = false;
    public const bool AutonomousThreatScoreTargetSwitchPresent = false;
    public const bool AutonomousPerceptionAggroRadiusPresent = false;

    public const int NativeRoomMonsterSlots = 15;
    public const int NativeAttackStorageSlots = 5;
    public const int NativeMediumMovementAttemptCap = 3;
    public const int NativeFastFollowRetryLimit = 15;
    public const int ConfusionAbilityId = 71;
    public const int ConfusionMessageAbilityId = 101;
    public const int MonsGuardsAbilityId = 146;

    public static IReadOnlyList<string> NativeUpdateLaneAnchors { get; } = new[]
    {
        "_background_energy", "_background_fast", "_background_medium", "_background_slow",
        "_energy_update_monster",
        "_fast_update_monster", "_fast_update_monsters", "_fast_update_next_monster",
        "_medium_update_monster", "_medium_update_monsters", "_medium_update_next_monster",
        "_slow_update_monster", "_slow_update_monsters", "_slow_update_next_monster"
    };

    public static IReadOnlyList<string> NativeTargetSelectionAnchors { get; } = new[]
    {
        "_find_action_target", "_find_any_action_target", "_is_valid_monster_target",
        "_monster_could_attack", "_monster_count_valid_targets", "_count_valid_targets",
        "_confirm_monster_in_room", "_is_combatting_monster", "_match_combat_target"
    };

    public static IReadOnlyList<string> NativeCommandTargetResolverAnchors { get; } = new[]
    {
        "_find_action_target", "_find_any_action_target"
    };

    public static IReadOnlyList<string> NativeMovementTrackingAnchors { get; } = new[]
    {
        "_move_monster", "_move_monster_to_fighter", "_dir_monster_travelling_coord",
        "_handle_following_monsters", "_track_monster", "_track_player", "_stop_following"
    };

    public static IReadOnlyList<string> NativeKnownMonsterMemoryAnchors { get; } = new[]
    {
        "_find_monster_in_memory", "_get_known_monster_data", "_load_known_monster_into_buffer",
        "_save_known_monster_from_buffer", "_clear_known_monster", "_clear_known_monsters_to_null",
        "_mem_get_known_monster", "_check_monster_confusion"
    };

    public static IReadOnlyList<string> NativeCombatActionAnchors { get; } = new[]
    {
        "_attack_monster_monster", "_attack_monster_user", "_give_monsters_a_free_attack",
        "_monster_cast", "_monster_cast_area"
    };

    public static IReadOnlyList<string> NativeAiBoundaryAnchors =>
        NativeUpdateLaneAnchors
            .Concat(NativeTargetSelectionAnchors)
            .Concat(NativeMovementTrackingAnchors)
            .Concat(NativeKnownMonsterMemoryAnchors)
            .Concat(NativeCombatActionAnchors)
            .ToArray();

    public static int ResolveRandomRoamThreshold(int followPercent)
    {
        if (followPercent is < short.MinValue or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(followPercent));
        // Native signed arithmetic performs truncation toward zero. Stock Classic Follow is 0..100.
        return (100 - followPercent) / 2;
    }

    public static bool MonsterTypeUsesProbabilisticRoam(int monsterType) => monsterType is 1 or 3 or 4 || monsterType > 5;
    public static bool MonsterTypeUsesUnconditionalRoam(int monsterType) => monsterType == 5;
    public static bool MonsterTypeHoldsWithoutRoute(int monsterType) => monsterType is 0 or 2;

    public static bool ConfusionFumbles(int confusionPercent, int nativeRoll)
    {
        if (nativeRoll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(nativeRoll));
        return confusionPercent > 0 && nativeRoll < confusionPercent;
    }

    public static bool FastFollowChancePasses(int followPercent, int nativeRoll, bool forcedFollowFlag)
    {
        if (nativeRoll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(nativeRoll));
        return forcedFollowFlag || nativeRoll < followPercent;
    }

    public static bool FastFollowShouldAbandonAfterFailure(int retryCount) => retryCount > NativeFastFollowRetryLimit;

    public static void RequireClassicFleeSemantics() { if (!ExactFleeThresholdAndTriggerResolved) throw new InvalidOperationException("native-npc-ai-flee-semantics-unresolved"); }
    public static void RequireClassicPerceptionAggroSemantics() { if (!ExactPerceptionAggroAcquisitionResolved) throw new InvalidOperationException("native-npc-ai-perception-aggro-unresolved"); }
    public static void RequireClassicThreatSwitchOrdering() { if (!ExactThreatTargetSwitchOrderingResolved) throw new InvalidOperationException("native-npc-ai-threat-switch-order-unresolved"); }
    public static void RequireClassicPatrolPathing() { if (!ExactPatrolPathingResolved) throw new InvalidOperationException("native-npc-ai-patrol-pathing-unresolved"); }
    public static void RequireClassicTrackingSemantics() { if (!ExactTrackingSemanticsResolved) throw new InvalidOperationException("native-npc-ai-tracking-semantics-unresolved"); }
    public static void RequireClassicAssistPriorityPropagation() { if (!ExactAssistPriorityPropagationResolved) throw new InvalidOperationException("native-npc-ai-assist-priority-unresolved"); }
    public static void RequireClassicUpdateCadencePriority() { if (!ExactNativeUpdateCadencePriorityResolved) throw new InvalidOperationException("native-npc-ai-update-cadence-priority-unresolved"); }
}
