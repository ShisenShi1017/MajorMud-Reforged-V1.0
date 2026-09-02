using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeWorldResetCleanupSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeWorldResetCleanupSmokeReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var plan = new RuntimeResetPlanDefinition("reforged-reset-coordination-smoke", RuntimeResetScope.Room, new[]
        {
            new RuntimeResetActionDefinition("doors", RuntimeResetActionKind.Door, "doors-locks-access", 10, true),
            new RuntimeResetActionDefinition("traps", RuntimeResetActionKind.Trap, "traps", 20, true),
            new RuntimeResetActionDefinition("containers", RuntimeResetActionKind.Container, "world-containers", 30, true),
            new RuntimeResetActionDefinition("hard-spawn-exception", RuntimeResetActionKind.Spawn, "npc-ai", 40, false, true),
            new RuntimeResetActionDefinition("orphan-cleanup", RuntimeResetActionKind.OrphanCleanup, "world-reset-cleanup", 90, false)
        }, RuntimeResetFailurePolicy.StopAndRollback);
        var service = new RuntimeWorldResetCleanupService(new[] { plan });
        var dispatches = service.Begin(new RuntimeResetRequest("reforged-reset-coordination-smoke", "smoke", DateTimeOffset.UnixEpoch, true));
        if (dispatches.Select(x => x.ActionId).SequenceEqual(new[] { "doors", "traps", "containers", "orphan-cleanup" })) checks.Add("reforged-plan-order-and-exclusion"); else failures.Add("dispatch-order-invalid");
        if (dispatches.All(x => x.OwnerSubsystem.Length > 0)) checks.Add("typed-owner-routing"); else failures.Add("owner-routing-invalid");
        var execution = service.Complete(dispatches[0].ExecutionId, new[]
        {
            new RuntimeResetActionResult("doors", true),
            new RuntimeResetActionResult("traps", false, "smoke-failure"),
            new RuntimeResetActionResult("containers", true),
            new RuntimeResetActionResult("orphan-cleanup", true)
        }, DateTimeOffset.UnixEpoch.AddSeconds(1));
        if (execution.Completed && execution.RolledBack) checks.Add("required-failure-rollback"); else failures.Add("rollback-policy-invalid");
        if (execution.FailedActionIds.Contains("traps")) checks.Add("failure-recording"); else failures.Add("failure-not-recorded");
        if (service.ValidateState().Count == 0) checks.Add("state-invariants"); else failures.AddRange(service.ValidateState());
        if (!RuntimeClassicWorldResetCleanupAuthority.GenericCoordinatorOwnsClassicOrdering) checks.Add("generic-coordinator-receives-no-classic-ordering-credit"); else failures.Add("generic-coordinator-misclassified");
        try
        {
            RuntimeClassicWorldResetCleanupAuthority.RequireExhaustedClassicResetBoundary();
            checks.Add("classic-reset-boundary-exhausted-as-owner-specific");
        }
        catch (InvalidOperationException ex)
        {
            failures.Add("classic-reset-boundary:" + ex.Message);
        }
        RecordCheck(!RuntimeClassicWorldResetCleanupAuthority.UniversalResetTransactionExists,
            "classic-no-universal-reset-transaction", "classic-universal-reset-transaction-invented", checks, failures);
        return new(failures.Count == 0, checks, failures);
    }

    private static void RecordCheck(bool condition, string check, string failure, List<string> checks, List<string> failures)
    {
        if (condition) checks.Add(check); else failures.Add(failure);
    }
}
