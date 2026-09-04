using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcFleeSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string RoomId,
    int CurrentHitPoints,
    int MaximumHitPoints,
    bool ExplicitFleeRequested,
    string? ExplicitDestinationRoomId,
    bool DestinationReachable,
    string EvidenceAuthority);

public sealed record RuntimeNpcFleeResult(
    string NpcInstanceId,
    long WorldTick,
    bool Accepted,
    string? DestinationRoomId,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

/// <summary>
/// Evidence-constrained flee admission. The host supplies an explicit flee request and an already
/// validated reachable destination. Native HP thresholds, morale, route choice, chase/leash,
/// movement cost and interruption rules remain unresolved and are not inferred here.
/// </summary>
public sealed class RuntimeNpcFleeService
{
    public RuntimeNpcFleeResult Apply(RuntimeLivingNpcState npc, RuntimeNpcFleeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("flee-snapshot-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));
        var rejects = new List<string>();
        if (!npc.Alive) rejects.Add("npc-not-alive");
        if (!npc.RoomId.Equals(snapshot.RoomId, StringComparison.OrdinalIgnoreCase)) rejects.Add("snapshot-room-mismatch");
        if (snapshot.MaximumHitPoints <= 0) rejects.Add("invalid-maximum-hit-points");
        if (snapshot.CurrentHitPoints < 0 || snapshot.CurrentHitPoints > snapshot.MaximumHitPoints) rejects.Add("invalid-current-hit-points");
        if (!snapshot.ExplicitFleeRequested) rejects.Add("flee-not-explicitly-requested");
        if (string.IsNullOrWhiteSpace(snapshot.ExplicitDestinationRoomId)) rejects.Add("missing-explicit-destination");
        if (snapshot.ExplicitDestinationRoomId?.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase) == true) rejects.Add("destination-is-current-room");
        if (!snapshot.DestinationReachable) rejects.Add("destination-not-explicitly-reachable");
        var accepted = rejects.Count == 0;
        return new(npc.InstanceId, snapshot.WorldTick, accepted,
            accepted ? snapshot.ExplicitDestinationRoomId : null, rejects,
            accepted ? "explicit-host-supplied-flee-destination" : "safe-fallback-flee-rejected",
            accepted);
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcFleeResult ApplyNpcFleeSnapshot(RuntimeNpcFleeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var result = new RuntimeNpcFleeService().Apply(npc, snapshot);
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["last_flee_tick"] = snapshot.WorldTick.ToString(),
                ["last_flee_authority"] = result.EvidenceAuthority,
                ["last_flee_rejection_count"] = result.Rejections.Count.ToString()
            };
            if (result.Accepted)
            {
                metadata["npc_fleeing"] = "true";
                metadata["npc_flee_destination_room_id"] = result.DestinationRoomId!;
                metadata.Remove("current_target_id");
            }
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }
}

public sealed class RuntimeNpcFleeSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcFleeSmokeReport Run()
    {
        var report = new RuntimeNpcFleeSmokeReport();
        try
        {
            var svc = new RuntimeNpcFleeService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 2, 10, true, null, "none", 1, new Dictionary<string,string>());
            var ok = new RuntimeNpcFleeSnapshot("npc.i", 40, "room.1", 2, 10, true, "room.2", true, "explicit-test");
            var accepted = svc.Apply(npc, ok);
            Require(accepted.Accepted && accepted.DestinationRoomId == "room.2", "explicit-flee-accepted"); report.Checks.Add("explicit-flee-accepted");
            var blocked = svc.Apply(npc, ok with { DestinationReachable = false });
            Require(!blocked.Accepted && blocked.DestinationRoomId is null, "unreachable-flee-rejected"); report.Checks.Add("unreachable-flee-rejected");
            Require(JsonSerializer.Serialize(accepted) == JsonSerializer.Serialize(svc.Apply(npc, ok)), "deterministic-flee-replay"); report.Checks.Add("deterministic-flee-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
