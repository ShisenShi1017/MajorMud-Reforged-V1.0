using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcCompanionFollowRequest(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string LeaderRoomId,
    string DestinationRoomId,
    bool DestinationReachable,
    bool TeleportFollow,
    string Authority,
    long WorldTick);

public sealed record RuntimeNpcCompanionFollowResult(
    bool Accepted,
    string NpcInstanceId,
    string? DestinationRoomId,
    bool TeleportFollow,
    IReadOnlyList<string> Rejections,
    string Reason);

public sealed record RuntimeNpcCompanionFollowCompletion(
    string NpcInstanceId,
    string OwnerKind,
    string OwnerId,
    string FromRoomId,
    string DestinationRoomId,
    bool TraversalCompleted,
    string Authority,
    long WorldTick);

/// <summary>
/// Exact follow admission and completion boundary for an already-owned companion NPC. The caller owns
/// leader movement, path selection, teleport policy, restriction checks, costs, and timing. This service
/// only commits an exact destination supplied by that owning subsystem.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcCompanionFollowResult AdmitNpcCompanionFollow(RuntimeNpcCompanionFollowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var rejections = ValidateCompanionFollowOwnership(request.NpcInstanceId, request.OwnerKind, request.OwnerId, request.Authority, out var npc);
            if (npc is not null)
            {
                if (!npc.Alive) rejections.Add("npc-is-not-alive");
                if (npc.RoomId.Equals(request.DestinationRoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("npc-already-at-destination");
                if (string.IsNullOrWhiteSpace(request.LeaderRoomId)) rejections.Add("leader-room-required");
                if (string.IsNullOrWhiteSpace(request.DestinationRoomId)) rejections.Add("destination-room-required");
                if (!request.LeaderRoomId.Equals(request.DestinationRoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("destination-does-not-match-leader-room");
                if (!request.DestinationReachable) rejections.Add("destination-not-explicitly-reachable");
                if (_world.FindRoomById(request.DestinationRoomId!) is null) rejections.Add("destination-room-not-generated");
                if (request.WorldTick < 0) rejections.Add("world-tick-invalid");
            }

            if (npc is null || rejections.Count != 0)
                return new(false, request.NpcInstanceId ?? string.Empty, null, request.TeleportFollow, rejections, "companion-follow-rejected");

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_follow_pending"] = "true",
                ["npc_companion_follow_destination_room_id"] = request.DestinationRoomId!,
                ["npc_companion_follow_leader_room_id"] = request.LeaderRoomId!,
                ["npc_companion_follow_teleport"] = request.TeleportFollow ? "true" : "false",
                ["npc_companion_follow_authority"] = request.Authority!.Trim(),
                ["npc_companion_follow_admitted_tick"] = request.WorldTick.ToString()
            };
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, request.DestinationRoomId, request.TeleportFollow, Array.Empty<string>(), "companion-follow-admitted");
        }
    }

    public RuntimeNpcCompanionFollowResult CompleteNpcCompanionFollow(RuntimeNpcCompanionFollowCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        lock (_gate)
        {
            var rejections = ValidateCompanionFollowOwnership(completion.NpcInstanceId, completion.OwnerKind, completion.OwnerId, completion.Authority, out var npc);
            var teleport = false;
            if (npc is not null)
            {
                if (!npc.Alive) rejections.Add("npc-is-not-alive");
                if (!npc.RoomId.Equals(completion.FromRoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("source-room-mismatch");
                if (!npc.Metadata.TryGetValue("npc_companion_follow_pending", out var pending) || !pending.Equals("true", StringComparison.OrdinalIgnoreCase)) rejections.Add("companion-follow-not-admitted");
                if (!npc.Metadata.TryGetValue("npc_companion_follow_destination_room_id", out var admitted) || !admitted.Equals(completion.DestinationRoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("destination-does-not-match-admitted-follow");
                if (!completion.TraversalCompleted) rejections.Add("traversal-not-explicitly-completed");
                if (_world.FindRoomById(completion.DestinationRoomId!) is null) rejections.Add("destination-room-not-generated");
                if (completion.WorldTick < 0) rejections.Add("world-tick-invalid");
                teleport = npc.Metadata.TryGetValue("npc_companion_follow_teleport", out var teleportRaw) && teleportRaw.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (npc is null || rejections.Count != 0)
                return new(false, completion.NpcInstanceId ?? string.Empty, null, teleport, rejections, "companion-follow-completion-rejected");

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_companion_last_follow_origin_room_id"] = completion.FromRoomId!,
                ["npc_companion_last_follow_destination_room_id"] = completion.DestinationRoomId!,
                ["npc_companion_last_follow_authority"] = completion.Authority!.Trim(),
                ["npc_companion_last_follow_completed_tick"] = completion.WorldTick.ToString(),
                ["npc_companion_last_follow_teleport"] = teleport ? "true" : "false"
            };
            foreach (var key in new[] { "npc_companion_follow_pending", "npc_companion_follow_destination_room_id", "npc_companion_follow_leader_room_id", "npc_companion_follow_teleport", "npc_companion_follow_authority", "npc_companion_follow_admitted_tick" }) metadata.Remove(key);
            _npcs[npc.InstanceId] = npc with { RoomId = completion.DestinationRoomId!, Metadata = metadata };
            Save();
            return new(true, npc.InstanceId, completion.DestinationRoomId, teleport, Array.Empty<string>(), "companion-follow-completed");
        }
    }

    private List<string> ValidateCompanionFollowOwnership(string instanceId, string ownerKind, string ownerId, string authority, out RuntimeLivingNpcState? npc)
    {
        var rejections = new List<string>();
        if (!_npcs.TryGetValue(instanceId ?? string.Empty, out var current))
        {
            npc = null;
            rejections.Add("instance-not-found");
            return rejections;
        }
        npc = current;
        if (string.IsNullOrWhiteSpace(authority)) rejections.Add("authority-required");
        if (!current.Metadata.TryGetValue("npc_companion_owner_kind", out var storedKind) || !current.Metadata.TryGetValue("npc_companion_owner_id", out var storedId)) rejections.Add("companion-owner-not-assigned");
        else
        {
            if (!storedKind.Equals(ownerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)) rejections.Add("companion-owner-kind-mismatch");
            if (!storedId.Equals(ownerId ?? string.Empty, StringComparison.Ordinal)) rejections.Add("companion-owner-id-mismatch");
        }
        return rejections;
    }
}

public sealed class RuntimeNpcCompanionFollowSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCompanionFollowSmokeReport Run()
    {
        var report = new RuntimeNpcCompanionFollowSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcCompanionFollow)) is not null, "companion-follow-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcCompanionFollow)) is not null, "companion-follow-completion-exposed");
            report.Checks.AddRange(["companion-follow-admission-exposed", "companion-follow-completion-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
