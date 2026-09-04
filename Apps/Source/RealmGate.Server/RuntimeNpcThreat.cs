using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcThreatCandidate(
    string EntityId,
    string RoomId,
    bool Alive,
    bool Detectable,
    bool ExplicitlyHostile,
    long ExplicitThreat,
    string EvidenceAuthority,
    IReadOnlyDictionary<string,string>? Metadata = null);

public sealed record RuntimeNpcThreatSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string RoomId,
    IReadOnlyList<RuntimeNpcThreatCandidate> Candidates);

public sealed record RuntimeNpcThreatSelectionResult(
    string NpcInstanceId,
    long WorldTick,
    string? PriorTargetId,
    string? SelectedTargetId,
    long? SelectedThreat,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority,
    bool StateChanged);

/// <summary>
/// Evidence-constrained threat-selection boundary. The caller owns threat generation and submits
/// explicit scores. The runtime validates candidate admission and ranks by descending explicit
/// threat with stable ordinal tie-breaking. Native threat generation, decay, taunt, healing aggro,
/// assist propagation, and memory duration remain unresolved and are never inferred here.
/// </summary>
public sealed class RuntimeNpcThreatService
{
    public RuntimeNpcThreatSelectionResult Select(
        RuntimeLivingNpcState npc,
        RuntimeNpcThreatSnapshot snapshot,
        string? priorTargetId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("threat-snapshot-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));

        var rejections = new List<string>();
        if (!npc.Alive)
            return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, null, null,
                ["npc-not-alive"], "runtime-npc-life-state", !string.IsNullOrWhiteSpace(priorTargetId));
        if (!npc.RoomId.Equals(snapshot.RoomId, StringComparison.OrdinalIgnoreCase))
            return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, null, null,
                ["snapshot-room-mismatch"], "runtime-room-ownership", !string.IsNullOrWhiteSpace(priorTargetId));

        var admitted = new List<RuntimeNpcThreatCandidate>();
        foreach (var candidate in snapshot.Candidates.OrderBy(x => x.EntityId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(candidate.EntityId)) { rejections.Add("<empty>:missing-entity-id"); continue; }
            if (candidate.EntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{candidate.EntityId}:self"); continue; }
            if (candidate.EntityId.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{candidate.EntityId}:npc-vs-npc-hostility-not-classic"); continue; }
            if (!candidate.Alive) { rejections.Add($"{candidate.EntityId}:dead"); continue; }
            if (!candidate.RoomId.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{candidate.EntityId}:different-room"); continue; }
            if (!candidate.Detectable) { rejections.Add($"{candidate.EntityId}:not-explicitly-detectable"); continue; }
            if (!candidate.ExplicitlyHostile) { rejections.Add($"{candidate.EntityId}:not-explicitly-hostile"); continue; }
            if (candidate.ExplicitThreat < 0) { rejections.Add($"{candidate.EntityId}:negative-explicit-threat"); continue; }
            admitted.Add(candidate);
        }

        var selected = admitted
            .OrderByDescending(x => x.ExplicitThreat)
            .ThenBy(x => x.EntityId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var selectedId = selected?.EntityId;
        return new(npc.InstanceId, snapshot.WorldTick, priorTargetId, selectedId, selected?.ExplicitThreat,
            rejections,
            selected is null ? "safe-fallback-no-explicit-threat-candidate" : "explicit-host-supplied-threat-ranking",
            !string.Equals(priorTargetId, selectedId, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcThreatSelectionResult ApplyNpcThreatSnapshot(RuntimeNpcThreatSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            metadata.TryGetValue("current_target_id", out var prior);
            var result = new RuntimeNpcThreatService().Select(npc, snapshot, prior);
            if (result.StateChanged)
            {
                if (string.IsNullOrWhiteSpace(result.SelectedTargetId)) metadata.Remove("current_target_id");
                else metadata["current_target_id"] = result.SelectedTargetId;
            }
            metadata["last_threat_tick"] = snapshot.WorldTick.ToString();
            metadata["last_threat_authority"] = result.EvidenceAuthority;
            metadata["last_threat_candidate_count"] = snapshot.Candidates.Count.ToString();
            metadata["last_threat_rejection_count"] = result.Rejections.Count.ToString();
            if (result.SelectedThreat is long selectedThreat) metadata["current_target_explicit_threat"] = selectedThreat.ToString();
            else metadata.Remove("current_target_explicit_threat");
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }
}

public sealed class RuntimeNpcThreatSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcThreatSmokeReport Run()
    {
        var report = new RuntimeNpcThreatSmokeReport();
        try
        {
            var svc = new RuntimeNpcThreatService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var snapshot = new RuntimeNpcThreatSnapshot("npc.i", 20, "room.1", [
                new("player.b", "room.1", true, true, true, 5, "explicit-test"),
                new("player.a", "room.1", true, true, true, 5, "explicit-test"),
                new("player.high", "room.1", true, true, true, 9, "explicit-test"),
                new("player.hidden", "room.1", true, false, true, 100, "explicit-test"),
                new("player.negative", "room.1", true, true, true, -1, "explicit-test")]);
            var first = svc.Select(npc, snapshot, "player.a");
            Require(first.SelectedTargetId == "player.high" && first.SelectedThreat == 9, "highest-explicit-threat-selected"); report.Checks.Add("highest-explicit-threat-selected");
            var tie = svc.Select(npc, snapshot with { Candidates = snapshot.Candidates.Where(x => x.ExplicitThreat == 5).ToArray() }, null);
            Require(tie.SelectedTargetId == "player.a", "stable-threat-tie-break"); report.Checks.Add("stable-threat-tie-break");
            Require(first.Rejections.Any(x => x.Contains("negative-explicit-threat", StringComparison.Ordinal)), "negative-threat-rejected"); report.Checks.Add("negative-threat-rejected");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(svc.Select(npc, snapshot, "player.a")), "deterministic-threat-replay"); report.Checks.Add("deterministic-threat-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
