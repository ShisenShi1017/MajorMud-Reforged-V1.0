using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcThreatForgetEvent(
    string EventId,
    string TargetEntityId,
    bool ForgetExplicitlyAuthorized,
    string Reason,
    string EvidenceAuthority);

public sealed record RuntimeNpcThreatMemorySnapshot(
    string NpcInstanceId,
    long WorldTick,
    IReadOnlyList<RuntimeNpcThreatForgetEvent> Events);

public sealed record RuntimeNpcThreatMemoryResult(
    string NpcInstanceId,
    long WorldTick,
    bool Applied,
    string? SelectedTargetId,
    long? SelectedThreat,
    IReadOnlyDictionary<string,long> ThreatLedger,
    IReadOnlyList<string> ForgottenTargets,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority);

/// <summary>
/// Explicit threat-memory removal boundary. The host decides when a target is conclusively
/// forgettable; the runtime owns exact ledger removal, replay suppression, reranking and
/// persistence. Native memory duration, lost-target timeout and visibility-driven forgetting
/// remain unresolved and are not inferred.
/// </summary>
public sealed class RuntimeNpcThreatMemoryService
{
    public RuntimeNpcThreatMemoryResult Apply(
        RuntimeLivingNpcState npc,
        RuntimeNpcThreatMemorySnapshot snapshot,
        IReadOnlyDictionary<string,long> priorLedger,
        long? priorTick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(priorLedger);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("threat-memory-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));
        if (priorTick is long admittedTick && snapshot.WorldTick <= admittedTick)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), [],
                [snapshot.WorldTick == admittedTick ? "duplicate-threat-memory-tick" : "regressive-threat-memory-tick"],
                "persisted-monotonic-threat-memory-tick");
        if (!npc.Alive)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), [], ["npc-not-alive"], "runtime-npc-life-state");

        var ledger = new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase);
        var forgotten = new List<string>();
        var rejections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in snapshot.Events.OrderBy(x => x.EventId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(evt.EventId)) { rejections.Add("<empty>:missing-event-id"); continue; }
            if (!seen.Add(evt.EventId)) { rejections.Add($"{evt.EventId}:duplicate-event-id"); continue; }
            if (string.IsNullOrWhiteSpace(evt.TargetEntityId)) { rejections.Add($"{evt.EventId}:missing-target-id"); continue; }
            if (evt.TargetEntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{evt.EventId}:self-target"); continue; }
            if (!evt.ForgetExplicitlyAuthorized) { rejections.Add($"{evt.EventId}:forget-not-explicitly-authorized"); continue; }
            if (!ledger.Remove(evt.TargetEntityId)) { rejections.Add($"{evt.EventId}:target-not-in-threat-ledger"); continue; }
            forgotten.Add(evt.TargetEntityId);
        }

        var selected = ledger.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        return new(npc.InstanceId, snapshot.WorldTick, true,
            selected.Key, selected.Key is null ? null : selected.Value,
            ledger, forgotten, rejections, "explicit-host-authorized-threat-forgetting");
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcThreatMemoryResult ApplyNpcThreatMemorySnapshot(RuntimeNpcThreatMemorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var ledger = ReadThreatLedger(metadata);
            long? priorTick = metadata.TryGetValue("last_threat_memory_tick", out var tickText) && long.TryParse(tickText, out var parsedTick) ? parsedTick : null;
            var result = new RuntimeNpcThreatMemoryService().Apply(npc, snapshot, ledger, priorTick);
            if (!result.Applied) return result;

            metadata[ThreatLedgerMetadataKey] = JsonSerializer.Serialize(result.ThreatLedger.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
            metadata["last_threat_memory_tick"] = snapshot.WorldTick.ToString();
            metadata["last_threat_memory_authority"] = result.EvidenceAuthority;
            metadata["last_threat_memory_event_count"] = snapshot.Events.Count.ToString();
            metadata["last_threat_memory_forgotten_count"] = result.ForgottenTargets.Count.ToString();
            metadata["last_threat_memory_rejection_count"] = result.Rejections.Count.ToString();
            if (string.IsNullOrWhiteSpace(result.SelectedTargetId))
            {
                metadata.Remove("current_target_id");
                metadata.Remove("current_target_explicit_threat");
            }
            else
            {
                metadata["current_target_id"] = result.SelectedTargetId;
                metadata["current_target_explicit_threat"] = result.SelectedThreat!.Value.ToString();
            }
            _npcs[npc.InstanceId] = npc with { Metadata = metadata };
            Save();
            return result;
        }
    }
}

public sealed class RuntimeNpcThreatMemorySmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcThreatMemorySmokeReport Run()
    {
        var report = new RuntimeNpcThreatMemorySmokeReport();
        try
        {
            var svc = new RuntimeNpcThreatMemoryService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var snapshot = new RuntimeNpcThreatMemorySnapshot("npc.i", 40, [
                new("evt.2", "player.b", true, "explicit-lost-target", "explicit-test"),
                new("evt.1", "player.a", false, "not-authorized", "explicit-test")]);
            var ledger = new Dictionary<string,long>{{"player.a",5},{"player.b",7}};
            var first = svc.Apply(npc, snapshot, ledger, 39);
            Require(first.Applied && !first.ThreatLedger.ContainsKey("player.b") && first.SelectedTargetId == "player.a", "explicit-threat-forgetting-applies"); report.Checks.Add("explicit-threat-forgetting-applies");
            Require(first.Rejections.Any(x => x.Contains("forget-not-explicitly-authorized", StringComparison.Ordinal)), "unauthorized-forget-rejected"); report.Checks.Add("unauthorized-forget-rejected");
            var duplicate = svc.Apply(npc, snapshot, first.ThreatLedger, 40);
            Require(!duplicate.Applied && duplicate.Rejections.Contains("duplicate-threat-memory-tick"), "duplicate-memory-tick-suppressed"); report.Checks.Add("duplicate-memory-tick-suppressed");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(svc.Apply(npc, snapshot, ledger, 39)), "deterministic-threat-memory-replay"); report.Checks.Add("deterministic-threat-memory-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
