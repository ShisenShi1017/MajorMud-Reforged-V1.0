using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcReturnHomeSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string CurrentRoomId,
    bool ReturnRequested,
    bool HomeRoomReachable,
    string EvidenceAuthority);

public sealed record RuntimeNpcReturnHomeResult(
    string NpcInstanceId,
    long WorldTick,
    bool Accepted,
    string CurrentRoomId,
    string? HomeRoomId,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

public sealed record RuntimeNpcReturnHomeMovementSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string FromRoomId,
    string DestinationRoomId,
    bool TraversalCompleted,
    string EvidenceAuthority);

public sealed record RuntimeNpcReturnHomeMovementResult(
    string NpcInstanceId,
    long WorldTick,
    bool Accepted,
    string FromRoomId,
    string? DestinationRoomId,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

/// <summary>
/// Admits return-home state only from an explicit host request and the instance's persisted canonical
/// home room. It does not infer leash distance, chase termination, route choice, or movement cost.
/// </summary>
public sealed class RuntimeNpcReturnHomeService
{
    public RuntimeNpcReturnHomeResult Apply(RuntimeLivingNpcState npc, RuntimeNpcReturnHomeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("return-home-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));

        var rejections = new List<string>();
        if (!npc.Alive) rejections.Add("npc-not-alive");
        if (!npc.RoomId.Equals(snapshot.CurrentRoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("current-room-mismatch");
        if (!snapshot.ReturnRequested) rejections.Add("return-home-not-explicitly-requested");
        if (string.IsNullOrWhiteSpace(npc.HomeRoomId)) rejections.Add("missing-home-room");
        if (npc.RoomId.Equals(npc.HomeRoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("npc-already-home");
        if (!snapshot.HomeRoomReachable) rejections.Add("home-room-not-explicitly-reachable");

        var accepted = rejections.Count == 0;
        return new RuntimeNpcReturnHomeResult(npc.InstanceId, snapshot.WorldTick, accepted, snapshot.CurrentRoomId,
            accepted ? npc.HomeRoomId : null, rejections,
            accepted ? "explicit-host-supplied-return-home-admission" : "safe-fallback-return-home-rejected", accepted);
    }
}

public sealed class RuntimeNpcReturnHomeMovementService
{
    public RuntimeNpcReturnHomeMovementResult Apply(RuntimeLivingNpcState npc, RuntimeNpcReturnHomeMovementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("return-home-movement-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));

        var rejections = new List<string>();
        if (!npc.Alive) rejections.Add("npc-not-alive");
        if (!npc.RoomId.Equals(snapshot.FromRoomId, StringComparison.OrdinalIgnoreCase)) rejections.Add("source-room-mismatch");
        if (!npc.Metadata.TryGetValue("npc_returning_home", out var returning) || !returning.Equals("true", StringComparison.OrdinalIgnoreCase))
            rejections.Add("npc-not-in-admitted-return-home-state");
        if (!npc.Metadata.TryGetValue("npc_return_home_destination_room_id", out var admitted) || string.IsNullOrWhiteSpace(admitted))
            rejections.Add("missing-admitted-home-destination");
        else if (!admitted.Equals(snapshot.DestinationRoomId, StringComparison.OrdinalIgnoreCase))
            rejections.Add("destination-does-not-match-admitted-home-room");
        if (!npc.HomeRoomId.Equals(snapshot.DestinationRoomId, StringComparison.OrdinalIgnoreCase))
            rejections.Add("destination-is-not-canonical-home-room");
        if (!snapshot.TraversalCompleted) rejections.Add("traversal-not-explicitly-completed");

        var accepted = rejections.Count == 0;
        return new RuntimeNpcReturnHomeMovementResult(npc.InstanceId, snapshot.WorldTick, accepted, snapshot.FromRoomId,
            accepted ? snapshot.DestinationRoomId : null, rejections,
            accepted ? "explicit-owning-movement-subsystem-return-home-completion" : "safe-fallback-return-home-movement-rejected", accepted);
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcReturnHomeResult ApplyNpcReturnHomeSnapshot(RuntimeNpcReturnHomeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var result = new RuntimeNpcReturnHomeService().Apply(npc, snapshot);
            if (result.Accepted && _world.FindRoomById(npc.HomeRoomId) is null)
                result = result with { Accepted = false, HomeRoomId = null, Rejections = new[] { "home-room-not-generated" }, EvidenceAuthority = "safe-fallback-return-home-rejected", StateChanged = false };

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["last_return_home_tick"] = snapshot.WorldTick.ToString(),
                ["last_return_home_authority"] = result.EvidenceAuthority,
                ["last_return_home_rejection_count"] = result.Rejections.Count.ToString()
            };
            if (result.Accepted)
            {
                metadata["npc_returning_home"] = "true";
                metadata["npc_return_home_destination_room_id"] = npc.HomeRoomId;
                metadata.Remove("current_target_id");
                metadata["combat_hostility_state"] = "returning-home";
            }
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }

    public RuntimeNpcReturnHomeMovementResult CompleteNpcReturnHomeMovement(RuntimeNpcReturnHomeMovementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var result = new RuntimeNpcReturnHomeMovementService().Apply(npc, snapshot);
            if (result.Accepted && _world.FindRoomById(snapshot.DestinationRoomId) is null)
                result = result with { Accepted = false, DestinationRoomId = null, Rejections = new[] { "destination-room-not-generated" }, EvidenceAuthority = "safe-fallback-return-home-movement-rejected", StateChanged = false };

            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["last_return_home_movement_tick"] = snapshot.WorldTick.ToString(),
                ["last_return_home_movement_authority"] = result.EvidenceAuthority,
                ["last_return_home_movement_rejection_count"] = result.Rejections.Count.ToString()
            };
            if (result.Accepted)
            {
                metadata.Remove("npc_returning_home");
                metadata.Remove("npc_return_home_destination_room_id");
                metadata.Remove("current_target_id");
                metadata["last_return_home_origin_room_id"] = snapshot.FromRoomId;
                metadata["last_return_home_completed_room_id"] = snapshot.DestinationRoomId;
                metadata["combat_hostility_state"] = "home";
                _npcs[npc.InstanceId] = npc with { RoomId = snapshot.DestinationRoomId, Metadata = metadata };
            }
            else _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }
}

public sealed class RuntimeNpcReturnHomeSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcReturnHomeSmokeReport Run()
    {
        var report = new RuntimeNpcReturnHomeSmokeReport();
        try
        {
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.2", "room.1", 10, 10, true, null, "hostile", 1, new Dictionary<string,string> { ["current_target_id"] = "target.1" });
            var admit = new RuntimeNpcReturnHomeService().Apply(npc, new("npc.i", 50, "room.2", true, true, "test"));
            Require(admit.Accepted && admit.HomeRoomId == "room.1", "explicit-return-home-admitted");
            report.Checks.Add("explicit-return-home-admitted");
            var metadata = new Dictionary<string,string> { ["npc_returning_home"]="true", ["npc_return_home_destination_room_id"]="room.1" };
            var admittedNpc = npc with { Metadata = metadata };
            var moved = new RuntimeNpcReturnHomeMovementService().Apply(admittedNpc, new("npc.i", 51, "room.2", "room.1", true, "test"));
            Require(moved.Accepted, "exact-return-home-movement-completes");
            report.Checks.Add("exact-return-home-movement-completes");
            var wrong = new RuntimeNpcReturnHomeMovementService().Apply(admittedNpc, new("npc.i", 51, "room.2", "room.3", true, "test"));
            Require(!wrong.Accepted && wrong.Rejections.Contains("destination-does-not-match-admitted-home-room"), "wrong-home-destination-rejected");
            report.Checks.Add("wrong-home-destination-rejected");
            Require(JsonSerializer.Serialize(moved) == JsonSerializer.Serialize(new RuntimeNpcReturnHomeMovementService().Apply(admittedNpc, new("npc.i", 51, "room.2", "room.1", true, "test"))), "deterministic-return-home-replay");
            report.Checks.Add("deterministic-return-home-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
