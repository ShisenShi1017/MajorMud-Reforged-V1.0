using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicCharacterStatusClosureSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicCharacterStatusClosureSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, bool value)
        {
            checks.Add(name + ":" + value);
            if (!value) failures.Add(name);
        }
        static bool Accepted(Action action) { try { action(); return true; } catch (InvalidOperationException) { return false; } }

        var identityLaw = RuntimeIdentityLawParitySmokeReport.Run();
        Check("identity-law-existing-smoke", identityLaw.Passed);
        Check("identity-shadow-evidence-zero-failures", RuntimeClassicIdentityShadowEvidenceRegistry.Validate().Count == 0);
        Check("modifier-order-evidence-zero-failures", RuntimeCharacterModifierOrderEvidenceRegistry.Validate().Count == 0);

        Check("given-name-min-1", RuntimeIdentity.ClassicGivenNameMinimumLength == 1);
        Check("given-name-max-10", RuntimeIdentity.ClassicGivenNameMaximumLength == 10);
        Check("family-name-min-1", RuntimeIdentity.ClassicFamilyNameMinimumLength == 1);
        Check("family-name-max-18", RuntimeIdentity.ClassicFamilyNameMaximumLength == 18);
        Check("observed-blank-lastname-enabled", RuntimeIdentity.ClassicBlankLastNameAllowed);
        Check("one-letter-name-valid", Accepted(() => RuntimeIdentity.ValidateClassicGivenName("A")));
        Check("apostrophe-name-invalid", !Accepted(() => RuntimeIdentity.ValidateClassicGivenName("O'Neil")));
        Check("hyphen-name-invalid", !Accepted(() => RuntimeIdentity.ValidateClassicGivenName("A-B")));
        Check("given-invalid-message-exact", RuntimeIdentity.ClassicInvalidGivenNameMessage == "You have entered an invalid name!");
        Check("last-invalid-message-exact", RuntimeIdentity.ClassicInvalidFamilyNameMessage == "Your last name contains invalid characters!");
        Check("name-in-use-message-exact", RuntimeIdentity.ClassicNameAlreadyInUseMessage == "The name you have chosen is already in use!");

        Check("shadow-ability-9", RuntimeClassicCharacterAppearance.ShadowAbilityId == 9);
        Check("shadow-look-exact-no-punctuation", RuntimeClassicCharacterAppearance.ShadowLookPayload == "A shadowy figure");
        Check("no-universal-modifier-pipeline", RuntimeCharacterModifierOrderEvidenceRegistry.UniversalCrossCategoryOrderResolved && !RuntimeCharacterModifierOrderEvidenceRegistry.UniversalCrossCategoryOrderExists);
        Check("owner-local-modifier-order-authoritative", RuntimeCharacterModifierOrderEvidenceRegistry.OwnerLocalProjectionOrderIsAuthoritative);

        Check("pvp-minimum-level-4", RuntimeClassicDeathLawPvpAuthority.MinimumPvpLevel == 4);
        Check("pvp-disabled-minus-one", !RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(-1));
        Check("pvp-level-3-denied", !RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(3, 3, 0));
        Check("pvp-level-4-admitted", RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(4, 4, 0));
        Check("fiend-threshold-300-still-admitted", RuntimeCharacterAlignmentResolver.CanInitiateAttack(new RuntimeCharacterAlignmentState(300, "Fiend", false, 0, 0, "proven")));
        Check("fiend-threshold-301-denied", !RuntimeCharacterAlignmentResolver.CanInitiateAttack(new RuntimeCharacterAlignmentState(301, "Fiend", false, 0, 0, "proven")));
        Check("player-attack-base-10", RuntimeClassicDeathLawPvpAuthority.PlayerAttackBaseEvilPoints == 10);
        Check("player-attack-seedy-zero", RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(30, false) == 0);
        Check("player-attack-neutral-ten", RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(0, false) == 10);
        Check("player-attack-good-twenty", RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(-100, false) == 20);
        Check("player-attack-saint-thirty", RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(-201, false) == 30);
        Check("player-attack-lawful-thirty", RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(0, true) == 30);
        Check("evil-relation-11-slow-rounds", RuntimeClassicDeathLawPvpAuthority.EvilRelationSlowUpdateRounds == 11);

        Check("pvp-native-messages-resolved", RuntimeClassicPvpPolicyAuthority.ExactPvpMessagesResolved);
        Check("pvp-native-branch-order-resolved", RuntimeClassicPvpPolicyAuthority.ExactPvpBranchOrderingResolved);
        Check("pvp-generic-party-ban-rejected", !RuntimeClassicPvpPolicyAuthority.GenericSamePartyRestrictionIsNativeClassic);
        Check("pvp-generic-guild-ban-rejected", !RuntimeClassicPvpPolicyAuthority.GenericSameGuildRestrictionIsNativeClassic);
        Check("pvp-generic-cooldown-rejected", !RuntimeClassicPvpPolicyAuthority.GenericAttackCooldownIsNativeClassic);
        Check("pvp-generic-logout-restriction-rejected", !RuntimeClassicPvpPolicyAuthority.GenericLogoutRestrictionIsNativeClassic);

        Check("law-lawful-marker-f6", RuntimeClassicLawBountyGuardAuthority.NativeLawfulMarker == 0xF6);
        Check("law-player-attack-base-10", RuntimeClassicLawBountyGuardAuthority.NativePlayerAttackBaseEvilPoints == 10);
        Check("law-robbery-base-1", RuntimeClassicLawBountyGuardAuthority.NativeRobberyBaseEvilPoints == 1);
        Check("law-forgive-relation-11", RuntimeClassicLawBountyGuardAuthority.NativeEvilRelationSlowUpdateRounds == 11);
        Check("law-forgive-accounting-resolved", RuntimeClassicLawBountyGuardAuthority.ExactForgiveSchedulingAccountingResolved);
        Check("law-messages-resolved", RuntimeClassicLawBountyGuardAuthority.ExactLawMessagesResolved);
        Check("law-evil-point-serialization-resolved", RuntimeClassicLawBountyGuardAuthority.ExactEvilPointSerializationResolved);
        Check("law-jail-order-resolved", RuntimeClassicLawBountyGuardAuthority.ExactJailExecutionOrderingResolved);
        Check("law-generic-bounty-rejected", !RuntimeClassicLawBountyGuardAuthority.GenericBountyAmountIsNativeClassic);
        Check("law-generic-crime-decay-rejected", !RuntimeClassicLawBountyGuardAuthority.GenericCrimeDecayDeadlineIsNativeClassic);

        return new(failures.Count == 0, checks, failures);
    }
}
