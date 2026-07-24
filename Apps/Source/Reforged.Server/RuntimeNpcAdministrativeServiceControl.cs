using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcAdministrativeServiceControlResult(
    bool Applied,
    string InstanceId,
    string Operation,
    string Reason,
    IReadOnlyList<string> ClearedKeys);

public sealed partial class RuntimeLivingWorldService
{
    private static readonly string[] AdministrativeServiceTransientKeys =
    [
        "npc_service_active_request_id", "npc_service_actor_id", "npc_service_room_id", "npc_service_kind", "npc_service_authority", "npc_service_admitted_at_utc",
        "npc_dialogue_active_request_id", "npc_dialogue_actor_id", "npc_dialogue_room_id", "npc_dialogue_textblock_id", "npc_dialogue_authority", "npc_dialogue_admitted_at_utc",
        "npc_item_effect_active_request_id", "npc_item_effect_item_instance_id", "npc_item_effect_target_id", "npc_item_effect_authority", "npc_item_effect_admitted_at_utc",
        "npc_equipment_selection_active_request_id", "npc_equipment_selection_item_instance_id", "npc_equipment_selection_slot", "npc_equipment_selection_authority", "npc_equipment_selection_admitted_at_utc"
    ];

    public RuntimeNpcAdministrativeServiceControlResult CancelNpcTransientInteraction(
        string instanceId,
        string operation,
        string authority,
        string reason,
        DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return new(false, instanceId ?? string.Empty, operation ?? string.Empty, "instance-not-found", []);
            if (string.IsNullOrWhiteSpace(authority))
                return new(false, current.InstanceId, operation ?? string.Empty, "authority-required", []);
            if (string.IsNullOrWhiteSpace(reason))
                return new(false, current.InstanceId, operation ?? string.Empty, "reason-required", []);

            var normalized = (operation ?? string.Empty).Trim().ToLowerInvariant();
            var prefixes = normalized switch
            {
                "service" => new[] { "npc_service_" },
                "dialogue" => new[] { "npc_dialogue_" },
                "item-effect" => new[] { "npc_item_effect_" },
                "equipment-selection" => new[] { "npc_equipment_selection_" },
                "all" => new[] { "npc_service_", "npc_dialogue_", "npc_item_effect_", "npc_equipment_selection_" },
                _ => []
            };
            if (prefixes.Length == 0)
                return new(false, current.InstanceId, normalized, "unsupported-operation", []);

            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            var cleared = AdministrativeServiceTransientKeys
                .Where(key => prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) && metadata.Remove(key))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (cleared.Length == 0)
                return new(false, current.InstanceId, normalized, "no-active-transient-state", []);

            metadata["npc_admin_interaction_last_operation"] = normalized;
            metadata["npc_admin_interaction_last_reason"] = reason.Trim();
            metadata["npc_admin_interaction_last_authority"] = authority.Trim();
            metadata["npc_admin_interaction_last_at_utc"] = nowUtc.ToString("O");
            metadata["npc_admin_interaction_last_cleared_keys"] = JsonSerializer.Serialize(cleared);
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, normalized, "cancelled", cleared);
        }
    }
}

public sealed class RuntimeNpcAdministrativeServiceControlSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcAdministrativeServiceControlSmokeReport Run()
    {
        var report = new RuntimeNpcAdministrativeServiceControlSmokeReport();
        try
        {
            if (typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.CancelNpcTransientInteraction)) is null)
                throw new InvalidOperationException("administrative-service-control-not-exposed");
            report.Checks.Add("administrative-service-control-exposed");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
