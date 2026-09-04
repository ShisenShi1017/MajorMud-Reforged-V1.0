using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcPerceptionCandidate(
    string EntityId,
    string RoomId,
    bool Alive,
    bool Detectable,
    bool ExplicitlyHostile,
    string EvidenceAuthority,
    IReadOnlyDictionary<string,string>? Metadata = null);

public sealed record RuntimeNpcPerceptionSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string RoomId,
    IReadOnlyList<RuntimeNpcPerceptionCandidate> Candidates);

public sealed record RuntimeNpcTargetAcquisitionResult(
    string NpcInstanceId,
    long WorldTick,
    string? PriorTargetId,
    string? SelectedTargetId,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

/// <summary>
/// Evidence-constrained perception boundary. The caller owns observation production.
/// This runtime activates only same-room, alive, explicitly detectable and explicitly hostile
/// candidates. Native sight/hearing ranges, stealth rolls, faction rules, threat ranking and
/// target memory remain unresolved and are never inferred here.
/// </summary>
public sealed class RuntimeNpcPerceptionService
{
    public RuntimeNpcTargetAcquisitionResult Acquire(
        RuntimeLivingNpcState npc,
        RuntimeNpcPerceptionSnapshot snapshot,
        string? priorTargetId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("perception-snapshot-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));

        var rejections = new List<string>();
        if (!npc.Alive)
            return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, null,
                ["npc-not-alive"], "runtime-npc-life-state", !string.IsNullOrWhiteSpace(priorTargetId));
        if (!npc.RoomId.Equals(snapshot.RoomId, StringComparison.OrdinalIgnoreCase))
            return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, null,
                ["snapshot-room-mismatch"], "runtime-room-ownership", !string.IsNullOrWhiteSpace(priorTargetId));

        var admitted = new List<RuntimeNpcPerceptionCandidate>();
        foreach (var candidate in snapshot.Candidates.OrderBy(x => x.EntityId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(candidate.EntityId)) { rejections.Add("<empty>:missing-entity-id"); continue; }
            if (candidate.EntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{candidate.EntityId}:self"); continue; }
            if (candidate.EntityId.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{candidate.EntityId}:npc-vs-npc-hostility-not-classic"); continue; }
            if (!candidate.Alive) { rejections.Add($"{candidate.EntityId}:dead"); continue; }
            if (!candidate.RoomId.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{candidate.EntityId}:different-room"); continue; }
            if (!candidate.Detectable) { rejections.Add($"{candidate.EntityId}:not-explicitly-detectable"); continue; }
            if (!candidate.ExplicitlyHostile) { rejections.Add($"{candidate.EntityId}:not-explicitly-hostile"); continue; }
            admitted.Add(candidate);
        }

        RuntimeNpcPerceptionCandidate? selected = null;
        if (!string.IsNullOrWhiteSpace(priorTargetId))
            selected = admitted.FirstOrDefault(x => x.EntityId.Equals(priorTargetId, StringComparison.OrdinalIgnoreCase));
        selected ??= admitted.FirstOrDefault();
        var selectedId = selected?.EntityId;
        return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, selectedId, rejections,
            selected is null ? "safe-fallback-no-explicit-perception-candidate" : "explicit-same-room-detectable-hostile-candidate",
            !string.Equals(priorTargetId, selectedId, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcTargetAcquisitionResult ApplyNpcPerceptionSnapshot(RuntimeNpcPerceptionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            metadata.TryGetValue("current_target_id", out var prior);
            var result = new RuntimeNpcPerceptionService().Acquire(npc, snapshot, prior);
            if (result.StateChanged)
            {
                if (string.IsNullOrWhiteSpace(result.SelectedTargetId)) metadata.Remove("current_target_id");
                else metadata["current_target_id"] = result.SelectedTargetId;
            }
            metadata["last_perception_tick"] = snapshot.WorldTick.ToString();
            metadata["last_perception_authority"] = result.EvidenceAuthority;
            metadata["last_perception_candidate_count"] = snapshot.Candidates.Count.ToString();
            metadata["last_perception_rejection_count"] = result.Rejections.Count.ToString();
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }

    public bool ClearNpcTargetIfInvalid(string instanceId, string entityId, string roomId, bool alive, bool detectable, bool explicitlyHostile, long worldTick)
    {
        var snapshot = new RuntimeNpcPerceptionSnapshot(instanceId, worldTick,
            _npcs.TryGetValue(instanceId, out var npc) ? npc.RoomId : roomId,
            [new RuntimeNpcPerceptionCandidate(entityId, roomId, alive, detectable, explicitlyHostile, "explicit-target-validation")]);
        var result = ApplyNpcPerceptionSnapshot(snapshot);
        return result.StateChanged;
    }
}

public sealed class RuntimeNpcPerceptionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcPerceptionSmokeReport Run()
    {
        var report = new RuntimeNpcPerceptionSmokeReport();
        try
        {
            var svc = new RuntimeNpcPerceptionService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var snapshot = new RuntimeNpcPerceptionSnapshot("npc.i", 10, "room.1", [
                new("player.z", "room.1", true, true, true, "explicit-test"),
                new("player.a", "room.1", true, true, true, "explicit-test"),
                new("player.hidden", "room.1", true, false, true, "explicit-test"),
                new("player.neutral", "room.1", true, true, false, "explicit-test")]);
            var first = svc.Acquire(npc, snapshot, null);
            Require(first.SelectedTargetId == "player.a", "stable-explicit-hostile-order"); report.Checks.Add("stable-explicit-hostile-order");
            var retained = svc.Acquire(npc, snapshot, "player.z");
            Require(retained.SelectedTargetId == "player.z", "retain-valid-prior-target"); report.Checks.Add("retain-valid-prior-target");
            var empty = svc.Acquire(npc, snapshot with { Candidates = [new("player.hidden", "room.1", true, false, true, "explicit-test")] }, "player.z");
            Require(empty.SelectedTargetId is null && empty.StateChanged, "clear-invalid-target"); report.Checks.Add("clear-invalid-target");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(svc.Acquire(npc, snapshot, null)), "deterministic-perception-replay"); report.Checks.Add("deterministic-perception-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
