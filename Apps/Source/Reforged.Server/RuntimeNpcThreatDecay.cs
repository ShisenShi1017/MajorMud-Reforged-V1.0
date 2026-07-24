using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcThreatDecayEvent(
    string EventId,
    string TargetEntityId,
    long ExplicitThreatReduction,
    bool RemoveWhenZero,
    string SourceKind,
    string EvidenceAuthority);

public sealed record RuntimeNpcThreatDecaySnapshot(
    string NpcInstanceId,
    long WorldTick,
    IReadOnlyList<RuntimeNpcThreatDecayEvent> Events);

public sealed record RuntimeNpcThreatDecayResult(
    string NpcInstanceId,
    long WorldTick,
    bool Applied,
    string? SelectedTargetId,
    long? SelectedThreat,
    IReadOnlyDictionary<string,long> ThreatLedger,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority);

/// <summary>
/// Evidence-constrained threat-reduction boundary. The host supplies explicit positive reductions;
/// the runtime owns deterministic subtraction, zero-flooring/removal, replay suppression, ranking,
/// and persistence. Native decay cadence, percentage formulas, memory duration, taunt and hate
/// transfer remain unresolved and are not inferred.
/// </summary>
public sealed class RuntimeNpcThreatDecayService
{
    public RuntimeNpcThreatDecayResult Apply(
        RuntimeLivingNpcState npc,
        RuntimeNpcThreatDecaySnapshot snapshot,
        IReadOnlyDictionary<string,long> priorLedger,
        long? priorTick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(priorLedger);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("threat-decay-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));
        if (priorTick is long admittedTick && snapshot.WorldTick <= admittedTick)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase),
                [snapshot.WorldTick == admittedTick ? "duplicate-threat-decay-tick" : "regressive-threat-decay-tick"],
                "persisted-monotonic-threat-decay-tick");
        if (!npc.Alive)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), ["npc-not-alive"], "runtime-npc-life-state");

        var ledger = new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase);
        var rejections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in snapshot.Events.OrderBy(x => x.EventId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(evt.EventId)) { rejections.Add("<empty>:missing-event-id"); continue; }
            if (!seen.Add(evt.EventId)) { rejections.Add($"{evt.EventId}:duplicate-event-id"); continue; }
            if (string.IsNullOrWhiteSpace(evt.TargetEntityId)) { rejections.Add($"{evt.EventId}:missing-target-id"); continue; }
            if (evt.TargetEntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{evt.EventId}:self-target"); continue; }
            if (evt.ExplicitThreatReduction <= 0) { rejections.Add($"{evt.EventId}:nonpositive-explicit-threat-reduction"); continue; }
            if (!ledger.TryGetValue(evt.TargetEntityId, out var current)) { rejections.Add($"{evt.EventId}:target-not-in-threat-ledger"); continue; }
            var remaining = current > evt.ExplicitThreatReduction ? current - evt.ExplicitThreatReduction : 0;
            if (remaining == 0 && evt.RemoveWhenZero) ledger.Remove(evt.TargetEntityId);
            else ledger[evt.TargetEntityId] = remaining;
        }

        var selected = ledger.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        return new(npc.InstanceId, snapshot.WorldTick, true,
            selected.Key, selected.Key is null ? null : selected.Value,
            ledger, rejections, "explicit-host-supplied-threat-reductions");
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcThreatDecayResult ApplyNpcThreatDecaySnapshot(RuntimeNpcThreatDecaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var ledger = ReadThreatLedger(metadata);
            long? priorTick = metadata.TryGetValue("last_threat_decay_tick", out var tickText) && long.TryParse(tickText, out var parsedTick) ? parsedTick : null;
            var result = new RuntimeNpcThreatDecayService().Apply(npc, snapshot, ledger, priorTick);
            if (!result.Applied) return result;

            metadata[ThreatLedgerMetadataKey] = JsonSerializer.Serialize(result.ThreatLedger.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
            metadata["last_threat_decay_tick"] = snapshot.WorldTick.ToString();
            metadata["last_threat_decay_authority"] = result.EvidenceAuthority;
            metadata["last_threat_decay_event_count"] = snapshot.Events.Count.ToString();
            metadata["last_threat_decay_rejection_count"] = result.Rejections.Count.ToString();
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

public sealed class RuntimeNpcThreatDecaySmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcThreatDecaySmokeReport Run()
    {
        var report = new RuntimeNpcThreatDecaySmokeReport();
        try
        {
            var svc = new RuntimeNpcThreatDecayService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var snapshot = new RuntimeNpcThreatDecaySnapshot("npc.i", 30, [
                new("evt.2", "player.b", 2, true, "elapsed", "explicit-test"),
                new("evt.1", "player.a", 5, true, "elapsed", "explicit-test"),
                new("evt.bad", "player.x", 1, true, "elapsed", "explicit-test")]);
            var ledger = new Dictionary<string,long>{{"player.a",5},{"player.b",7}};
            var first = svc.Apply(npc, snapshot, ledger, 29);
            Require(first.Applied && !first.ThreatLedger.ContainsKey("player.a") && first.ThreatLedger["player.b"] == 5 && first.SelectedTargetId == "player.b", "explicit-threat-reductions-apply"); report.Checks.Add("explicit-threat-reductions-apply");
            Require(first.Rejections.Any(x => x.Contains("target-not-in-threat-ledger", StringComparison.Ordinal)), "unknown-ledger-target-rejected"); report.Checks.Add("unknown-ledger-target-rejected");
            var duplicate = svc.Apply(npc, snapshot, first.ThreatLedger, 30);
            Require(!duplicate.Applied && duplicate.Rejections.Contains("duplicate-threat-decay-tick"), "duplicate-decay-tick-suppressed"); report.Checks.Add("duplicate-decay-tick-suppressed");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(svc.Apply(npc, snapshot, ledger, 29)), "deterministic-threat-decay-replay"); report.Checks.Add("deterministic-threat-decay-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
