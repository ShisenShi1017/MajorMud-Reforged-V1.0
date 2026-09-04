using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcInspectionSnapshot(
    string WorldId,
    string InstanceId,
    string SpawnId,
    string NpcId,
    string Name,
    string RoomId,
    string HomeRoomId,
    int CurrentHitPoints,
    int MaximumHitPoints,
    bool Alive,
    DateTimeOffset? RespawnAtUtc,
    string InteractionModel,
    int RespawnMinutes,
    IReadOnlyDictionary<string,string> Metadata,
    IReadOnlyDictionary<string,long> ThreatLedger,
    string? CurrentTargetId,
    string? PendingActionId,
    string? ActionClaimConsumer,
    string? ActionClaimId,
    long? LastAiTick,
    bool RespawnBlocked,
    bool RespawnSuppressed,
    bool Fleeing,
    bool ReturningHome);

public sealed record RuntimeNpcInspectionQuery(
    string? InstanceId = null,
    string? NpcId = null,
    string? SpawnId = null,
    string? RoomId = null,
    bool IncludeDead = true);

public sealed partial class RuntimeLivingWorldService
{
    public IReadOnlyList<RuntimeNpcInspectionSnapshot> InspectNpcs(RuntimeNpcInspectionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            return _npcs.Values
                .Where(x => query.IncludeDead || x.Alive)
                .Where(x => string.IsNullOrWhiteSpace(query.InstanceId) || x.InstanceId.Equals(query.InstanceId, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(query.NpcId) || x.NpcId.Equals(query.NpcId, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(query.SpawnId) || x.SpawnId.Equals(query.SpawnId, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(query.RoomId) || x.RoomId.Equals(query.RoomId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(ToInspectionSnapshot)
                .ToArray();
        }
    }

    public RuntimeNpcInspectionSnapshot? InspectNpc(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        lock (_gate) return _npcs.TryGetValue(instanceId, out var value) ? ToInspectionSnapshot(value) : null;
    }

    private RuntimeNpcInspectionSnapshot ToInspectionSnapshot(RuntimeLivingNpcState value)
    {
        var metadata = new Dictionary<string,string>(value.Metadata, StringComparer.OrdinalIgnoreCase);
        var threat = ParseInspectionThreatLedger(metadata);
        metadata.TryGetValue("current_target_id", out var currentTarget);
        metadata.TryGetValue("pending_ai_action_id", out var pendingAction);
        metadata.TryGetValue("pending_ai_action_claim_consumer", out var claimConsumer);
        metadata.TryGetValue("pending_ai_action_claim_id", out var claimId);
        long? lastAiTick = metadata.TryGetValue("last_ai_tick", out var tickRaw) && long.TryParse(tickRaw, out var tick) ? tick : null;
        return new RuntimeNpcInspectionSnapshot(
            WorldId, value.InstanceId, value.SpawnId, value.NpcId, value.Name, value.RoomId, value.HomeRoomId,
            value.CurrentHitPoints, value.MaximumHitPoints, value.Alive, value.RespawnAtUtc,
            value.InteractionModel, value.RespawnMinutes, metadata, threat,
            EmptyToNull(currentTarget), EmptyToNull(pendingAction), EmptyToNull(claimConsumer), EmptyToNull(claimId), lastAiTick,
            metadata.ContainsKey("npc_respawn_block_reason"),
            metadata.ContainsKey("npc_respawn_suppressed_at_utc"),
            metadata.TryGetValue("npc_fleeing", out var fleeing) && fleeing.Equals("true", StringComparison.OrdinalIgnoreCase),
            metadata.TryGetValue("npc_returning_home", out var returning) && returning.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string,long> ParseInspectionThreatLedger(IReadOnlyDictionary<string,string> metadata)
    {
        if (!metadata.TryGetValue("npc_explicit_threat_ledger_json", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new SortedDictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var decoded = JsonSerializer.Deserialize<Dictionary<string,long>>(raw) ?? new();
            return new SortedDictionary<string,long>(decoded, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new SortedDictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class RuntimeNpcInspectionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNpcInspectionSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcInspectionSmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var host = new ServerRuntimeHost(projectRoot, options);
            host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured) ? configured : host.Worlds.Values.First();
            var temp = Path.Combine(Path.GetTempPath(), "reforged-npc-inspection-" + Guid.NewGuid().ToString("N"));
            var living = new RuntimeLivingWorldService(projectRoot, world, temp);
            var all = living.InspectNpcs(new());
            Require(all.Count == living.NpcInstanceCount, "all-runtime-npcs-visible");
            report.Checks.Add("all-runtime-npcs-visible");
            Require(all.SequenceEqual(all.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)), "stable-instance-order");
            report.Checks.Add("stable-instance-order");
            if (all.Count > 0)
            {
                var exact = living.InspectNpc(all[0].InstanceId);
                Require(exact is not null && exact.InstanceId == all[0].InstanceId, "exact-instance-inspection");
                report.Checks.Add("exact-instance-inspection");
                var byRoom = living.InspectNpcs(new(RoomId: all[0].RoomId));
                Require(byRoom.All(x => x.RoomId.Equals(all[0].RoomId, StringComparison.OrdinalIgnoreCase)), "room-filter-inspection");
                report.Checks.Add("room-filter-inspection");
            }
            Require(living.InspectNpc("missing-instance") is null, "unknown-instance-safe-null");
            report.Checks.Add("unknown-instance-safe-null");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
