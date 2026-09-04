using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcFleeMovementSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string FromRoomId,
    string DestinationRoomId,
    bool TraversalCompleted,
    string EvidenceAuthority);

public sealed record RuntimeNpcFleeMovementResult(
    string NpcInstanceId,
    long WorldTick,
    bool Accepted,
    string FromRoomId,
    string? DestinationRoomId,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

/// <summary>
/// Commits a flee movement only after an owning movement subsystem reports that the exact persisted
/// flee destination was traversed. This boundary does not select exits, calculate movement cost,
/// bypass restrictions, or infer native flee/chase/leash behavior.
/// </summary>
public sealed class RuntimeNpcFleeMovementService
{
    public RuntimeNpcFleeMovementResult Apply(RuntimeLivingNpcState npc, RuntimeNpcFleeMovementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("flee-movement-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));

        var rejections = new List<string>();
        if (!npc.Alive) rejections.Add("npc-not-alive");
        if (!npc.RoomId.Equals(snapshot.FromRoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("source-room-mismatch");
        if (!npc.Metadata.TryGetValue("npc_fleeing", out var fleeing) || !fleeing.Equals("true", StringComparison.OrdinalIgnoreCase))
            rejections.Add("npc-not-in-admitted-flee-state");
        if (!npc.Metadata.TryGetValue("npc_flee_destination_room_id", out var admittedDestination) || string.IsNullOrWhiteSpace(admittedDestination))
            rejections.Add("missing-admitted-flee-destination");
        else if (!admittedDestination.Equals(snapshot.DestinationRoomId, StringComparison.OrdinalIgnoreCase))
            rejections.Add("destination-does-not-match-admitted-flee-destination");
        if (string.IsNullOrWhiteSpace(snapshot.DestinationRoomId)) rejections.Add("missing-destination-room");
        if (snapshot.DestinationRoomId.Equals(snapshot.FromRoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("destination-is-source-room");
        if (!snapshot.TraversalCompleted) rejections.Add("traversal-not-explicitly-completed");

        var accepted = rejections.Count == 0;
        return new RuntimeNpcFleeMovementResult(
            npc.InstanceId,
            snapshot.WorldTick,
            accepted,
            snapshot.FromRoomId,
            accepted ? snapshot.DestinationRoomId : null,
            rejections,
            accepted ? "explicit-owning-movement-subsystem-completion" : "safe-fallback-flee-movement-rejected",
            accepted);
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcFleeMovementResult CompleteNpcFleeMovement(RuntimeNpcFleeMovementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");

            var result = new RuntimeNpcFleeMovementService().Apply(npc, snapshot);
            if (result.Accepted && _world.FindRoomById(snapshot.DestinationRoomId) is null)
            {
                return result with
                {
                    Accepted = false,
                    DestinationRoomId = null,
                    Rejections = new[] { "destination-room-not-generated" },
                    EvidenceAuthority = "safe-fallback-flee-movement-rejected",
                    StateChanged = false
                };
            }

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["last_flee_movement_tick"] = snapshot.WorldTick.ToString(),
                ["last_flee_movement_authority"] = result.EvidenceAuthority,
                ["last_flee_movement_rejection_count"] = result.Rejections.Count.ToString()
            };

            if (result.Accepted)
            {
                metadata.Remove("npc_fleeing");
                metadata.Remove("npc_flee_destination_room_id");
                metadata.Remove("current_target_id");
                metadata["last_flee_origin_room_id"] = snapshot.FromRoomId;
                metadata["last_flee_completed_destination_room_id"] = snapshot.DestinationRoomId;
                metadata["combat_hostility_state"] = "fled";
                _npcs[npc.InstanceId] = npc with { RoomId = snapshot.DestinationRoomId, Metadata = metadata };
            }
            else
            {
                _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            }

            Save();
            return result;
        }
    }
}

public sealed class RuntimeNpcFleeMovementSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcFleeMovementSmokeReport Run()
    {
        var report = new RuntimeNpcFleeMovementSmokeReport();
        try
        {
            var service = new RuntimeNpcFleeMovementService();
            var metadata = new Dictionary<string,string>
            {
                ["npc_fleeing"] = "true",
                ["npc_flee_destination_room_id"] = "room.2"
            };
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 2, 10, true, null, "none", 1, metadata);
            var snapshot = new RuntimeNpcFleeMovementSnapshot("npc.i", 41, "room.1", "room.2", true, "movement-test");
            var accepted = service.Apply(npc, snapshot);
            Require(accepted.Accepted && accepted.DestinationRoomId == "room.2", "exact-admitted-flee-movement-completes");
            report.Checks.Add("exact-admitted-flee-movement-completes");

            var wrongDestination = service.Apply(npc, snapshot with { DestinationRoomId = "room.3" });
            Require(!wrongDestination.Accepted && wrongDestination.Rejections.Contains("destination-does-not-match-admitted-flee-destination"), "mismatched-destination-rejected");
            report.Checks.Add("mismatched-destination-rejected");

            var incomplete = service.Apply(npc, snapshot with { TraversalCompleted = false });
            Require(!incomplete.Accepted, "uncompleted-traversal-rejected");
            report.Checks.Add("uncompleted-traversal-rejected");

            Require(JsonSerializer.Serialize(accepted) == JsonSerializer.Serialize(service.Apply(npc, snapshot)), "deterministic-flee-movement-replay");
            report.Checks.Add("deterministic-flee-movement-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
