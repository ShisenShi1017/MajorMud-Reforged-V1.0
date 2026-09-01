using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeIdentityLawParitySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeIdentityLawParitySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { checks.Add(id); if (!condition) failures.Add(id); }
        static bool Accepted(Action action) { try { action(); return true; } catch (InvalidOperationException) { return false; } }

        Check(RuntimeIdentity.ClassicGivenNameMaximumLength == 10, "classic-given-name-max-10");
        Check(RuntimeIdentity.ClassicFamilyNameMaximumLength == 18, "classic-family-name-max-18");
        Check(Accepted(() => RuntimeIdentity.ValidateClassicGivenName("A")), "classic-given-name-1-accepted");
        Check(Accepted(() => RuntimeIdentity.ValidateClassicGivenName("Abcdefghij")), "classic-given-name-10-accepted");
        Check(!Accepted(() => RuntimeIdentity.ValidateClassicGivenName("Ab-C")), "classic-given-name-punctuation-rejected");
        Check(!Accepted(() => RuntimeIdentity.ValidateClassicGivenName("Abcdefghijk")), "classic-given-name-11-rejected");
        Check(Accepted(() => RuntimeIdentity.ValidateClassicFamilyName(string.Empty)), "classic-blank-family-name-observed-config-accepted");
        Check(Accepted(() => RuntimeIdentity.ValidateClassicFamilyName("A")), "classic-family-name-1-accepted");
        Check(Accepted(() => RuntimeIdentity.ValidateClassicFamilyName("Abcdefghijklmnopqr")), "classic-family-name-18-accepted");
        Check(!Accepted(() => RuntimeIdentity.ValidateClassicFamilyName("O'Neil")), "classic-family-name-punctuation-rejected");
        Check(!Accepted(() => RuntimeIdentity.ValidateClassicFamilyName("Abcdefghijklmnopqrs")), "classic-family-name-19-rejected");

        Check(RuntimeClassicDeathLawPvpAuthority.LawfulAttackPenaltyMultiplier == 3, "lawful-attack-multiplier-3");
        Check(RuntimeClassicDeathLawPvpAuthority.ApplyLawfulAttackMultiplier(2, true) == 6, "lawful-attack-multiplier-applies");
        Check(RuntimeClassicDeathLawPvpAuthority.ResolveLawConsequence("Outlaw", 0) == RuntimeClassicLawConsequence.GuardsAttackUntilDowned,
            "outlaw-guards-spare-life");
        Check(RuntimeClassicDeathLawPvpAuthority.ResolveLawConsequence("Criminal", 0) == RuntimeClassicLawConsequence.GuardsAttackUntilDeath,
            "criminal-guards-slay");
        Check(RuntimeClassicDeathLawPvpAuthority.ResolveLawConsequence("Villain", 0) ==
              (RuntimeClassicLawConsequence.GuardsAttackUntilDeath | RuntimeClassicLawConsequence.DivineInterventionEligible),
            "villain-criminal-plus-divine-eligibility");
        Check(RuntimeClassicDeathLawPvpAuthority.ResolveLawConsequence("Fiend", 300) == RuntimeClassicLawConsequence.None,
            "fiend-300-ep-can-still-initiate");
        Check(RuntimeClassicDeathLawPvpAuthority.ResolveLawConsequence("Fiend", 301) == RuntimeClassicLawConsequence.AttackInitiationLocked,
            "fiend-over-300-ep-attack-lockout");

        Check(!RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(-1), "pvp-lvldiff-minus1-disabled");
        Check(RuntimeClassicDeathLawPvpAuthority.IsPvpEnabled(0), "pvp-lvldiff-zero-enabled");
        Check(!RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(3, 3, 0), "pvp-level-below-four-blocked");
        Check(RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(4, 4, 0), "pvp-level-four-admitted");
        Check(RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(10, 10, 0), "pvp-lvldiff-zero-same-level");
        Check(!RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(10, 11, 0), "pvp-lvldiff-zero-blocks-different-level");
        Check(RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(20, 10, 10), "pvp-lvldiff-boundary-admitted");
        Check(!RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(21, 10, 10), "pvp-lvldiff-over-boundary-denied");
        Check(RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(50, 10, 10, defenderIsFiend: true),
            "pvp-lower-level-fiend-not-protected");
        Check(!RuntimeClassicDeathLawPvpAuthority.IsLevelRangeAllowed(10, 50, 10, defenderIsFiend: true),
            "pvp-higher-level-fiend-no-exception");
        Check(RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(30, false) == 0, "pvp-seedy-target-zero-ep");
        Check(RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(0, false) == 10, "pvp-neutral-target-ten-ep");
        Check(RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(-100, false) == 20, "pvp-good-target-twenty-ep");
        Check(RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(-201, false) == 30, "pvp-saint-target-thirty-ep");
        Check(RuntimeClassicDeathLawPvpAuthority.CalculatePlayerAttackEvilPoints(0, true) == 30, "pvp-lawful-target-thirty-ep");
        Check(RuntimeClassicDeathLawPvpAuthority.EvilRelationSlowUpdateRounds == 11, "pvp-evil-relation-eleven-slow-rounds");

        return new(failures.Count == 0, checks, failures);
    }
}
