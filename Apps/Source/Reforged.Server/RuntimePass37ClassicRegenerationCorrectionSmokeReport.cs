using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass37ClassicRegenerationCorrectionSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass37ClassicRegenerationCorrectionSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicRegenerationEvidenceRegistry.Validate().Count == 0, "regeneration-evidence-registry-valid");
        Check(RuntimeCharacterRegenerationAuthority.PassiveCadence == TimeSpan.FromSeconds(30), "passive-cadence-30s");
        Check(RuntimeCharacterRegenerationAuthority.RestCadence == TimeSpan.FromSeconds(20), "rest-cadence-20s");
        Check(RuntimeCharacterRegenerationAuthority.MeditateCadence == TimeSpan.FromSeconds(10), "meditate-cadence-10s");
        Check(RuntimeCharacterRegenerationAuthority.FocusActivationDelay == TimeSpan.FromSeconds(20), "focus-activation-20s");
        Check(RuntimeCharacterRegenerationAuthority.CalculateClassicHitPointBase(1, 1) == 1, "stock-hp-base-minimum-one");
        Check(RuntimeCharacterRegenerationAuthority.CalculateClassicHitPointBase(10, 50) == 2, "stock-hp-base-divisor-750");

        var character = ProbeCharacter();
        var mage = ProbeClass(1, 3);
        var passive = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(character, mage, DateTimeOffset.UnixEpoch);
        var rest = RuntimeCharacterRegenerationAuthority.ApplyRestTick(character, mage, DateTimeOffset.UnixEpoch);
        var meditate = RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(character, mage, DateTimeOffset.UnixEpoch);
        Check(passive.HitPointRate == 3, "passive-hp-percent-after-stock-base");
        Check(rest.HitPointRate == 9, "rest-stock-base-times-three-before-percent");
        Check(passive.ManaRate == 8, "passive-mana-applies-mpregen");
        Check(meditate.ManaRate == 4, "meditation-types1-4-skips-mpregen");

        var statusMana = character with
        {
            Conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeClassicStatusProjection.Prefix + "mana_regeneration"] = "25"
            }
        };
        Check(RuntimeCharacterRegenerationAuthority.ResolveManaRegenerationPercent(statusMana) == 125, "passive-mpregen-stacks-equipment-and-status");
        Check(RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(statusMana, mage, DateTimeOffset.UnixEpoch).ManaRate == 9, "passive-stacked-mpregen-applied");
        Check(RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(statusMana, mage, DateTimeOffset.UnixEpoch).ManaRate == 4, "meditation-stacked-mpregen-still-skipped");

        var kai = ProbeClass(5, 9);
        Check(RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(character, kai, DateTimeOffset.UnixEpoch).ManaRate == 2, "kai-passive-one-point-base-with-mpregen");
        Check(RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(character, kai, DateTimeOffset.UnixEpoch).ManaRate == 2, "kai-meditate-one-point-base-with-mpregen");

        var rejected = RuntimeClassicRegenerationEvidenceRegistry.Rows.Single(row => row.Id == "hp.greatermud-divisor");
        Check(rejected.State == RuntimeClassicRegenerationEvidenceState.RejectedForClassic, "greatermud-divisor-rejected-for-classic");
        Check(!RuntimeClassicRegenerationEvidenceRegistry.ExactHostPhaseResolved, "exact-host-phase-stays-unknown");
        Check(!RuntimeClassicRegenerationEvidenceRegistry.ExactMeditateResponseResolved, "exact-meditate-response-stays-unknown");
        Check(!RuntimeClassicRegenerationEvidenceRegistry.ExactStatusCleanupOrderingResolved, "exact-status-cleanup-order-stays-unknown");

        return new(failures.Count == 0, checks, failures);
    }

    private static RuntimeCharacterRecord ProbeCharacter()
    {
        var semantic = RuntimeCharacterSemanticState.Empty with
        {
            DerivedStats = new Dictionary<string, int>
            {
                ["hit_point_regeneration_percent"] = 50,
                ["mana_regeneration_percent"] = 100
            }
        };
        return new RuntimeCharacterRecord(
            "pass37-probe", "account", "Probe", "Classic", "race.1", "Human", "class.12", "Mage",
            10, 0, 10, 100, 5, 100, "room.map1.1", 1,
            new Dictionary<string, int> { ["hea"] = 50, ["int"] = 50, ["wil"] = 50, ["cha"] = 50 },
            new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
            new Dictionary<string, string?>(), new Dictionary<string, string>(),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, SemanticState: semantic);
    }

    private static RuntimeCharacterDefinition ProbeClass(int magicType, int magicLevel) =>
        new("class.pass37", "Pass37", "test", "class",
            new Dictionary<string, int>(), new Dictionary<string, int>(),
            0, 0, 0, Array.Empty<RuntimeAbilityGrant>(), null, new RuntimeMagicDefinition(magicType, magicLevel),
            0, 0, 0, 0, 0, 0, string.Empty, string.Empty, 0, "test");
}
