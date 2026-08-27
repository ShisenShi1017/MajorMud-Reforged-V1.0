using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass29EvilReputationSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass29EvilReputationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { checks.Add(id); if (!condition) failures.Add(id); }

        var thresholds = RuntimeCharacterAlignmentResolver.ClassicReputationMinimumEvilPoints;
        Check(thresholds.Count == 8, "classic-reputation-eight-reference-bands");
        Check(thresholds["Saint"] == -220 && thresholds["Good"] == -200 && thresholds["Neutral"] == -50,
            "classic-good-side-reference-points");
        Check(thresholds["Seedy"] == 30 && thresholds["Outlaw"] == 40 && thresholds["Criminal"] == 80 &&
              thresholds["Villain"] == 120 && thresholds["Fiend"] == 210,
            "classic-evil-side-reference-points");

        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(-201) == "Saint", "ep-minus201-saint");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(-200) == "Good", "ep-minus200-good");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(-50) == "Neutral", "ep-minus50-neutral");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(29) == "Neutral", "ep-29-neutral");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(30) == "Seedy", "ep-30-seedy");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(40) == "Outlaw", "ep-40-outlaw");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(80) == "Criminal", "ep-80-criminal");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(120) == "Villain", "ep-120-villain");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(209) == "Villain", "ep-209-villain");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(210) == "Fiend", "ep-210-fiend");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(237) == "Fiend", "ep-237-fiend-omnimud-corroboration");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(300) == "Fiend", "ep-300-remains-fiend-with-separate-lockout");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(0, "Lawful") == "Lawful", "lawful-pledge-not-erased-by-ep-band-resolver");

        var neutral29 = new RuntimeCharacterAlignmentState(29, "Neutral", false, 0, 0, "proven", "pass29-smoke");
        var crossedToSeedy = RuntimeCharacterAlignmentResolver.ApplyEvilPointDelta(neutral29, 1);
        Check(crossedToSeedy.EvilPoints == 30 && crossedToSeedy.Reputation == "Seedy" && crossedToSeedy.PerformedEvilActionToday,
            "ep-delta-crosses-neutral-to-seedy");

        var cleanupConfig = new RuntimeCharacterAlignmentCleanupConfiguration(2, 3, 60, 1, 18);
        var outlaw40 = new RuntimeCharacterAlignmentState(40, "Outlaw", false, 0, 0, "proven", "pass29-smoke");
        var redeemed = RuntimeCharacterAlignmentResolver.ApplyDailyCleanup(outlaw40, cleanupConfig);
        Check(redeemed.EvilPoints == 37 && redeemed.Reputation == "Seedy" && redeemed.DailyReductionApplied == 3,
            "cleanup-crosses-outlaw-to-seedy");

        var longSession = new RuntimeCharacterAlignmentState(237, "Fiend", false, 24 * 60, 0, "proven", "pass29-smoke");
        var capped = RuntimeCharacterAlignmentResolver.ApplyDailyCleanup(longSession, cleanupConfig);
        Check(capped.DailyReductionApplied == 18 && capped.EvilPoints == 219 && capped.Reputation == "Fiend",
            "maxepday-18-cap-enforced");

        var good = new RuntimeCharacterAlignmentState(-199, "Good", false, 60, 0, "proven", "pass29-smoke");
        var towardSaint = RuntimeCharacterAlignmentResolver.ApplyDailyCleanup(good, cleanupConfig);
        Check(towardSaint.EvilPoints == -201 && towardSaint.Reputation == "Saint",
            "negative-ep-redemption-crosses-good-to-saint");

        return new(failures.Count == 0, checks, failures);
    }
}
