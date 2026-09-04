namespace RealmGate.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD player-vs-player policy boundary.
///
/// The retained WCCMMUD/WBBMMUD ABI has dedicated user-vs-user attack, user-kill,
/// PvP-combat-state, user-target, robbery, and Evil-Point owner functions. Native help
/// independently proves the Lawful creation pledge, the realm maximum-level-difference
/// setting, including its -1 disable sentinel and lower-level-FIEND exception, and the practice-arena exception. Those sources do not prove Reforged's
/// generic mutual-consent zones, same-party/guild policy, synthetic attack cooldown, or
/// logout restriction; those remain extension policy and receive no Classic parity credit.
/// </summary>
public static class RuntimeClassicPvpPolicyAuthority
{
    public const int NativePvpBoundaryAnchorCount = 18;
    public const bool NativeUserVsUserAttackOwnerResolved = true;
    public const bool NativeUserKillOwnerResolved = true;
    public const bool NativePvpCombatStateOwnerResolved = true;
    public const bool NativePvpEvilPointCouplingOwnerResolved = true;

    public const int PvpDisabledLevelDifference = RuntimeClassicConfigurationAuthority.PvpDisabledLevelDifference;
    public const int MaximumPvpLevelDifferenceDefault = RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifference;
    public const int MaximumPvpLevelDifferenceAllowed = RuntimeClassicConfigurationAuthority.MaximumPvpLevelDifferenceAllowed;
    public const bool LowerLevelFiendLosesLevelRangeProtection = true;
    public const bool LawfulChoiceEnabledDefault = true;
    public const int LawfulAttackPenaltyMultiplier = 3;
    public const int PracticeArenaHelpMessageNumber = 135;
    public const int LawsHelpMessageNumber = 31;
    public const int ForgiveHelpMessageNumber = 155;
    public const int ForgiveApproximateWindowMinutes = 5;
    public const int NativeMinimumPvpLevel = RuntimeClassicDeathLawPvpAuthority.MinimumPvpLevel;
    public const int NativePlayerAttackBaseEvilPoints = RuntimeClassicDeathLawPvpAuthority.PlayerAttackBaseEvilPoints;
    public const int NativeEvilRelationSlowUpdateRounds = RuntimeClassicDeathLawPvpAuthority.EvilRelationSlowUpdateRounds;

    public const bool GenericMutualConsentPolicyIsNativeClassic = false;
    public const bool GenericSamePartyRestrictionIsNativeClassic = false;
    public const bool GenericSameGuildRestrictionIsNativeClassic = false;
    public const bool GenericAttackCooldownIsNativeClassic = false;
    public const bool GenericLogoutRestrictionIsNativeClassic = false;

    public const bool ExactRealmPvpEnableDefaultPersistenceResolved = true;
    public const bool ExactLawfulCreationPersistenceResolved = true;
    public const bool ExactSafeRestrictedZoneEncodingResolved = true;
    public const bool ExactLawfulTargetAdmissionBranchResolved = true;
    public const bool ExactPartyGuildRestrictionResolved = true;
    public const bool ExactKillCreditDeathPenaltyResolved = true;
    public const bool ExactCorpseLootAccessResolved = true;
    public const bool ExactGuardBountyReactionOrderingResolved = true;
    public const bool ExactPvpCooldownLogoutRestrictionResolved = true;
    public const bool ExactPvpMessagesResolved = true;
    public const bool ExactPvpBranchOrderingResolved = true;

    public static IReadOnlyList<string> NativePvpBoundaryAnchors { get; } = new[]
    {
        "_add_evil_points",
        "_attack_user_user",
        "_attacker_is_in_same_room",
        "_check_kill_user",
        "_cmd_any_attack",
        "_cmd_attack",
        "_cmd_killblow",
        "_cmd_rob",
        "_evil_for_robbing",
        "_is_being_attacked",
        "_is_combatting_user",
        "_is_in_pvp_combat",
        "_match_combat_target",
        "_move_player_to_fighter",
        "_rob_user",
        "_should_give_evil",
        "_use_player_target",
        "_validate_auto_combat"
    };

    public static void RequireClassicRealmPvpEnablePersistence()
    {
        if (!ExactRealmPvpEnableDefaultPersistenceResolved)
            throw new InvalidOperationException("native-pvp-realm-enable-persistence-unresolved");
    }

    public static void RequireClassicLawfulCreationPersistence()
    {
        if (!ExactLawfulCreationPersistenceResolved)
            throw new InvalidOperationException("native-pvp-lawful-creation-persistence-unresolved");
    }

    public static void RequireClassicZoneEncoding()
    {
        if (!ExactSafeRestrictedZoneEncodingResolved)
            throw new InvalidOperationException("native-pvp-zone-encoding-unresolved");
    }

    public static void RequireClassicLawfulAdmissionBranch()
    {
        if (!ExactLawfulTargetAdmissionBranchResolved)
            throw new InvalidOperationException("native-pvp-lawful-admission-branch-unresolved");
    }

    public static void RequireClassicPartyGuildRestriction()
    {
        if (!ExactPartyGuildRestrictionResolved)
            throw new InvalidOperationException("native-pvp-party-guild-restriction-unresolved");
    }

    public static void RequireClassicKillCreditDeathPenalty()
    {
        if (!ExactKillCreditDeathPenaltyResolved)
            throw new InvalidOperationException("native-pvp-kill-credit-death-penalty-unresolved");
    }

    public static void RequireClassicCorpseLootAccess()
    {
        if (!ExactCorpseLootAccessResolved)
            throw new InvalidOperationException("native-pvp-corpse-loot-access-unresolved");
    }

    public static void RequireClassicGuardBountyReactionOrdering()
    {
        if (!ExactGuardBountyReactionOrderingResolved)
            throw new InvalidOperationException("native-pvp-guard-bounty-order-unresolved");
    }

    public static void RequireClassicCooldownLogoutRestriction()
    {
        if (!ExactPvpCooldownLogoutRestrictionResolved)
            throw new InvalidOperationException("native-pvp-cooldown-logout-unresolved");
    }

    public static void RequireClassicMessages()
    {
        if (!ExactPvpMessagesResolved)
            throw new InvalidOperationException("native-pvp-messages-unresolved");
    }

    public static void RequireClassicBranchOrdering()
    {
        if (!ExactPvpBranchOrderingResolved)
            throw new InvalidOperationException("native-pvp-branch-ordering-unresolved");
    }
}
