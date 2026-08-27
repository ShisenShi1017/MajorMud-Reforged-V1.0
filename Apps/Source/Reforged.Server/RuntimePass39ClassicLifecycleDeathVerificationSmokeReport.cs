using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass39ClassicLifecycleDeathVerificationSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass39ClassicLifecycleDeathVerificationSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicLifecycleDeathEvidenceRegistry.Validate().Count == 0, "lifecycle-death-evidence-registry-valid");
        Check(RuntimeClassicDeathLawPvpAuthority.GracefulExitDelaySeconds == 5, "graceful-exit-delay-remains-5s");
        Check(RuntimeClassicCharacterProgressionFormula.MaximumLives == 9, "classic-maximum-lives-remains-9");
        Check(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.HandleExplicitClassicSessionExit)) is not null,
            "explicit-classic-exit-party-hook-present");
        Check(typeof(RuntimeCommandDispatcher).GetMethod(nameof(RuntimeCommandDispatcher.RequestsSessionExit)) is not null,
            "session-exit-routing-present");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.PriorityQueueFormulaPromoted, "priority-queue-hypothesis-rejected");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.FixedThreeMinuteLinkdeadTimerPromoted, "fixed-linkdead-timer-not-promoted");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.PlayerJsonNativeSavePromoted, "player-json-native-save-not-promoted");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.DeathXpUnderLevelTenImmunityPromoted, "death-xp-level-gate-remains-gated");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.DeathXpTenPercentPromoted, "death-xp-ten-percent-remains-gated");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.DeathXpTwoMillionCapPromoted, "death-xp-cap-remains-gated");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.ExactLeaderExitSuccessionResolved, "leader-exit-succession-remains-gated");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.ExactLinkdeadGameplayResolved, "linkdead-gameplay-remains-gated");
        Check(!RuntimeClassicLifecycleDeathEvidenceRegistry.ExactCrossOwnerExitOrderingResolved, "exit-cleanup-order-remains-gated");

        // Existing PASS combat scheduling is the explicit contradiction to the user document's
        // ActionDelay priority queue. Keep those known vectors live as a regression anchor.
        Check(RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(0, 300).Swings == 3,
            "stock-attack-energy-scheduler-not-priority-queue");
        Check(RuntimeClassicPhysicalFormulaCalculator.ResolveNormalWeaponAttackSchedule(0, 100).Swings == 5,
            "stock-five-swing-cap-retained");

        return new RuntimePass39ClassicLifecycleDeathVerificationSmokeReport(failures.Count == 0, checks, failures);
    }
}
