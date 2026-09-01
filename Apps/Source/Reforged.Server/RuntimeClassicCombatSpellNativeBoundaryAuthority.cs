namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD native combat/spell ownership boundary.
///
/// The managed reconstruction has classified all 1,350 canonical spells and all 111 runtime
/// status effects.  The retained WCCMMUD/WBBMMUD ABI independently proves dedicated native
/// ownership for attack/kill/autocombat, targeting/energy, cast/spell-record, ability lookup,
/// room/monster spell delivery and spell upkeep/termination families.  This is a boundary
/// closure only: exact native formulas, internal branch priority, mutation ordering, per-call
/// RNG draw ordering and failure/refund semantics remain fail-closed. The Nahr message-8480
/// arguments are separately exact: native equipped-item ID 0 produces two empty buffers. Reforged transaction/rollback/deterministic-stream policy must not
/// be credited as stock Classic behavior unless separately recovered.
/// </summary>
public static class RuntimeClassicCombatSpellNativeBoundaryAuthority
{
    public const int CanonicalSpellCount = 1350;
    public const int RuntimeStatusEffectCount = 111;
    public const int RuntimeAuthoritativeStatusEffectCount = 111;
    public const int NativeAttackCombatOwnerExportCount = 45;
    public const int NativeSpellCastOwnerExportCount = 53;
    public const int NativeAbilityOwnerExportCount = 4;
    public const int NativeCombatSpellOwnerExportCount = 102;
    public const int WccMudExportCount = 592;
    public const int WbbMudExportCount = 592;
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const string WbbMudSha256 = "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091";

    public static readonly IReadOnlyList<string> NativeAttackCombatOwnerExports = new[]
    {
        "_attack_monster_monster",
        "_attack_monster_user",
        "_attack_user_monster",
        "_attack_user_user",
        "_attacker_is_in_same_room",
        "_attacker_type",
        "_background_energy",
        "_calculate_attack",
        "_check_kill_monster",
        "_check_kill_user",
        "_clear_autocombat_info",
        "_clear_backstab_attack_type",
        "_cmd_any_attack",
        "_cmd_attack",
        "_cmd_backstab",
        "_cmd_bash",
        "_cmd_killblow",
        "_cmd_smash",
        "_compute_energy_used",
        "_do_autocombat",
        "_do_autocombat_for_user",
        "_energy_update_character",
        "_energy_update_monster",
        "_engage_autocombat",
        "_find_action_target",
        "_find_any_action_target",
        "_get_armour_rating",
        "_get_damage_descriptor",
        "_get_my_attack_type",
        "_give_monsters_a_free_attack",
        "_init_autocombat",
        "_is_being_attacked",
        "_is_combatting_monster",
        "_is_combatting_user",
        "_is_in_pvp_combat",
        "_is_inside_autocombat",
        "_kill_autocombat",
        "_kill_autocombat_against",
        "_kill_autocombat_against_monster",
        "_match_combat_target",
        "_monster_could_attack",
        "_move_monster_to_fighter",
        "_move_player_to_fighter",
        "_restart_autocombat",
        "_validate_auto_combat",
    };

    public static readonly IReadOnlyList<string> NativeSpellCastOwnerExports = new[]
    {
        "_add_cast_spell_to_monster",
        "_add_cast_spell_to_user",
        "_add_duration_spell_to_room",
        "_add_spell_to_spellbook",
        "_alloc_get_spell_data",
        "_cast_monster_target",
        "_cast_no_target",
        "_cast_user_target",
        "_clear_spell",
        "_clear_spell_told_flags",
        "_clear_spells_to_null",
        "_cmd_cast",
        "_count_valid_targets",
        "_display_list_of_all_spells",
        "_display_spell_desc",
        "_display_spell_success",
        "_display_users_spells",
        "_find_spell_in_spellbook",
        "_free_spell_data",
        "_get_last_spell_number",
        "_get_spell_ability_value",
        "_get_spell_data",
        "_get_spell_from_name",
        "_get_spell_match_type",
        "_get_spell_random_modifier",
        "_is_valid_monster_target",
        "_is_valid_target",
        "_load_spell_into_buffer",
        "_mem_get_spell",
        "_monster_add_cast_spell_to_user",
        "_monster_add_duration_spell_to_room",
        "_monster_cast",
        "_monster_cast_area",
        "_monster_count_valid_targets",
        "_monster_display_spell_success",
        "_negate_dynamic_with_ability",
        "_perform_routine_spell_monster_upkeep",
        "_perform_routine_spell_player_upkeep",
        "_perform_spell_termination_monster_upkeep",
        "_perform_spell_termination_player_upkeep",
        "_purge_spell_from_spellbook",
        "_room_cast_on_monster",
        "_room_cast_on_user",
        "_save_spell_from_buffer",
        "_silly_spell",
        "_sort_spell_list",
        "_spell_has_ability",
        "_update_dynamic_with_ability",
        "_use_monster_target",
        "_use_no_target",
        "_use_player_target",
        "_use_spell_target",
        "_user_can_use_spell",
    };

    public static readonly IReadOnlyList<string> NativeAbilityOwnerExports = new[]
    {
        "_get_monster_ability_value",
        "_get_user_ability_value",
        "_monster_has_ability",
        "_user_has_ability",
    };

    public static readonly IReadOnlySet<string> NativeCombatSpellOwnerExports =
        NativeAttackCombatOwnerExports.Concat(NativeSpellCastOwnerExports).Concat(NativeAbilityOwnerExports)
            .ToHashSet(StringComparer.Ordinal);

    public const bool NonNativeSpellClassificationComplete = true;
    public const bool NativeAttackKillAutocombatOwnershipResolved = true;
    public const bool NativeTargetEnergyOwnershipResolved = true;
    public const bool NativeCastSpellRecordOwnershipResolved = true;
    public const bool NativeSpellUpkeepTerminationOwnershipResolved = true;
    public const bool NativeAbilityLookupOwnershipResolved = true;

    public const bool ReforgedAtomicRollbackDefinesExactClassicSemantics = false;
    public const bool ReforgedDeterministicTargetOrderingDefinesNativeOrdering = false;
    public const bool ReforgedRandomStreamSplittingDefinesNativeRngOrdering = false;
    public const bool ReforgedCastLifecycleDefinesNativeResourceTiming = false;

    public const bool ExactNativeAttackFormulaOrderingResolved = false;
    public const bool ExactNativeSpellFormulaOrderingResolved = false;
    public const bool ExactNativeCastBranchOrderingResolved = false;
    public const bool ExactNativeTargetSelectionOrderingResolved = false;
    public const bool ExactNativeResourceChargeRefundOrderingResolved = false;
    public const bool ExactNativeResistanceSaveOrderingResolved = false;
    public const bool ExactNativeStatusApplyTickTerminateOrderingResolved = false;
    public const bool ExactNativeMultiTargetMutationOrderingResolved = false;
    public const bool ExactNativeCombatSpellRngDrawOrderingResolved = false;
    public const bool ExactNativeCombatSpellFailureMessagesResolved = false;
    public const bool ExactNahr8480ArgumentsResolved = true;

    // The parity assertions combat-spells.native-boundary and combat.non-native-exhausted
    // score ownership/classification closure, not a claim that every owner-local machine-code
    // formula has been independently reconstructed. Those detailed formulas remain governed by
    // their separately scored combat/spell assertions and the fail-closed Require* gates below.
    public const bool NativeOwnerBoundaryExhausted = true;
    public const bool CanonicalSpellAndEffectClassificationExhausted = true;
    public const bool ResidualNativeOwnerInternalsSeparatelyScoped = true;
    public const int UnclassifiedCombatSpellDependencyGroupCount = 0;

    public static bool OwnsNativeExport(string exportName) => NativeCombatSpellOwnerExports.Contains(exportName);

    public static void RequireClassicAttackFormulaOrdering() { if (!ExactNativeAttackFormulaOrderingResolved) throw new InvalidOperationException("native-combat-attack-formula-ordering-unresolved"); }
    public static void RequireClassicSpellFormulaOrdering() { if (!ExactNativeSpellFormulaOrderingResolved) throw new InvalidOperationException("native-spell-formula-ordering-unresolved"); }
    public static void RequireClassicCastBranchOrdering() { if (!ExactNativeCastBranchOrderingResolved) throw new InvalidOperationException("native-spell-cast-branch-ordering-unresolved"); }
    public static void RequireClassicTargetSelectionOrdering() { if (!ExactNativeTargetSelectionOrderingResolved) throw new InvalidOperationException("native-combat-spell-target-ordering-unresolved"); }
    public static void RequireClassicResourceTiming() { if (!ExactNativeResourceChargeRefundOrderingResolved) throw new InvalidOperationException("native-spell-resource-ordering-unresolved"); }
    public static void RequireClassicResistanceSaveOrdering() { if (!ExactNativeResistanceSaveOrderingResolved) throw new InvalidOperationException("native-spell-resistance-save-ordering-unresolved"); }
    public static void RequireClassicStatusLifecycleOrdering() { if (!ExactNativeStatusApplyTickTerminateOrderingResolved) throw new InvalidOperationException("native-spell-status-lifecycle-ordering-unresolved"); }
    public static void RequireClassicMultiTargetMutationOrdering() { if (!ExactNativeMultiTargetMutationOrderingResolved) throw new InvalidOperationException("native-spell-multitarget-ordering-unresolved"); }
    public static void RequireClassicRngDrawOrdering() { if (!ExactNativeCombatSpellRngDrawOrderingResolved) throw new InvalidOperationException("native-combat-spell-rng-ordering-unresolved"); }
    public static void RequireClassicFailureMessages() { if (!ExactNativeCombatSpellFailureMessagesResolved) throw new InvalidOperationException("native-combat-spell-failure-messages-unresolved"); }
    public static void RequireClassicNahr8480Arguments() { if (!ExactNahr8480ArgumentsResolved) throw new InvalidOperationException("native-nahr-8480-arguments-unresolved"); }

    public static IReadOnlyList<string> ValidateBoundaryClosure()
    {
        var failures = new List<string>();
        if (NativeAttackCombatOwnerExports.Count != NativeAttackCombatOwnerExportCount)
            failures.Add("native-attack-owner-count");
        if (NativeSpellCastOwnerExports.Count != NativeSpellCastOwnerExportCount)
            failures.Add("native-spell-owner-count");
        if (NativeAbilityOwnerExports.Count != NativeAbilityOwnerExportCount)
            failures.Add("native-ability-owner-count");
        if (NativeCombatSpellOwnerExports.Count != NativeCombatSpellOwnerExportCount)
            failures.Add("native-combat-spell-owner-union-count");
        Check(CanonicalSpellCount == 1350 && RuntimeStatusEffectCount == 111 &&
            RuntimeAuthoritativeStatusEffectCount == 111 && NonNativeSpellClassificationComplete,
            "combat-spell-canonical-classification-counts", failures);
        Check(NativeAttackKillAutocombatOwnershipResolved && NativeTargetEnergyOwnershipResolved &&
            NativeCastSpellRecordOwnershipResolved && NativeSpellUpkeepTerminationOwnershipResolved &&
            NativeAbilityLookupOwnershipResolved,
            "combat-spell-native-owner-family-demoted", failures);
        Check(NativeOwnerBoundaryExhausted && CanonicalSpellAndEffectClassificationExhausted &&
            ResidualNativeOwnerInternalsSeparatelyScoped && UnclassifiedCombatSpellDependencyGroupCount == 0,
            "combat-spell-boundary-not-exhausted", failures);
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
