using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcTrainerOfferingAdmissionRequest(
    string NpcInstanceId,
    string TrainerId,
    string OfferingId,
    string ActorId,
    string ActorRoomId,
    DateTimeOffset RequestedAtUtc,
    string Authority);

public sealed record RuntimeNpcTrainerOfferingAdmissionResult(
    bool Admitted,
    string NpcInstanceId,
    string TrainerId,
    string OfferingId,
    string? RequestId,
    DateTimeOffset NextAvailableUtc,
    string Reason);

public sealed record RuntimeNpcTrainerOfferingCompletionRequest(
    string NpcInstanceId,
    string TrainerId,
    string OfferingId,
    string RequestId,
    bool Succeeded,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset NextAvailableUtc,
    string Authority,
    string? Outcome);

public sealed record RuntimeNpcTrainerOfferingCompletionResult(
    bool Completed,
    string NpcInstanceId,
    string TrainerId,
    string OfferingId,
    string RequestId,
    string Reason);

/// <summary>
/// Exact availability and cooldown-commit bridge for a persistent trainer offering.
/// Eligibility, cost, progression, cooldown duration, and refusal text remain owned by the
/// caller. This service only admits an already-defined enabled offering when the supplied
/// request time reaches its persisted availability, and commits the exact supplied next
/// availability after an exact successful completion.
/// </summary>
public sealed class RuntimeNpcTrainerOfferingAdmissionService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeGameplayCompletionStateStore _store;

    public RuntimeNpcTrainerOfferingAdmissionService(RuntimeLivingWorldService living, RuntimeGameplayCompletionStateStore store)
    {
        _living = living;
        _store = store;
    }

    public RuntimeNpcTrainerOfferingAdmissionResult AdmitExact(RuntimeNpcTrainerOfferingAdmissionRequest request, RuntimeServiceDefinition trainer)
    {
        if (string.IsNullOrWhiteSpace(request.Authority)) return RejectAdmission(request, DateTimeOffset.MinValue, "authority-required");
        if (!trainer.Id.Equals(request.TrainerId, StringComparison.OrdinalIgnoreCase)) return RejectAdmission(request, DateTimeOffset.MinValue, "trainer-id-mismatch");
        if (string.IsNullOrWhiteSpace(request.OfferingId)) return RejectAdmission(request, DateTimeOffset.MinValue, "offering-id-required");

        var state = _store.ReadTrainer(trainer);
        if (!state.Offerings.TryGetValue(request.OfferingId, out var offering))
            return RejectAdmission(request, DateTimeOffset.MinValue, "offering-not-found");
        if (!offering.Enabled)
            return RejectAdmission(request, offering.NextAvailableUtc, "offering-disabled");
        if (request.RequestedAtUtc < offering.NextAvailableUtc)
            return RejectAdmission(request, offering.NextAvailableUtc, "offering-cooldown-active");

        var admission = _living.AdmitNpcService(
            request.NpcInstanceId,
            request.ActorId,
            request.ActorRoomId,
            "train",
            request.Authority,
            request.RequestedAtUtc);

        if (!admission.Admitted)
            return new(false, request.NpcInstanceId, request.TrainerId, request.OfferingId, admission.RequestId, offering.NextAvailableUtc, admission.Reason);

        return new(true, admission.InstanceId, trainer.Id, offering.OfferingId, admission.RequestId, offering.NextAvailableUtc, "offering-admitted");
    }

    public RuntimeNpcTrainerOfferingCompletionResult CompleteExact(RuntimeNpcTrainerOfferingCompletionRequest request, RuntimeServiceDefinition trainer)
    {
        if (string.IsNullOrWhiteSpace(request.Authority)) return RejectCompletion(request, "authority-required");
        if (!trainer.Id.Equals(request.TrainerId, StringComparison.OrdinalIgnoreCase)) return RejectCompletion(request, "trainer-id-mismatch");
        if (string.IsNullOrWhiteSpace(request.RequestId)) return RejectCompletion(request, "request-id-required");

        var npc = _living.InspectNpc(request.NpcInstanceId);
        if (npc is null) return RejectCompletion(request, "instance-not-found");
        if (!npc.Metadata.TryGetValue("npc_service_active_request_id", out var active) || !active.Equals(request.RequestId, StringComparison.Ordinal))
            return RejectCompletion(request, "active-request-mismatch");
        if (!npc.Metadata.TryGetValue("npc_service_kind", out var kind) || !kind.Equals("train", StringComparison.OrdinalIgnoreCase))
            return RejectCompletion(request, "active-service-kind-mismatch");

        var state = _store.ReadTrainer(trainer);
        if (!state.Offerings.TryGetValue(request.OfferingId, out var offering)) return RejectCompletion(request, "offering-not-found");

        if (request.Succeeded)
        {
            if (request.NextAvailableUtc < request.CompletedAtUtc) return RejectCompletion(request, "next-availability-precedes-completion");
            _store.ExecuteTrainerTransaction(trainer, offerings =>
            {
                offerings[request.OfferingId] = offering with
                {
                    NextAvailableUtc = request.NextAvailableUtc,
                    Authority = request.Authority.Trim()
                };
                return 0;
            });
        }

        var completed = _living.CompleteNpcService(
            request.NpcInstanceId,
            request.RequestId,
            request.Succeeded,
            request.Outcome,
            request.CompletedAtUtc);

        return completed
            ? new(true, request.NpcInstanceId, trainer.Id, request.OfferingId, request.RequestId, request.Succeeded ? "offering-completed-and-cooldown-committed" : "offering-completed-without-cooldown-change")
            : RejectCompletion(request, "service-completion-rejected");
    }

    private static RuntimeNpcTrainerOfferingAdmissionResult RejectAdmission(RuntimeNpcTrainerOfferingAdmissionRequest r, DateTimeOffset next, string reason) =>
        new(false, r.NpcInstanceId ?? string.Empty, r.TrainerId ?? string.Empty, r.OfferingId ?? string.Empty, null, next, reason);

    private static RuntimeNpcTrainerOfferingCompletionResult RejectCompletion(RuntimeNpcTrainerOfferingCompletionRequest r, string reason) =>
        new(false, r.NpcInstanceId ?? string.Empty, r.TrainerId ?? string.Empty, r.OfferingId ?? string.Empty, r.RequestId ?? string.Empty, reason);
}

public sealed class RuntimeNpcTrainerOfferingAdmissionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcTrainerOfferingAdmissionSmokeReport Run()
    {
        var report = new RuntimeNpcTrainerOfferingAdmissionSmokeReport();
        try
        {
            var type = typeof(RuntimeNpcTrainerOfferingAdmissionService);
            Require(type.GetMethod(nameof(RuntimeNpcTrainerOfferingAdmissionService.AdmitExact)) is not null, "trainer-offering-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeNpcTrainerOfferingAdmissionService.CompleteExact)) is not null, "trainer-offering-completion-exposed");
            var living = typeof(RuntimeLivingWorldService);
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcService)) is not null, "trainer-service-admission-integrated");
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcService)) is not null, "trainer-service-completion-integrated");
            report.Checks.AddRange([
                "trainer-offering-admission-exposed",
                "trainer-offering-completion-exposed",
                "trainer-service-admission-integrated",
                "trainer-service-completion-integrated"
            ]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
