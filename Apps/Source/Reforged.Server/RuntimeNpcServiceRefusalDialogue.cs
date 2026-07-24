using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcServiceRefusalDialogueRequest(
    string NpcInstanceId,
    string ActorId,
    string ActorRoomId,
    string ServiceKind,
    string RefusalReason,
    string TextblockId,
    bool TextblockCanonical,
    DateTimeOffset RequestedAtUtc,
    string Authority);

public sealed record RuntimeNpcServiceRefusalDialogueResult(
    bool Admitted,
    string NpcInstanceId,
    string ServiceKind,
    string RefusalReason,
    string TextblockId,
    string? DialogueRequestId,
    string Reason);

/// <summary>
/// Exact service-refusal dialogue ownership boundary. Merchant/trainer policy owners supply an
/// already-resolved refusal reason and canonical textblock identity. This service performs no
/// eligibility, pricing, cooldown, shop-hour, or textblock-selection inference; it only admits
/// the exact same-room dialogue through the established NPC dialogue owner.
/// </summary>
public sealed class RuntimeNpcServiceRefusalDialogueService
{
    private readonly RuntimeLivingWorldService _living;

    public RuntimeNpcServiceRefusalDialogueService(RuntimeLivingWorldService living) => _living = living;

    public RuntimeNpcServiceRefusalDialogueResult AdmitExact(RuntimeNpcServiceRefusalDialogueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Authority)) return Reject(request, "authority-required");
        if (string.IsNullOrWhiteSpace(request.ActorId)) return Reject(request, "actor-id-required");
        if (string.IsNullOrWhiteSpace(request.RefusalReason)) return Reject(request, "refusal-reason-required");
        if (!IsSupportedService(request.ServiceKind)) return Reject(request, "unsupported-service-kind");
        if (string.IsNullOrWhiteSpace(request.TextblockId)) return Reject(request, "textblock-id-required");
        if (!request.TextblockCanonical) return Reject(request, "textblock-not-canonical");

        var admitted = _living.AdmitNpcDialogue(
            request.NpcInstanceId,
            request.ActorId,
            request.ActorRoomId,
            request.Authority,
            request.RequestedAtUtc);

        if (!admitted.Admitted)
            return new(false, request.NpcInstanceId, Normalize(request.ServiceKind), request.RefusalReason.Trim(), request.TextblockId.Trim(), admitted.RequestId, admitted.Reason);

        var npc = _living.InspectNpc(request.NpcInstanceId)!;
        var metadata = new Dictionary<string, string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["npc_service_refusal_kind"] = Normalize(request.ServiceKind),
            ["npc_service_refusal_reason"] = request.RefusalReason.Trim(),
            ["npc_service_refusal_textblock_id"] = request.TextblockId.Trim(),
            ["npc_service_refusal_authority"] = request.Authority.Trim(),
            ["npc_service_refusal_requested_at_utc"] = request.RequestedAtUtc.ToString("O")
        };
        _living.ReplaceNpcMetadata(request.NpcInstanceId, metadata);
        return new(true, request.NpcInstanceId, Normalize(request.ServiceKind), request.RefusalReason.Trim(), request.TextblockId.Trim(), admitted.RequestId, "service-refusal-dialogue-admitted");
    }

    public bool CompleteExact(string npcInstanceId, string dialogueRequestId, bool rendered, string? outcome, DateTimeOffset completedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(dialogueRequestId)) return false;
        var completed = _living.CompleteNpcDialogue(npcInstanceId, dialogueRequestId, rendered, outcome, completedAtUtc);
        if (!completed) return false;
        var npc = _living.InspectNpc(npcInstanceId);
        if (npc is null) return false;
        var metadata = new Dictionary<string, string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["npc_service_refusal_last_rendered"] = rendered ? "true" : "false",
            ["npc_service_refusal_completed_at_utc"] = completedAtUtc.ToString("O")
        };
        foreach (var key in new[] { "npc_service_refusal_kind", "npc_service_refusal_reason", "npc_service_refusal_textblock_id", "npc_service_refusal_authority", "npc_service_refusal_requested_at_utc" })
            metadata.Remove(key);
        return _living.ReplaceNpcMetadata(npcInstanceId, metadata);
    }

    private static bool IsSupportedService(string value) =>
        value.Equals("buy", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("sell", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("repair", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("train", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static RuntimeNpcServiceRefusalDialogueResult Reject(RuntimeNpcServiceRefusalDialogueRequest request, string reason) =>
        new(false, request.NpcInstanceId ?? string.Empty, request.ServiceKind ?? string.Empty, request.RefusalReason ?? string.Empty, request.TextblockId ?? string.Empty, null, reason);
}

public sealed class RuntimeNpcServiceRefusalDialogueSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcServiceRefusalDialogueSmokeReport Run()
    {
        var report = new RuntimeNpcServiceRefusalDialogueSmokeReport();
        try
        {
            var type = typeof(RuntimeNpcServiceRefusalDialogueService);
            Require(type.GetMethod(nameof(RuntimeNpcServiceRefusalDialogueService.AdmitExact)) is not null, "service-refusal-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeNpcServiceRefusalDialogueService.CompleteExact)) is not null, "service-refusal-completion-exposed");
            var living = typeof(RuntimeLivingWorldService);
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcDialogue)) is not null, "dialogue-owner-reused");
            Require(living.GetMethod(nameof(RuntimeLivingWorldService.ReplaceNpcMetadata)) is not null, "metadata-replacement-exposed");
            report.Checks.AddRange(["service-refusal-admission-exposed", "service-refusal-completion-exposed", "dialogue-owner-reused", "metadata-replacement-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
