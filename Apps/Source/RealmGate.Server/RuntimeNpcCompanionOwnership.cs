using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcCompanionOwnershipRequest(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string RelationshipKind,
    string Authority,
    DateTimeOffset AssignedAtUtc);

public sealed record RuntimeNpcCompanionOwnershipResult(
    bool Applied,
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string RelationshipKind,
    string Reason);

public sealed record RuntimeNpcCompanionOwnershipView(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string RelationshipKind,
    string Authority,
    DateTimeOffset AssignedAtUtc);

/// <summary>
/// Exact ownership boundary for already-created summons, pets, followers, hirelings, and escorts.
/// The caller owns relationship selection and duration policy. This service does not infer native charm,
/// command, follow, teleport, logout, persistence-duration, or combat-assistance semantics.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    private static readonly HashSet<string> SupportedCompanionRelationships = new(StringComparer.OrdinalIgnoreCase)
    {
        "summon", "pet", "follower", "hireling", "escort", "charmed"
    };

    private static readonly HashSet<string> SupportedCompanionOwnerKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "character", "party", "quest", "script", "spell", "event"
    };

    public RuntimeNpcCompanionOwnershipResult AssignNpcCompanionOwnership(RuntimeNpcCompanionOwnershipRequest request)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(request.NpcInstanceId ?? string.Empty, out var current))
                return RejectCompanion(request, "instance-not-found");
            if (!current.Alive)
                return RejectCompanion(request, "npc-is-not-alive");
            if (string.IsNullOrWhiteSpace(request.Authority))
                return RejectCompanion(request, "authority-required");
            if (!SupportedCompanionOwnerKinds.Contains(request.OwnerKind ?? string.Empty))
                return RejectCompanion(request, "owner-kind-not-supported");
            if (string.IsNullOrWhiteSpace(request.OwnerId))
                return RejectCompanion(request, "owner-id-required");
            if (!SupportedCompanionRelationships.Contains(request.RelationshipKind ?? string.Empty))
                return RejectCompanion(request, "relationship-kind-not-supported");
            if (current.Metadata.TryGetValue("npc_companion_owner_id", out var existingOwner) && !string.IsNullOrWhiteSpace(existingOwner))
                return RejectCompanion(request, "companion-owner-already-assigned");

            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_owner_kind"] = request.OwnerKind!.Trim().ToLowerInvariant(),
                ["npc_companion_owner_id"] = request.OwnerId!.Trim(),
                ["npc_companion_relationship_kind"] = request.RelationshipKind!.Trim().ToLowerInvariant(),
                ["npc_companion_authority"] = request.Authority!.Trim(),
                ["npc_companion_assigned_at_utc"] = request.AssignedAtUtc.ToString("O")
            };
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, metadata["npc_companion_owner_kind"], metadata["npc_companion_owner_id"], metadata["npc_companion_relationship_kind"], "companion-ownership-assigned");
        }
    }

    public RuntimeNpcCompanionOwnershipResult ReleaseNpcCompanionOwnership(string instanceId, string ownerKind, string ownerId, string authority, DateTimeOffset releasedAtUtc)
    {
        lock (_gate)
        {
            var request = new RuntimeNpcCompanionOwnershipRequest(instanceId, ownerKind, ownerId, string.Empty, authority, releasedAtUtc);
            if (!_npcs.TryGetValue(instanceId ?? string.Empty, out var current))
                return RejectCompanion(request, "instance-not-found");
            if (string.IsNullOrWhiteSpace(authority))
                return RejectCompanion(request, "authority-required");
            if (!current.Metadata.TryGetValue("npc_companion_owner_kind", out var storedKind) ||
                !current.Metadata.TryGetValue("npc_companion_owner_id", out var storedId))
                return RejectCompanion(request, "companion-owner-not-assigned");
            if (!storedKind.Equals(ownerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                !storedId.Equals(ownerId ?? string.Empty, StringComparison.Ordinal))
                return RejectCompanion(request, "companion-owner-mismatch");

            var relationship = current.Metadata.TryGetValue("npc_companion_relationship_kind", out var storedRelationship) ? storedRelationship : string.Empty;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_last_owner_kind"] = storedKind,
                ["npc_companion_last_owner_id"] = storedId,
                ["npc_companion_last_relationship_kind"] = relationship,
                ["npc_companion_released_by"] = authority.Trim(),
                ["npc_companion_released_at_utc"] = releasedAtUtc.ToString("O")
            };
            foreach (var key in new[] { "npc_companion_owner_kind", "npc_companion_owner_id", "npc_companion_relationship_kind", "npc_companion_authority", "npc_companion_assigned_at_utc" }) metadata.Remove(key);
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, storedKind, storedId, relationship, "companion-ownership-released");
        }
    }

    public RuntimeNpcCompanionOwnershipView? InspectNpcCompanionOwnership(string instanceId)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId ?? string.Empty, out var current)) return null;
            if (!current.Metadata.TryGetValue("npc_companion_owner_kind", out var ownerKind) ||
                !current.Metadata.TryGetValue("npc_companion_owner_id", out var ownerId) ||
                !current.Metadata.TryGetValue("npc_companion_relationship_kind", out var relationship) ||
                !current.Metadata.TryGetValue("npc_companion_authority", out var authority) ||
                !current.Metadata.TryGetValue("npc_companion_assigned_at_utc", out var assignedRaw) ||
                !DateTimeOffset.TryParse(assignedRaw, out var assignedAt)) return null;
            return new(current.InstanceId, ownerKind, ownerId, relationship, authority, assignedAt);
        }
    }

    private static RuntimeNpcCompanionOwnershipResult RejectCompanion(RuntimeNpcCompanionOwnershipRequest request, string reason) =>
        new(false, request.NpcInstanceId ?? string.Empty, request.OwnerKind ?? string.Empty, request.OwnerId ?? string.Empty, request.RelationshipKind ?? string.Empty, reason);
}

public sealed class RuntimeNpcCompanionOwnershipSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCompanionOwnershipSmokeReport Run()
    {
        var report = new RuntimeNpcCompanionOwnershipSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.AssignNpcCompanionOwnership)) is not null, "companion-assign-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.ReleaseNpcCompanionOwnership)) is not null, "companion-release-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.InspectNpcCompanionOwnership)) is not null, "companion-inspection-exposed");
            report.Checks.AddRange(["companion-assign-exposed", "companion-release-exposed", "companion-inspection-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
