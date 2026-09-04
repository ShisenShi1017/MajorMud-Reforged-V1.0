using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcAdministrationResult(
    bool Applied,
    string InstanceId,
    string Operation,
    string Reason,
    string? PreviousRoomId,
    string? CurrentRoomId,
    bool AiEnabled,
    bool Frozen);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcAdministrationResult SetNpcAiEnabled(string instanceId, bool enabled, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return Missing(instanceId, "set-ai-enabled");
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_ai_enabled"] = enabled ? "true" : "false",
                ["npc_ai_control_authority"] = NormalizeAuthority(authority),
                ["npc_ai_control_updated_at_utc"] = nowUtc.ToString("O")
            };
            if (!enabled)
            {
                ClearTransientNpcExecutionMetadata(metadata);
                metadata.Remove("current_target_id");
                metadata.Remove("interaction_request");
                metadata["combat_hostility_state"] = current.Alive ? "disabled" : "dead";
            }
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return Result(_npcs[instanceId], "set-ai-enabled", enabled ? "enabled" : "disabled", current.RoomId);
        }
    }

    public RuntimeNpcAdministrationResult SetNpcFrozen(string instanceId, bool frozen, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return Missing(instanceId, "set-frozen");
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_frozen"] = frozen ? "true" : "false",
                ["npc_freeze_authority"] = NormalizeAuthority(authority),
                ["npc_freeze_updated_at_utc"] = nowUtc.ToString("O")
            };
            if (frozen)
            {
                metadata.Remove("npc_fleeing");
                metadata.Remove("npc_flee_destination_room_id");
                metadata.Remove("npc_returning_home");
                metadata.Remove("npc_return_home_destination_room_id");
            }
            _npcs[instanceId] = current with { Metadata = metadata };
            Save();
            return Result(_npcs[instanceId], "set-frozen", frozen ? "frozen" : "unfrozen", current.RoomId);
        }
    }

    public RuntimeNpcAdministrationResult MoveNpc(string instanceId, string destinationRoomId, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return Missing(instanceId, "move");
            if (string.IsNullOrWhiteSpace(destinationRoomId) || _world.FindRoomById(destinationRoomId.Trim()) is null)
                return Result(current, "move", "destination-room-is-not-canonical", current.RoomId, false);
            if (!current.Alive)
                return Result(current, "move", "dead-instance-cannot-be-moved", current.RoomId, false);
            var destination = destinationRoomId.Trim();
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_admin_move_authority"] = NormalizeAuthority(authority),
                ["npc_admin_move_at_utc"] = nowUtc.ToString("O"),
                ["npc_admin_move_origin_room_id"] = current.RoomId,
                ["npc_admin_move_destination_room_id"] = destination
            };
            metadata.Remove("npc_fleeing");
            metadata.Remove("npc_flee_destination_room_id");
            metadata.Remove("npc_returning_home");
            metadata.Remove("npc_return_home_destination_room_id");
            _npcs[instanceId] = current with { RoomId = destination, Metadata = metadata };
            PublishLivingWorldEventLocked("npc-admin-moved", instanceId, destination, nowUtc);
            Save();
            return Result(_npcs[instanceId], "move", "moved", current.RoomId);
        }
    }

    private static string NormalizeAuthority(string authority) => string.IsNullOrWhiteSpace(authority) ? "host-administrator" : authority.Trim();
    private RuntimeNpcAdministrationResult Missing(string instanceId, string operation) =>
        new(false, instanceId ?? string.Empty, operation, "instance-not-found", null, null, false, false);
    private static RuntimeNpcAdministrationResult Result(RuntimeLivingNpcState state, string operation, string reason, string? previousRoom, bool applied = true) =>
        new(applied, state.InstanceId, operation, reason, previousRoom, state.RoomId,
            !state.Metadata.TryGetValue("npc_ai_enabled", out var ai) || !ai.Equals("false", StringComparison.OrdinalIgnoreCase),
            state.Metadata.TryGetValue("npc_frozen", out var frozen) && frozen.Equals("true", StringComparison.OrdinalIgnoreCase));
}

public sealed class RuntimeNpcAdministrationSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcAdministrationSmokeReport Run()
    {
        var report = new RuntimeNpcAdministrationSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.SetNpcAiEnabled)) is not null, "ai-control-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.SetNpcFrozen)) is not null, "freeze-control-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.MoveNpc)) is not null, "move-control-exposed");
            report.Checks.AddRange(["ai-control-exposed","freeze-control-exposed","move-control-exposed"]);
            var engine = new RuntimeNpcAiDecisionEngine();
            var npc = new RuntimeNpcDefinition { Id="npc.test", Name="Test", HitPoints=10, AttackCount=1 };
            var disabled = engine.Decide(new("i", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 1, "target", null, null, null,
                new Dictionary<string,string>{{"npc_ai_enabled","false"}}));
            Require(disabled.SelectedAction == RuntimeNpcDecisionKind.Disabled, "disabled-ai-blocks-decision");
            var frozen = engine.Decide(new("i", npc, RuntimeNpcLifeState.Spawned, "map1-room1", 2, "target", null, null, null,
                new Dictionary<string,string>{{"npc_frozen","true"}}));
            Require(frozen.SelectedAction == RuntimeNpcDecisionKind.Frozen, "frozen-npc-holds-state");
            report.Checks.AddRange(["disabled-ai-blocks-decision","frozen-npc-holds-state"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions{WriteIndented=true});
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
