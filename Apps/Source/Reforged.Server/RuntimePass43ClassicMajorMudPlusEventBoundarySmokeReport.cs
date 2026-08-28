using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass43ClassicMajorMudPlusEventBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass43ClassicMajorMudPlusEventBoundarySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.Validate().Count == 0, "classic-plus-event-evidence-registry-valid");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.LegacyRuneItemNumber == 965, "native-rune-item-965");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.CanonicalRuneItemId == "item.965", "canonical-rune-item-965");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.PlacementRangeCount == 17, "native-seventeen-placement-ranges");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.TotalRuneQuantity == 40, "native-forty-runes");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.MaximumAttemptsPerRune == 50, "native-fifty-attempt-cap");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.ExcludedRoomAttributeMask == 0x10, "native-room-mask-0x10");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.VisibleRoomCapacity == 17, "native-visible-capacity-17");
        Check(RuntimeClassicMajorMudPlusEventEvidenceRegistry.HiddenRoomCapacity == 15, "native-hidden-capacity-15");
        Check(!RuntimeClassicMajorMudPlusEventEvidenceRegistry.ExactNativeRngCallsiteThreadDrawOrderingResolved, "exact-contest-rng-order-remains-unknown");
        Check(!RuntimeClassicMajorMudPlusEventEvidenceRegistry.AutomaticPrizeOrRankingMutationPromoted, "no-invented-prize-ranking");
        Check(!RuntimeClassicMajorMudPlusEventEvidenceRegistry.GenericNativeEventSchedulerPromoted, "no-generic-native-event-scheduler-claim");

        return new RuntimePass43ClassicMajorMudPlusEventBoundarySmokeReport(failures.Count == 0, checks, failures);
    }
}
