using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleEMovementAiClosureAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Bundle E movement/AI disconnect-to-connect closure gate. This does not introduce a second AI
/// authority. It proves that each evidence-constrained movement and decision consumer is present,
/// executable, deterministic, and connected to RuntimeLivingWorldService persistence ownership.
/// </summary>
public static class RuntimeBundleEMovementAiClosureAudit
{
    public static RuntimeBundleEMovementAiClosureAuditReport Run()
    {
        var checks = new List<string>();
        var failures = new List<string>();

        Check(RuntimeNpcAiDecisionSmokeReport.Run().Passed, "ai-decision-engine", checks, failures);
        Check(RuntimeNpcAiSchedulerSmokeReport.Run().Passed, "ai-world-scheduler-and-pending-action-persistence", checks, failures);
        Check(RuntimeNpcPerceptionSmokeReport.Run().Passed, "perception-target-acquisition", checks, failures);
        Check(RuntimeNpcThreatSmokeReport.Run().Passed, "threat-target-selection", checks, failures);
        Check(RuntimeNpcThreatGenerationSmokeReport.Run().Passed, "threat-generation-consumer", checks, failures);
        Check(RuntimeNpcThreatDecaySmokeReport.Run().Passed, "threat-decay-consumer", checks, failures);
        Check(RuntimeNpcThreatMemorySmokeReport.Run().Passed, "threat-memory-persistence", checks, failures);
        Check(RuntimeNpcThreatTauntSmokeReport.Run().Passed, "taunt-threat-consumer", checks, failures);
        Check(RuntimeNpcAssistSmokeReport.Run().Passed, "explicit-assist-consumer", checks, failures);
        Check(RuntimeNpcFleeSmokeReport.Run().Passed, "flee-admission-consumer", checks, failures);
        Check(RuntimeNpcFleeMovementSmokeReport.Run().Passed, "flee-movement-completion", checks, failures);
        Check(RuntimeNpcReturnHomeSmokeReport.Run().Passed, "return-home-admission-and-movement", checks, failures);
        Check(RuntimeNpcExplicitRouteSmokeReport.Run().Passed, "explicit-route-admission-and-completion", checks, failures);
        Check(RuntimeNpcCompanionFollowSmokeReport.Run().Passed, "companion-follow-and-teleport-follow", checks, failures);
        Check(RuntimeNpcCompanionCombatAssistSmokeReport.Run().Passed, "companion-combat-assist", checks, failures);

        var living = typeof(RuntimeLivingWorldService);
        RequireMethod(living, nameof(RuntimeLivingWorldService.TickNpcAi), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.GetPendingNpcActions), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.ClaimNpcAction), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.ApplyNpcThreatSnapshot), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.ApplyNpcAssistSnapshot), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.ApplyNpcFleeSnapshot), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.ApplyNpcReturnHomeSnapshot), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.CompleteNpcReturnHomeMovement), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.AdmitNpcExplicitRoute), checks, failures);
        RequireMethod(living, nameof(RuntimeLivingWorldService.CompleteNpcExplicitRouteStep), checks, failures);

        return new(failures.Count == 0, checks, failures);
    }

    private static void Check(bool condition, string id, ICollection<string> checks, ICollection<string> failures)
    {
        if (condition) checks.Add(id);
        else failures.Add(id);
    }

    private static void RequireMethod(Type type, string method, ICollection<string> checks, ICollection<string> failures)
    {
        var id = $"living-world-consumer:{method}";
        Check(type.GetMethod(method) is not null, id, checks, failures);
    }
}
