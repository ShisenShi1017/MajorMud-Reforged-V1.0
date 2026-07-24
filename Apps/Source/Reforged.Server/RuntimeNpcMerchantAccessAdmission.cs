using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcMerchantAccessAdmissionRequest(
    string NpcInstanceId,
    string ShopId,
    string ActorId,
    string ActorRoomId,
    string ServiceKind,
    bool ShopOpen,
    bool ActorEligible,
    bool CurrencyAuthorized,
    DateTimeOffset RequestedAtUtc,
    string Authority);

public sealed record RuntimeNpcMerchantAccessAdmissionResult(
    bool Admitted,
    string NpcInstanceId,
    string ShopId,
    string ServiceKind,
    string? RequestId,
    string Reason);

public sealed record RuntimeNpcMerchantAccessCompletionRequest(
    string NpcInstanceId,
    string ShopId,
    string RequestId,
    bool Succeeded,
    DateTimeOffset CompletedAtUtc,
    string Authority,
    string? Outcome);

public sealed record RuntimeNpcMerchantAccessCompletionResult(
    bool Completed,
    string NpcInstanceId,
    string ShopId,
    string RequestId,
    string Reason);

/// <summary>
/// Exact merchant access bridge. The owning host policy supplies already-resolved shop-hours,
/// actor-restriction, and currency authorization decisions. This service never derives those
/// policies; it only admits an exact living same-room merchant request and completes that exact
/// request through the existing NPC service ownership boundary.
/// </summary>
public sealed class RuntimeNpcMerchantAccessAdmissionService
{
    private readonly RuntimeLivingWorldService _living;

    public RuntimeNpcMerchantAccessAdmissionService(RuntimeLivingWorldService living) => _living = living;

    public RuntimeNpcMerchantAccessAdmissionResult AdmitExact(RuntimeNpcMerchantAccessAdmissionRequest request, RuntimeServiceDefinition shop)
    {
        if (string.IsNullOrWhiteSpace(request.Authority)) return RejectAdmission(request, "authority-required");
        if (!shop.Id.Equals(request.ShopId, StringComparison.OrdinalIgnoreCase)) return RejectAdmission(request, "shop-id-mismatch");
        if (!IsMerchantService(shop)) return RejectAdmission(request, "service-is-not-a-shop");
        if (!IsSupportedKind(request.ServiceKind)) return RejectAdmission(request, "unsupported-service-kind");
        if (!request.ShopOpen) return RejectAdmission(request, "shop-closed");
        if (!request.ActorEligible) return RejectAdmission(request, "actor-ineligible");
        if (!request.CurrencyAuthorized) return RejectAdmission(request, "currency-not-authorized");

        var admission = _living.AdmitNpcService(
            request.NpcInstanceId,
            request.ActorId,
            request.ActorRoomId,
            request.ServiceKind,
            request.Authority,
            request.RequestedAtUtc);

        return admission.Admitted
            ? new(true, admission.InstanceId, shop.Id, request.ServiceKind.Trim().ToLowerInvariant(), admission.RequestId, "merchant-access-admitted")
            : new(false, request.NpcInstanceId, shop.Id, request.ServiceKind, admission.RequestId, admission.Reason);
    }

    public RuntimeNpcMerchantAccessCompletionResult CompleteExact(RuntimeNpcMerchantAccessCompletionRequest request, RuntimeServiceDefinition shop)
    {
        if (string.IsNullOrWhiteSpace(request.Authority)) return RejectCompletion(request, "authority-required");
        if (!shop.Id.Equals(request.ShopId, StringComparison.OrdinalIgnoreCase)) return RejectCompletion(request, "shop-id-mismatch");
        if (string.IsNullOrWhiteSpace(request.RequestId)) return RejectCompletion(request, "request-id-required");

        var npc = _living.InspectNpc(request.NpcInstanceId);
        if (npc is null) return RejectCompletion(request, "instance-not-found");
        if (!npc.Metadata.TryGetValue("npc_service_active_request_id", out var active) || !active.Equals(request.RequestId, StringComparison.Ordinal))
            return RejectCompletion(request, "active-request-mismatch");
        if (!npc.Metadata.TryGetValue("npc_service_kind", out var kind) || !IsSupportedKind(kind))
            return RejectCompletion(request, "active-service-kind-mismatch");

        var completed = _living.CompleteNpcService(
            request.NpcInstanceId,
            request.RequestId,
            request.Succeeded,
            request.Outcome,
            request.CompletedAtUtc);

        return completed
            ? new(true, request.NpcInstanceId, shop.Id, request.RequestId, request.Succeeded ? "merchant-access-completed" : "merchant-access-failed")
            : RejectCompletion(request, "service-completion-rejected");
    }

    private static bool IsMerchantService(RuntimeServiceDefinition shop) =>
        shop.ServiceKind.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
        shop.ServiceKind.Contains("merchant", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedKind(string value) =>
        value.Equals("buy", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("sell", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("repair", StringComparison.OrdinalIgnoreCase);

    private static RuntimeNpcMerchantAccessAdmissionResult RejectAdmission(RuntimeNpcMerchantAccessAdmissionRequest r, string reason) =>
        new(false, r.NpcInstanceId ?? string.Empty, r.ShopId ?? string.Empty, r.ServiceKind ?? string.Empty, null, reason);

    private static RuntimeNpcMerchantAccessCompletionResult RejectCompletion(RuntimeNpcMerchantAccessCompletionRequest r, string reason) =>
        new(false, r.NpcInstanceId ?? string.Empty, r.ShopId ?? string.Empty, r.RequestId ?? string.Empty, reason);
}

public sealed class RuntimeNpcMerchantAccessAdmissionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcMerchantAccessAdmissionSmokeReport Run()
    {
        var report = new RuntimeNpcMerchantAccessAdmissionSmokeReport();
        try
        {
            var type = typeof(RuntimeNpcMerchantAccessAdmissionService);
            Require(type.GetMethod(nameof(RuntimeNpcMerchantAccessAdmissionService.AdmitExact)) is not null, "merchant-access-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeNpcMerchantAccessAdmissionService.CompleteExact)) is not null, "merchant-access-completion-exposed");
            var living = typeof(RuntimeLivingWorldService);
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcService)) is not null, "merchant-service-admission-integrated");
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcService)) is not null, "merchant-service-completion-integrated");
            report.Checks.AddRange(["merchant-access-admission-exposed", "merchant-access-completion-exposed", "merchant-service-admission-integrated", "merchant-service-completion-integrated"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
