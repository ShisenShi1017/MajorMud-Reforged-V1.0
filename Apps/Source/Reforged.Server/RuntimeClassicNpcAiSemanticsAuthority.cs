namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD native NPC-AI ownership boundary.
///
/// WCCMMUD/WBBMMUD expose dedicated monster update, target-selection, movement/tracking,
/// known-monster memory, and combat-action entry points. Those exports establish native
/// ownership families, while decoded monster fields and existing Strong runtime behavior
/// constrain the executable Reforged side. Export names alone do not prove branch order,
/// thresholds, target-switch math, patrol routes, tracking rules, or update cadence.
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
    public const bool ExistingStrongThreatLedgerRuntimeActive = true;
    public const bool GenericSchedulerOrderingIsNativeClassic = false;
    public const bool GenericDecisionPriorityIsNativeClassic = false;

    public const bool ExactFleeThresholdAndTriggerResolved = false;
    public const bool ExactPerceptionAggroAcquisitionResolved = false;
    public const bool ExactThreatTargetSwitchOrderingResolved = false;
    public const bool ExactPatrolPathingResolved = false;
    public const bool ExactTrackingSemanticsResolved = false;
    public const bool ExactAssistPriorityPropagationResolved = false;
    public const bool ExactNativeUpdateCadencePriorityResolved = false;

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

    public static void RequireClassicFleeSemantics()
    {
        if (!ExactFleeThresholdAndTriggerResolved)
            throw new InvalidOperationException("native-npc-ai-flee-semantics-unresolved");
    }

    public static void RequireClassicPerceptionAggroSemantics()
    {
        if (!ExactPerceptionAggroAcquisitionResolved)
            throw new InvalidOperationException("native-npc-ai-perception-aggro-unresolved");
    }

    public static void RequireClassicThreatSwitchOrdering()
    {
        if (!ExactThreatTargetSwitchOrderingResolved)
            throw new InvalidOperationException("native-npc-ai-threat-switch-order-unresolved");
    }

    public static void RequireClassicPatrolPathing()
    {
        if (!ExactPatrolPathingResolved)
            throw new InvalidOperationException("native-npc-ai-patrol-pathing-unresolved");
    }

    public static void RequireClassicTrackingSemantics()
    {
        if (!ExactTrackingSemanticsResolved)
            throw new InvalidOperationException("native-npc-ai-tracking-semantics-unresolved");
    }

    public static void RequireClassicAssistPriorityPropagation()
    {
        if (!ExactAssistPriorityPropagationResolved)
            throw new InvalidOperationException("native-npc-ai-assist-priority-unresolved");
    }

    public static void RequireClassicUpdateCadencePriority()
    {
        if (!ExactNativeUpdateCadencePriorityResolved)
            throw new InvalidOperationException("native-npc-ai-update-cadence-priority-unresolved");
    }
}
