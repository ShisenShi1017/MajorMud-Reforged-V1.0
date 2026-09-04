using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcServiceAdmission(
    bool Admitted,
    string InstanceId,
    string? RequestId,
    string ActorId,
    string RoomId,
    string ServiceKind,
    string Reason);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcServiceAdmission AdmitNpcService(string instanceId, string actorId, string roomId, string serviceKind, string authority, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return new(false, instanceId ?? string.Empty, null, actorId ?? string.Empty, roomId ?? string.Empty, serviceKind ?? string.Empty, "instance-not-found");
            if (!current.Alive)
                return new(false, current.InstanceId, null, actorId ?? string.Empty, roomId ?? string.Empty, serviceKind ?? string.Empty, "npc-is-not-alive");
            if (string.IsNullOrWhiteSpace(actorId))
                return new(false, current.InstanceId, null, string.Empty, roomId ?? string.Empty, serviceKind ?? string.Empty, "actor-id-required");
            if (string.IsNullOrWhiteSpace(serviceKind))
                return new(false, current.InstanceId, null, actorId, roomId ?? string.Empty, string.Empty, "service-kind-required");
            if (!current.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase))
                return new(false, current.InstanceId, null, actorId, roomId ?? string.Empty, serviceKind, "room-ownership-mismatch");
            if (!SupportsService(current.InteractionModel, serviceKind))
                return new(false, current.InstanceId, null, actorId, roomId, serviceKind, "service-kind-not-supported-by-npc");
            if (current.Metadata.TryGetValue("npc_service_active_request_id", out var active) && !string.IsNullOrWhiteSpace(active))
                return new(false, current.InstanceId, active, actorId, roomId, serviceKind, "service-already-active");

            var generation = ReadServiceGeneration(current.Metadata) + 1;
            var requestId = $"{current.InstanceId}:service:{generation.ToString(CultureInfo.InvariantCulture)}";
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_service_generation"] = generation.ToString(CultureInfo.InvariantCulture),
                ["npc_service_active_request_id"] = requestId,
                ["npc_service_actor_id"] = actorId.Trim(),
                ["npc_service_room_id"] = roomId,
                ["npc_service_kind"] = serviceKind.Trim().ToLowerInvariant(),
                ["npc_service_authority"] = string.IsNullOrWhiteSpace(authority) ? "host-service-command" : authority.Trim(),
                ["npc_service_admitted_at_utc"] = nowUtc.ToString("O")
            };
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, requestId, actorId.Trim(), roomId, serviceKind.Trim().ToLowerInvariant(), "admitted");
        }
    }

    public bool CompleteNpcService(string instanceId, string requestId, bool succeeded, string? outcome, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            if (!current.Metadata.TryGetValue("npc_service_active_request_id", out var active) ||
                !active.Equals(requestId, StringComparison.Ordinal)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_service_last_request_id"] = requestId,
                ["npc_service_last_succeeded"] = succeeded ? "true" : "false",
                ["npc_service_last_outcome"] = string.IsNullOrWhiteSpace(outcome) ? (succeeded ? "completed" : "failed") : outcome.Trim(),
                ["npc_service_completed_at_utc"] = nowUtc.ToString("O")
            };
            foreach (var key in new[] { "npc_service_active_request_id", "npc_service_actor_id", "npc_service_room_id", "npc_service_kind", "npc_service_authority", "npc_service_admitted_at_utc" }) metadata.Remove(key);
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    private static bool SupportsService(string interactionModel, string serviceKind)
    {
        var model = interactionModel ?? string.Empty;
        var kind = serviceKind.Trim().ToLowerInvariant();
        if (kind == "train") return model.Contains("trainer", StringComparison.OrdinalIgnoreCase);
        if (kind is "buy" or "sell" or "repair")
            return model.Contains("service", StringComparison.OrdinalIgnoreCase) || model.Contains("shop", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static long ReadServiceGeneration(IReadOnlyDictionary<string,string> metadata) =>
        metadata.TryGetValue("npc_service_generation", out var raw) && long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : 0;
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private RuntimeLivingNpcState ResolveServiceNpc(RuntimeSession session, string serviceKind)
    {
        var npcs = LivingNpcsInRoom(session).Where(npc => npc.Alive).Where(npc =>
            serviceKind.Equals("train", StringComparison.OrdinalIgnoreCase)
                ? npc.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase)
                : npc.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) || npc.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase)).ToArray();
        return npcs.Length switch
        {
            1 => npcs[0],
            0 => throw new InvalidOperationException($"There is no living NPC here that owns the {serviceKind} service."),
            _ => npcs.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).First()
        };
    }
}

public sealed class RuntimeNpcServiceAdmissionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcServiceAdmissionSmokeReport Run()
    {
        var report = new RuntimeNpcServiceAdmissionSmokeReport();
        try
        {
            var type = typeof(RuntimeLivingWorldService);
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.AdmitNpcService)) is not null, "service-admission-exposed");
            Require(type.GetMethod(nameof(RuntimeLivingWorldService.CompleteNpcService)) is not null, "service-completion-exposed");
            report.Checks.AddRange(["service-admission-exposed", "service-completion-exposed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
