using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcAssistSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string RoomId,
    string CallerEntityId,
    bool CallerAlive,
    bool CallerDetectable,
    string TargetEntityId,
    string TargetRoomId,
    bool TargetAlive,
    bool TargetDetectable,
    bool TargetExplicitlyHostile,
    string EvidenceAuthority);

public sealed record RuntimeNpcAssistResult(
    string NpcInstanceId,
    long WorldTick,
    string? PriorTargetId,
    string? SelectedTargetId,
    bool Accepted,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

/// <summary>
/// Evidence-constrained assist boundary. The host supplies an explicit caller and already-observed
/// hostile target. The runtime validates ownership and same-room admission only. Native assist
/// radius, faction/alliance rules, propagation, threat transfer, memory and cooldowns are unresolved.
/// </summary>
public sealed class RuntimeNpcAssistService
{
    public RuntimeNpcAssistResult Apply(RuntimeLivingNpcState npc, RuntimeNpcAssistSnapshot snapshot, string? priorTargetId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("assist-snapshot-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));
        var rejects = new List<string>();
        if (!npc.Alive) rejects.Add("npc-not-alive");
        if (!npc.RoomId.Equals(snapshot.RoomId, StringComparison.OrdinalIgnoreCase)) rejects.Add("snapshot-room-mismatch");
        if (string.IsNullOrWhiteSpace(snapshot.CallerEntityId)) rejects.Add("missing-caller");
        if (!snapshot.CallerAlive) rejects.Add("caller-dead");
        if (!snapshot.CallerDetectable) rejects.Add("caller-not-explicitly-detectable");
        if (string.IsNullOrWhiteSpace(snapshot.TargetEntityId)) rejects.Add("missing-target");
        if (snapshot.TargetEntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) rejects.Add("self-target");
        if (!snapshot.TargetAlive) rejects.Add("target-dead");
        if (!snapshot.TargetRoomId.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) rejects.Add("target-different-room");
        if (!snapshot.TargetDetectable) rejects.Add("target-not-explicitly-detectable");
        if (!snapshot.TargetExplicitlyHostile) rejects.Add("target-not-explicitly-hostile");
        var accepted = rejects.Count == 0;
        var selected = accepted ? snapshot.TargetEntityId : priorTargetId;
        return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, selected, accepted, rejects,
            accepted ? "explicit-host-supplied-assist-target" : "safe-fallback-assist-rejected",
            accepted && !string.Equals(priorTargetId, selected, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcAssistResult ApplyNpcAssistSnapshot(RuntimeNpcAssistSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            metadata.TryGetValue("current_target_id", out var prior);
            var result = new RuntimeNpcAssistService().Apply(npc, snapshot, prior);
            if (result.Accepted) metadata["current_target_id"] = result.SelectedTargetId!;
            metadata["last_assist_tick"] = snapshot.WorldTick.ToString();
            metadata["last_assist_authority"] = result.EvidenceAuthority;
            metadata["last_assist_caller_id"] = snapshot.CallerEntityId;
            metadata["last_assist_target_id"] = snapshot.TargetEntityId;
            metadata["last_assist_rejection_count"] = result.Rejections.Count.ToString();
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }
}

public sealed class RuntimeNpcAssistSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcAssistSmokeReport Run()
    {
        var report = new RuntimeNpcAssistSmokeReport();
        try
        {
            var svc = new RuntimeNpcAssistService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var ok = new RuntimeNpcAssistSnapshot("npc.i", 30, "room.1", "npc.caller", true, true, "player.a", "room.1", true, true, true, "explicit-test");
            var accepted = svc.Apply(npc, ok, null);
            Require(accepted.Accepted && accepted.SelectedTargetId == "player.a", "explicit-assist-accepted"); report.Checks.Add("explicit-assist-accepted");
            var hidden = svc.Apply(npc, ok with { TargetDetectable = false }, "player.old");
            Require(!hidden.Accepted && hidden.SelectedTargetId == "player.old", "invalid-assist-does-not-overwrite-target"); report.Checks.Add("invalid-assist-does-not-overwrite-target");
            Require(JsonSerializer.Serialize(accepted) == JsonSerializer.Serialize(svc.Apply(npc, ok, null)), "deterministic-assist-replay"); report.Checks.Add("deterministic-assist-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
