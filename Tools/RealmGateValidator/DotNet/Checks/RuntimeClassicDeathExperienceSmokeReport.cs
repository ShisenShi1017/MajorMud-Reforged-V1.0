using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicDeathExperienceSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicDeathExperienceSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        var ordinary = RuntimeClassicDeathExperienceAuthority.PreserveOrdinaryDeathExperience(987_654_321, 2);
        Check(ordinary.ExperienceAfter == ordinary.ExperienceBefore && ordinary.ExperienceLost == 0,
            "ordinary-death-preserves-experience");

        var chart = 100;
        var thresholdFloor = checked(RuntimeClassicCharacterProgressionFormula.RequiredExperienceForChart(10, chart) + 1);
        Check(RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
                1, 20, chart, playerKeepExperienceEnabled: true, globalKeepExperienceEnabled: true) == thresholdFloor,
            "final-reroll-half-level-threshold-floor");
        Check(RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
                1_000_000_000, 20, chart, playerKeepExperienceEnabled: true, globalKeepExperienceEnabled: true) == 100_000_000,
            "final-reroll-ten-percent-floor");
        Check(RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
                100_000_009, 20, chart, playerKeepExperienceEnabled: true, globalKeepExperienceEnabled: true) >= 10_000_000,
            "final-reroll-has-no-two-million-cap");
        Check(RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
                1_000_000_000, 20, chart, playerKeepExperienceEnabled: false, globalKeepExperienceEnabled: true) == 0,
            "player-keep-option-gates-retention");
        Check(RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
                1_000_000_000, 20, chart, playerKeepExperienceEnabled: true, globalKeepExperienceEnabled: false) == 0,
            "global-keep-option-gates-retention");
        Check(RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
                1_000_000_000, 1, chart, playerKeepExperienceEnabled: true, globalKeepExperienceEnabled: true) == 0,
            "level-one-reroll-retains-zero");
        Check(RuntimeClassicDeathExperienceAuthority.FinalRerollConsumerConnected,
            "final-reroll-consumer-connected");
        var tempRoot = Path.Combine(Path.GetTempPath(), "mmr-death-xp-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            RuntimeClassicPermanentDeathInfoStore.Save(tempRoot, "account", "Classic", "source", 123456, DateTimeOffset.UnixEpoch);
            Check(RuntimeClassicPermanentDeathInfoStore.Peek(tempRoot, "account", "Classic")?.RetainedExperience == 123456,
                "permanent-info-retained-xp-persisted");
            Check(RuntimeClassicPermanentDeathInfoStore.Consume(tempRoot, "account", "Classic") == 123456,
                "permanent-info-retained-xp-consumed-once");
            Check(RuntimeClassicPermanentDeathInfoStore.Consume(tempRoot, "account", "Classic") == 0,
                "permanent-info-retained-xp-second-consume-empty");
        }
        finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }

        var catalog = RuntimeItemCatalog.Load(projectRoot, "Classic");
        var character = ProbeCharacter(2, 987_654_321);
        var transition = RuntimeCharacterDeathResolver.Defeat(character, catalog, character.RoomId, DateTimeOffset.UnixEpoch.AddHours(1));
        Check(transition.Character.Experience == character.Experience,
            "live-ordinary-death-resolver-preserves-experience");
        Check(transition.Character.SemanticState?.RuntimeMarkers.GetValueOrDefault(RuntimeCharacterDeathMarkers.ExperienceLost) == "0",
            "live-ordinary-death-records-zero-loss");

        return new(failures.Count == 0, checks, failures);
    }

    private static RuntimeCharacterRecord ProbeCharacter(int lives, long experience) =>
        new(
            "pass49-probe", "account", "Pass49 Probe", "Classic", "race.1", "Human", "class.1", "Warrior",
            20, experience, 100, 100, 0, 0, "room.map1.2140", 2140,
            new Dictionary<string, int>(), new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lives"] = lives.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            ItemState: new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>()));
}
