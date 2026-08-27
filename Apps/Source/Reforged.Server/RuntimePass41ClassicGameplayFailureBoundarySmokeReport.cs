using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimePass41ClassicGameplayFailureBoundarySmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimePass41ClassicGameplayFailureBoundarySmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { checks.Add(name); if (!value) failures.Add(name); }

        Check(RuntimeClassicGameplayFailureEvidenceRegistry.Validate().Count == 0, "classic-gameplay-failure-evidence-registry-valid");

        var service = new RuntimeErrorRecoveryService();
        var rejected = service.Apply(new RuntimeFailureContext(
            RuntimeFailureKind.GameplayActionRejected,
            "ClassicGameplay",
            "representative-rejected-action",
            StateMutationStarted: false,
            CharacterScoped: true,
            WorldLoadCritical: false,
            ValidatedDefaultAvailable: false,
            RollbackAvailable: true));

        Check(rejected.Accepted, "rejected-action-decision-accepted");
        Check(rejected.Code == RuntimeClassicGameplayFailureEvidenceRegistry.RejectedGameplayDecisionCode, "rejected-action-noop-code");
        Check(rejected.Action == RuntimeRecoveryAction.NoOp, "rejected-action-is-noop");
        Check(!rejected.RequiresRollback, "rejected-action-does-not-rollback");
        Check(!rejected.DisableSubsystem, "rejected-action-does-not-disable-subsystem");
        Check(!rejected.StopWorldLoad, "rejected-action-does-not-stop-world-load");
        Check(!service.IsSubsystemDisabled("ClassicGameplay"), "rejected-action-preserves-recovery-service-state");

        var hardLoad = service.Decide(new RuntimeFailureContext(
            RuntimeFailureKind.InvalidRecord,
            "ClassicNativeData",
            "startup",
            WorldLoadCritical: true));
        Check(hardLoad.StopWorldLoad && hardLoad.Action == RuntimeRecoveryAction.StopWorldLoad, "catastrophic-native-load-boundary-is-not-gameplay-noop");

        Check(RuntimeClassicGameplayFailureEvidenceRegistry.Rows.Any(row => row.Id == "gameplay.command-failure-wording" && row.State == RuntimeClassicGameplayFailureEvidenceState.Unknown), "exact-failure-wording-remains-unknown");
        Check(RuntimeClassicGameplayFailureEvidenceRegistry.Rows.Any(row => row.Id == "persistence.catastrophic-save-rollback-order" && row.State == RuntimeClassicGameplayFailureEvidenceState.Unknown), "catastrophic-save-order-remains-unknown");

        return new RuntimePass41ClassicGameplayFailureBoundarySmokeReport(failures.Count == 0, checks, failures);
    }
}
