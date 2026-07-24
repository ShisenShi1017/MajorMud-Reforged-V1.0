using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicMissionRoute(int RootTextblockId, int? OwnerNpcId, string Keyword, int TargetTextblockId);
public sealed record RuntimeClassicDelayOperation(int TextblockId, int Line, int Sequence, string Opcode, int DelayValue, string Raw);
public sealed record RuntimeClassicEventExecutionContract(string Family, int OperationCount, string Owner);
public sealed record RuntimeClassicScheduledNarrativeRequest(string EventKey, int SourceTextblockId, int DelayValue, IReadOnlyList<RuntimeClassicTextblockOperation> Operations, RuntimeClassicNarrativeExecutionContext Context);

public interface IRuntimeClassicEventScheduler
{
    void Schedule(RuntimeClassicScheduledNarrativeRequest request);
}

/// <summary>
/// Canonical Phase F13 authority. Bundle F owns decoded mission/event activation and execution
/// contracts. Bundle G owns the global scheduler and persisted timer queue. No parallel scheduler,
/// mission engine, or event store is created here.
/// </summary>
public sealed class RuntimeClassicEventMissionAuthority
{
    private readonly IReadOnlyDictionary<(int Root, string Keyword), RuntimeClassicMissionRoute> _routes;
    private readonly IReadOnlyDictionary<(int Textblock, int Line, int Sequence), RuntimeClassicDelayOperation> _delays;
    private readonly IReadOnlyDictionary<string, RuntimeClassicEventExecutionContract> _contracts;
    private readonly IRuntimeClassicEventScheduler _scheduler;
    private readonly RuntimeClassicNarrativeExecutionCoordinator _coordinator;

    private RuntimeClassicEventMissionAuthority(
        IReadOnlyDictionary<(int Root, string Keyword), RuntimeClassicMissionRoute> routes,
        IReadOnlyDictionary<(int Textblock, int Line, int Sequence), RuntimeClassicDelayOperation> delays,
        IReadOnlyDictionary<string, RuntimeClassicEventExecutionContract> contracts,
        IRuntimeClassicEventScheduler scheduler,
        RuntimeClassicNarrativeExecutionCoordinator coordinator)
    {
        _routes = routes;
        _delays = delays;
        _contracts = contracts;
        _scheduler = scheduler;
        _coordinator = coordinator;
    }

    public static RuntimeClassicEventMissionAuthority Load(string projectRoot, IRuntimeClassicEventScheduler scheduler, RuntimeClassicNarrativeExecutionCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(coordinator);
        var path = Path.Combine(projectRoot, "Data", "Classic", "event_mission_runtime_authority.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Classic event/mission catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (Text(root, "schema") != "classic-event-mission-runtime-authority.v1") throw new InvalidDataException("Unsupported event/mission authority schema.");
        if (Text(root, "execution_owner") != "Bundle F" || Text(root, "scheduler_owner") != "Bundle G") throw new InvalidDataException("Event/scheduler ownership is invalid.");
        if (!root.GetProperty("fail_closed").GetBoolean()) throw new InvalidDataException("Event/mission runtime must fail closed.");

        var routes = new Dictionary<(int, string), RuntimeClassicMissionRoute>();
        foreach (var row in root.GetProperty("mission_routes").EnumerateArray())
        {
            var route = new RuntimeClassicMissionRoute(Int(row, "root_textblock_id"), NullableInt(row, "owner_npc_id"), Text(row, "keyword").Trim().ToLowerInvariant(), Int(row, "target_textblock_id"));
            if (route.RootTextblockId <= 0 || route.TargetTextblockId <= 0 || string.IsNullOrWhiteSpace(route.Keyword) || !row.GetProperty("target_exists").GetBoolean())
                throw new InvalidDataException("Invalid decoded mission route.");
            if (!routes.TryAdd((route.RootTextblockId, route.Keyword), route)) throw new InvalidDataException($"Duplicate mission route {route.RootTextblockId}:{route.Keyword}.");
        }

        var delays = new Dictionary<(int, int, int), RuntimeClassicDelayOperation>();
        foreach (var row in root.GetProperty("delay_operations").EnumerateArray())
        {
            var delay = new RuntimeClassicDelayOperation(Int(row, "textblock_id"), Int(row, "line"), Int(row, "sequence"), Text(row, "opcode").ToLowerInvariant(), Int(row, "delay_value"), Text(row, "raw"));
            if (delay.TextblockId <= 0 || delay.DelayValue < 0 || delay.Opcode is not ("delay" or "adddelay")) throw new InvalidDataException("Invalid decoded delay operation.");
            if (!delays.TryAdd((delay.TextblockId, delay.Line, delay.Sequence), delay)) throw new InvalidDataException("Duplicate decoded delay operation.");
        }

        var contracts = root.GetProperty("execution_contracts").EnumerateArray()
            .Select(x => new RuntimeClassicEventExecutionContract(Text(x, "family"), Int(x, "operation_count"), Text(x, "owner")))
            .ToDictionary(x => x.Family, StringComparer.OrdinalIgnoreCase);
        if (routes.Count != Int(root, "mission_route_count") || delays.Count != Int(root, "delay_operation_count") || Int(root, "trigger_count") != 2318)
            throw new InvalidDataException("Event/mission authority counts do not match the exhausted decode.");
        return new(routes, delays, contracts, scheduler, coordinator);
    }

    public RuntimeClassicMissionRoute ResolveMissionRoute(int rootTextblockId, string command)
    {
        var key = (rootTextblockId, (command ?? string.Empty).Trim().ToLowerInvariant());
        return _routes.TryGetValue(key, out var route) ? route : throw new KeyNotFoundException($"No decoded mission route for {rootTextblockId}:{key.Item2}.");
    }

    public RuntimeClassicNarrativeExecutionResult ExecuteImmediate(RuntimeClassicTextblockExecutionPlan plan, RuntimeClassicNarrativeExecutionContext context)
    {
        if (plan.Operations.Any(x => x.Opcode is "delay" or "adddelay")) throw new InvalidOperationException("Delayed narrative plans must use ScheduleDelayed.");
        return _coordinator.Execute(plan, context);
    }

    public void ScheduleDelayed(string eventKey, RuntimeClassicTextblockExecutionPlan plan, RuntimeClassicNarrativeExecutionContext context)
    {
        var ordered = plan.Operations.OrderBy(x => x.Sequence).ThenBy(x => x.Line).ToArray();
        var delayOps = ordered.Where(x => x.Opcode is "delay" or "adddelay").ToArray();
        if (delayOps.Length == 0) throw new InvalidOperationException("Decoded delayed event has no delay operation.");
        var totalDelay = 0;
        foreach (var op in delayOps)
        {
            if (!_delays.TryGetValue((plan.MatchedTextblockId, op.Line, op.Sequence), out var decoded)) throw new InvalidOperationException($"Unowned delay operation {plan.MatchedTextblockId}:{op.Line}:{op.Sequence}.");
            checked { totalDelay += decoded.DelayValue; }
        }
        var executable = ordered.Where(x => x.Opcode is not ("delay" or "adddelay")).ToArray();
        _scheduler.Schedule(new RuntimeClassicScheduledNarrativeRequest(eventKey, plan.MatchedTextblockId, totalDelay, executable, context));
    }

    public RuntimeClassicEventExecutionContract RequireContract(string family) => _contracts.TryGetValue(family, out var value) ? value : throw new KeyNotFoundException($"No decoded event execution contract for '{family}'.");

    private static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static int Int(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    private static int? NullableInt(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}

public sealed record RuntimeBundleFPhaseF13AuditReport(bool Passed, int MissionRouteCount, int TriggerCount, int DelayOperationCount, int ExecutionContractCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF13Audit
{
    private static readonly string[] RequiredFamilies = ["ability_mutation", "alignment_change", "currency_grant", "experience", "item_grant", "item_removal", "message_output", "npc_spawn", "random_branch", "scheduler_delay", "spell_application", "teleport", "world_remote_action"];

    public static RuntimeBundleFPhaseF13AuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var path = Path.Combine(projectRoot, "Data", "Classic", "event_mission_runtime_authority.json");
        if (!File.Exists(path)) return new(false, 0, 0, 0, 0, 1, checks, ["event-mission-authority-missing"]);
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        var routes = root.GetProperty("mission_routes").EnumerateArray().ToArray();
        var triggers = root.GetProperty("triggers").EnumerateArray().ToArray();
        var delays = root.GetProperty("delay_operations").EnumerateArray().ToArray();
        var contracts = root.GetProperty("execution_contracts").EnumerateArray().ToArray();
        var routeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes)
        {
            var key = $"{route.GetProperty("root_textblock_id").GetInt32()}:{route.GetProperty("keyword").GetString()}";
            if (!routeKeys.Add(key)) failures.Add($"duplicate-mission-route:{key}");
            if (!route.GetProperty("target_exists").GetBoolean()) failures.Add($"missing-mission-target:{key}");
        }
        var delayKeys = new HashSet<string>();
        foreach (var delay in delays)
        {
            var key = $"{delay.GetProperty("textblock_id").GetInt32()}:{delay.GetProperty("line").GetInt32()}:{delay.GetProperty("sequence").GetInt32()}";
            if (!delayKeys.Add(key)) failures.Add($"duplicate-delay-operation:{key}");
            if (delay.GetProperty("delay_value").GetInt32() < 0) failures.Add($"negative-delay:{key}");
        }
        var familyNames = contracts.Select(x => x.GetProperty("family").GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var family in RequiredFamilies) if (!familyNames.Contains(family)) failures.Add($"missing-execution-contract:{family}");
        Check(routes.Length == 8, "all-work-mission-reconcile-routes-owned");
        Check(routes.Select(x => x.GetProperty("keyword").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[] { "work", "mission", "missions", "reconcile" }), "mission-command-family-complete");
        Check(triggers.Length == 2318, "all-decoded-trigger-rows-owned");
        Check(delays.Length == 276, "all-decoded-delay-operations-owned");
        Check(delays.Count(x => x.GetProperty("opcode").GetString() == "adddelay") == 256, "all-adddelay-operations-owned");
        Check(delays.Count(x => x.GetProperty("opcode").GetString() == "delay") == 20, "all-delay-operations-owned");
        Check(root.GetProperty("scheduler_owner").GetString() == "Bundle G", "scheduler-ownership-not-duplicated");
        Check(root.GetProperty("execution_owner").GetString() == "Bundle F", "event-execution-ownership-canonical");
        Check(root.GetProperty("fail_closed").GetBoolean(), "unresolved-events-fail-closed");
        Check(failures.Count == 0, "zero-phase-f13-disconnects");
        return new(failures.Count == 0, routes.Length, triggers.Length, delays.Length, contracts.Length, failures.Count, checks, failures.Distinct().OrderBy(x => x).ToArray());
    }
}
