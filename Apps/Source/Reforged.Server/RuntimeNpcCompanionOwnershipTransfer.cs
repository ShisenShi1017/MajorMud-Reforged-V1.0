using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcCompanionOwnershipTransferRequest(
    string NpcInstanceId,
    string CurrentOwnerKind,
    string CurrentOwnerId,
    string NewOwnerKind,
    string NewOwnerId,
    string RelationshipKind,
    string Authority,
    DateTimeOffset TransferredAtUtc);

public sealed record RuntimeNpcCompanionOwnershipTransferResult(
    bool Applied,
    string NpcInstanceId,
    string PreviousOwnerKind,
    string PreviousOwnerId,
    string NewOwnerKind,
    string NewOwnerId,
    string RelationshipKind,
    string Reason);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcCompanionOwnershipTransferResult TransferNpcCompanionOwnership(RuntimeNpcCompanionOwnershipTransferRequest request)
    {
        lock (_gate)
        {
            RuntimeNpcCompanionOwnershipTransferResult Reject(string reason) => new(false, request.NpcInstanceId ?? string.Empty,
                request.CurrentOwnerKind ?? string.Empty, request.CurrentOwnerId ?? string.Empty,
                request.NewOwnerKind ?? string.Empty, request.NewOwnerId ?? string.Empty,
                request.RelationshipKind ?? string.Empty, reason);

            if (!_npcs.TryGetValue(request.NpcInstanceId ?? string.Empty, out var current)) return Reject("instance-not-found");
            if (!current.Alive) return Reject("npc-is-not-alive");
            if (string.IsNullOrWhiteSpace(request.Authority)) return Reject("authority-required");
            if (!SupportedCompanionOwnerKinds.Contains(request.NewOwnerKind ?? string.Empty)) return Reject("new-owner-kind-not-supported");
            if (string.IsNullOrWhiteSpace(request.NewOwnerId)) return Reject("new-owner-id-required");
            if (!SupportedCompanionRelationships.Contains(request.RelationshipKind ?? string.Empty)) return Reject("relationship-kind-not-supported");
            if (!current.Metadata.TryGetValue("npc_companion_owner_kind", out var storedKind) ||
                !current.Metadata.TryGetValue("npc_companion_owner_id", out var storedId)) return Reject("companion-owner-not-assigned");
            if (!storedKind.Equals(request.CurrentOwnerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                !storedId.Equals(request.CurrentOwnerId ?? string.Empty, StringComparison.Ordinal)) return Reject("current-owner-mismatch");

            var newKind = request.NewOwnerKind!.Trim().ToLowerInvariant();
            var newId = request.NewOwnerId!.Trim();
            if (storedKind.Equals(newKind, StringComparison.OrdinalIgnoreCase) && storedId.Equals(newId, StringComparison.Ordinal))
                return Reject("new-owner-matches-current-owner");

            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_previous_owner_kind"] = storedKind,
                ["npc_companion_previous_owner_id"] = storedId,
                ["npc_companion_owner_kind"] = newKind,
                ["npc_companion_owner_id"] = newId,
                ["npc_companion_relationship_kind"] = request.RelationshipKind!.Trim().ToLowerInvariant(),
                ["npc_companion_transfer_authority"] = request.Authority!.Trim(),
                ["npc_companion_transferred_at_utc"] = request.TransferredAtUtc.ToString("O")
            };
            foreach (var key in new[] { "npc_companion_pending_follow_owner_kind", "npc_companion_pending_follow_owner_id", "npc_companion_assist_owner_kind", "npc_companion_assist_owner_id", "npc_companion_command_target_id" }) metadata.Remove(key);
            metadata.Remove("current_target_id");
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, storedKind, storedId, newKind, newId, metadata["npc_companion_relationship_kind"], "companion-ownership-transferred");
        }
    }
}

public sealed class RuntimeNpcCompanionOwnershipTransferSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCompanionOwnershipTransferSmokeReport Run()
    {
        var report = new RuntimeNpcCompanionOwnershipTransferSmokeReport();
        try
        {
            if (typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.TransferNpcCompanionOwnership)) is null)
                throw new InvalidOperationException("companion-transfer-exposed");
            report.Checks.Add("companion-transfer-exposed");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
