using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcThreatTauntEvent(
    string EventId,
    string TargetEntityId,
    string TargetRoomId,
    bool TargetAlive,
    bool TargetDetectable,
    bool TargetExplicitlyHostile,
    bool TauntExplicitlyAuthorized,
    long ExplicitThreatFloor,
    string Reason,
    string EvidenceAuthority);

public sealed record RuntimeNpcThreatTauntSnapshot(
    string NpcInstanceId,
    long WorldTick,
    string RoomId,
    IReadOnlyList<RuntimeNpcThreatTauntEvent> Events);

public sealed record RuntimeNpcThreatTauntResult(
    string NpcInstanceId,
    long WorldTick,
    bool Applied,
    string? SelectedTargetId,
    long? SelectedThreat,
    IReadOnlyDictionary<string,long> ThreatLedger,
    IReadOnlyList<string> AppliedEventIds,
    IReadOnlyList<string> Rejections,
    string EvidenceAuthority);

/// <summary>
/// Explicit taunt admission boundary. The host supplies exact authorization, target validity and
/// a nonnegative threat floor. The runtime owns deterministic event admission, ledger flooring,
/// exact target override, replay suppression and persistence. Native taunt strength, duration,
/// resistance, immunity, cooldown and hate-transfer formulas remain unresolved and are not inferred.
/// </summary>
public sealed class RuntimeNpcThreatTauntService
{
    public RuntimeNpcThreatTauntResult Apply(
        RuntimeLivingNpcState npc,
        RuntimeNpcThreatTauntSnapshot snapshot,
        IReadOnlyDictionary<string,long> priorLedger,
        long? priorTick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(priorLedger);
        if (!npc.InstanceId.Equals(snapshot.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("threat-taunt-owner-mismatch");
        if (snapshot.WorldTick < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.WorldTick));
        if (priorTick is long admittedTick && snapshot.WorldTick <= admittedTick)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), [],
                [snapshot.WorldTick == admittedTick ? "duplicate-threat-taunt-tick" : "regressive-threat-taunt-tick"],
                "persisted-monotonic-threat-taunt-tick");
        if (!npc.Alive)
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), [], ["npc-not-alive"], "runtime-npc-life-state");
        if (!npc.RoomId.Equals(snapshot.RoomId, StringComparison.OrdinalIgnoreCase))
            return new(npc.InstanceId, snapshot.WorldTick, false, null, null,
                new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase), [], ["snapshot-room-mismatch"], "runtime-room-ownership");

        var ledger = new Dictionary<string,long>(priorLedger, StringComparer.OrdinalIgnoreCase);
        var applied = new List<string>();
        var rejections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? selectedTarget = null;
        long? selectedThreat = null;
        foreach (var evt in snapshot.Events.OrderBy(x => x.EventId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(evt.EventId)) { rejections.Add("<empty>:missing-event-id"); continue; }
            if (!seen.Add(evt.EventId)) { rejections.Add($"{evt.EventId}:duplicate-event-id"); continue; }
            if (!evt.TauntExplicitlyAuthorized) { rejections.Add($"{evt.EventId}:taunt-not-explicitly-authorized"); continue; }
            if (string.IsNullOrWhiteSpace(evt.TargetEntityId)) { rejections.Add($"{evt.EventId}:missing-target-id"); continue; }
            if (evt.TargetEntityId.Equals(npc.InstanceId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{evt.EventId}:self-target"); continue; }
            if (!evt.TargetAlive) { rejections.Add($"{evt.EventId}:target-dead"); continue; }
            if (!evt.TargetRoomId.Equals(npc.RoomId, StringComparison.OrdinalIgnoreCase)) { rejections.Add($"{evt.EventId}:target-different-room"); continue; }
            if (!evt.TargetDetectable) { rejections.Add($"{evt.EventId}:target-not-explicitly-detectable"); continue; }
            if (!evt.TargetExplicitlyHostile) { rejections.Add($"{evt.EventId}:target-not-explicitly-hostile"); continue; }
            if (evt.ExplicitThreatFloor < 0) { rejections.Add($"{evt.EventId}:negative-explicit-threat-floor"); continue; }
            ledger.TryGetValue(evt.TargetEntityId, out var current);
            var floored = Math.Max(current, evt.ExplicitThreatFloor);
            ledger[evt.TargetEntityId] = floored;
            selectedTarget = evt.TargetEntityId;
            selectedThreat = floored;
            applied.Add(evt.EventId);
        }

        if (selectedTarget is null)
        {
            var ranked = ledger.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            selectedTarget = ranked.Key;
            selectedThreat = ranked.Key is null ? null : ranked.Value;
        }
        return new(npc.InstanceId, snapshot.WorldTick, true, selectedTarget, selectedThreat,
            ledger, applied, rejections, "explicit-host-authorized-taunt-target-and-threat-floor");
    }
}

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcThreatTauntResult ApplyNpcThreatTauntSnapshot(RuntimeNpcThreatTauntSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!_npcs.TryGetValue(snapshot.NpcInstanceId, out var npc))
                throw new InvalidOperationException($"npc instance {snapshot.NpcInstanceId} is not materialized");
            var metadata = new Dictionary<string,string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var ledger = ReadThreatLedger(metadata);
            long? priorTick = metadata.TryGetValue("last_threat_taunt_tick", out var tickText) && long.TryParse(tickText, out var parsedTick) ? parsedTick : null;
            var result = new RuntimeNpcThreatTauntService().Apply(npc, snapshot, ledger, priorTick);
            if (!result.Applied) return result;

            metadata[ThreatLedgerMetadataKey] = JsonSerializer.Serialize(result.ThreatLedger.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
            metadata["last_threat_taunt_tick"] = snapshot.WorldTick.ToString();
            metadata["last_threat_taunt_authority"] = result.EvidenceAuthority;
            metadata["last_threat_taunt_event_count"] = snapshot.Events.Count.ToString();
            metadata["last_threat_taunt_applied_count"] = result.AppliedEventIds.Count.ToString();
            metadata["last_threat_taunt_rejection_count"] = result.Rejections.Count.ToString();
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

public sealed class RuntimeNpcThreatTauntSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcThreatTauntSmokeReport Run()
    {
        var report = new RuntimeNpcThreatTauntSmokeReport();
        try
        {
            var svc = new RuntimeNpcThreatTauntService();
            var npc = new RuntimeLivingNpcState("npc.i", "spawn.1", "npc.1", "Guard", "room.1", "room.1", 10, 10, true, null, "none", 1, new Dictionary<string,string>());
            var snapshot = new RuntimeNpcThreatTauntSnapshot("npc.i", 50, "room.1", [
                new("evt.2", "player.b", "room.1", true, true, true, true, 8, "explicit-taunt", "explicit-test"),
                new("evt.1", "player.a", "room.1", true, true, true, false, 20, "unauthorized", "explicit-test")]);
            var ledger = new Dictionary<string,long>{{"player.a",12},{"player.b",3}};
            var first = svc.Apply(npc, snapshot, ledger, 49);
            Require(first.Applied && first.SelectedTargetId == "player.b" && first.ThreatLedger["player.b"] == 8, "explicit-taunt-target-override-applies"); report.Checks.Add("explicit-taunt-target-override-applies");
            Require(first.Rejections.Any(x => x.Contains("taunt-not-explicitly-authorized", StringComparison.Ordinal)), "unauthorized-taunt-rejected"); report.Checks.Add("unauthorized-taunt-rejected");
            var duplicate = svc.Apply(npc, snapshot, first.ThreatLedger, 50);
            Require(!duplicate.Applied && duplicate.Rejections.Contains("duplicate-threat-taunt-tick"), "duplicate-taunt-tick-suppressed"); report.Checks.Add("duplicate-taunt-tick-suppressed");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(svc.Apply(npc, snapshot, ledger, 49)), "deterministic-threat-taunt-replay"); report.Checks.Add("deterministic-threat-taunt-replay");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
