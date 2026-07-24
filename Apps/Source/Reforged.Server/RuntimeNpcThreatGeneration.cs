using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcThreatGenerationEvent(
    string EventId,
    string TargetEntityId,
    string TargetRoomId,
    bool TargetAlive,
    bool TargetDetectable,
    bool TargetExplicitlyHostile,
    long ExplicitThreatDelta,
    string SourceKind,
    string EvidenceAuthority);

public sealed record RuntimeNpcThreatGenerationSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string RoomId,
    IReadOnlyList<RuntimeNpcThreatGenerationEvent> Events);

public sealed record RuntimeNpcThreatGenerationResult(
    string NpcInstanceId,
    long WorldTick,
    bool Applied,
    string? SelectedTargetId,
    long? SelectedThreat,
    IReadOnlyDictionary<string,long> ThreatLedger,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority);

/// <summary>
/// Evidence-constrained threat generation boundary. The host supplies explicit positive threat
/// deltas and the runtime owns deterministic accumulation, replay suppression, checked arithmetic,
/// target ranking, and persistence. Native damage/healing/spell formulas, decay, taunt, hate
/// transfer, and memory duration remain unresolved and are not inferred.
/// </summary>
public sealed class RuntimeNpcThreatGenerationService
{
    public RuntimeNpcThreatGenerationResult Apply(
        RuntimeLivingNpcState npc,
        RuntimeNpcThreatGenerationSnapshot snapshot,
        IReadOnlyDictionary<string,long> priorLedger,
        long? priorTick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(priorLedger);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("threat-generation-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));
        if (priorTick is long admittedTick && snapshot.WorldTick <= admittedTick)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase),
                [snapshot.WorldTick == admittedTick ? "duplicate-threat-generation-tick" : "regressive-threat-generation-tick"],
                "persisted-monotonic-threat-generation-tick");
        if (!npc.Alive)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), ["npc-not-alive"], "runtime-npc-life-state");
        if (!npc.RoomId.Equals(snapshot.RoomId, StringComparison.OrdinalIgnoreCase))
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), ["snapshot-room-mismatch"], "runtime-room-ownership");

        var ledger = new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase);
        var rejections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in snapshot.Events.OrderBy(x => x.EventId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(evt.EventId)) { rejections.Add("<empty>:missing-event-id"); continue; }
            if (!seen.Add(evt.EventId)) { rejections.Add($"{evt.EventId}:duplicate-event-id"); continue; }
            if (string.IsNullOrWhiteSpace(evt.TargetEntityId)) { rejections.Add($"{evt.EventId}:missing-target-id"); continue; }
            if (evt.TargetEntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{evt.EventId}:self-target"); continue; }
            if (!evt.TargetAlive) { rejections.Add($"{evt.EventId}:target-dead"); continue; }
            if (!evt.TargetRoomId.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{evt.EventId}:target-different-room"); continue; }
            if (!evt.TargetDetectable) { rejections.Add($"{evt.EventId}:target-not-explicitly-detectable"); continue; }
            if (!evt.TargetExplicitlyHostile) { rejections.Add($"{evt.EventId}:target-not-explicitly-hostile"); continue; }
            if (evt.ExplicitThreatDelta <= 0) { rejections.Add($"{evt.EventId}:nonpositive-explicit-threat-delta"); continue; }
            ledger.TryGetValue(evt.TargetEntityId, out var current);
            ledger[evt.TargetEntityId] = checked(current + evt.ExplicitThreatDelta);
        }

        var selected = ledger.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        return new(npc.InstanceId, snapshot.WorldTick, true,
            selected.Key, selected.Key is null ? null : selected.Value,
            ledger, rejections, "explicit-host-supplied-threat-deltas");
    }
}

public sealed partial class RuntimeLivingWorldService
{
    private const string ThreatLedgerMetadataKey = "npc_explicit_threat_ledger_json";

    public RuntimeNpcThreatGenerationResult ApplyNpcThreatGenerationSnapshot(RuntimeNpcThreatGenerationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var ledger = ReadThreatLedger(metadata);
            long? priorTick = metadata.TryGetValue("last_threat_generation_tick", out var tickText) && long.TryParse(tickText, out var parsedTick) ? parsedTick : null;
            var result = new RuntimeNpcThreatGenerationService().Apply(npc, snapshot, ledger, priorTick);
            if (!result.Applied) return result;

            metadata[ThreatLedgerMetadataKey] = JsonSerializer.Serialize(result.ThreatLedger.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
            metadata["last_threat_generation_tick"] = snapshot.WorldTick.ToString();
            metadata["last_threat_generation_authority"] = result.EvidenceAuthority;
            metadata["last_threat_generation_event_count"] = snapshot.Events.Count.ToString();
            metadata["last_threat_generation_rejection_count"] = result.Rejections.Count.ToString();
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

    private static IReadOnlyDictionary<string,long> ReadThreatLedger(IReadOnlyDictionary<string,string> metadata)
    {
        if (!metadata.TryGetValue(ThreatLedgerMetadataKey, out var json) || string.IsNullOrWhiteSpace(json))
            return new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string,long>>(json) is { } parsed
                ? new Dictionary<string,long>(parsed, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("invalid-persisted-threat-ledger", ex);
        }
    }
}

public sealed class RuntimeNpcThreatGenerationSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcThreatGenerationSmokeReport Run()
    {
        var report = new RuntimeNpcThreatGenerationSmokeReport();
        try
        {
            var svc = new RuntimeNpcThreatGenerationService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var snapshot = new RuntimeNpcThreatGenerationSnapshot("npc.i", 25, "room.1", [
                new("evt.2", "player.b", "room.1", true, true, true, 3, "damage", "explicit-test"),
                new("evt.1", "player.a", "room.1", true, true, true, 5, "damage", "explicit-test"),
                new("evt.bad", "player.bad", "room.1", true, true, true, 0, "unknown", "explicit-test")]);
            var first = svc.Apply(npc, snapshot, new Dictionary<string,long>{{"player.b",4}}, 24);
            Require(first.Applied && first.ThreatLedger["player.b"] == 7 && first.SelectedTargetId == "player.b", "explicit-threat-deltas-accumulate"); report.Checks.Add("explicit-threat-deltas-accumulate");
            Require(first.Rejections.Any(x => x.Contains("nonpositive-explicit-threat-delta", StringComparison.Ordinal)), "nonpositive-delta-rejected"); report.Checks.Add("nonpositive-delta-rejected");
            var duplicate = svc.Apply(npc, snapshot, first.ThreatLedger, 25);
            Require(!duplicate.Applied && duplicate.Rejections.Contains("duplicate-threat-generation-tick"), "duplicate-tick-suppressed"); report.Checks.Add("duplicate-tick-suppressed");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(svc.Apply(npc, snapshot, new Dictionary<string,long>{{"player.b",4}}, 24)), "deterministic-threat-generation-replay"); report.Checks.Add("deterministic-threat-generation-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
