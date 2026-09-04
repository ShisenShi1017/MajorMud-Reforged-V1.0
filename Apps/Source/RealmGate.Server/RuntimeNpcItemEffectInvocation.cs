using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcItemEffectAdmission(
    bool Admitted,
    string InstanceId,
    string ItemInstanceId,
    string TargetId,
    string? RequestId,
    string Reason);

public sealed record RuntimeNpcItemEffectCompletion(
    bool Succeeded,
    string InstanceId,
    string ItemInstanceId,
    string RequestId,
    bool EffectApplied,
    string Reason,
    RuntimeItemInstance? Item);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcItemEffectAdmission AdmitNpcItemEffect(
        string instanceId,
        string itemInstanceId,
        string targetId,
        string authority,
        DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current))
                return new(false, instanceId ?? string.Empty, itemInstanceId ?? string.Empty, targetId ?? string.Empty, null, "instance-not-found");
            if (!current.Alive)
                return new(false, current.InstanceId, itemInstanceId ?? string.Empty, targetId ?? string.Empty, null, "npc-is-not-alive");
            if (string.IsNullOrWhiteSpace(itemInstanceId))
                return new(false, current.InstanceId, string.Empty, targetId ?? string.Empty, null, "item-instance-id-required");
            if (string.IsNullOrWhiteSpace(targetId))
                return new(false, current.InstanceId, itemInstanceId, string.Empty, null, "target-id-required");
            if (string.IsNullOrWhiteSpace(authority))
                return new(false, current.InstanceId, itemInstanceId, targetId, null, "authority-required");
            if (current.Metadata.TryGetValue("npc_item_effect_active_request_id", out var active) && !string.IsNullOrWhiteSpace(active))
                return new(false, current.InstanceId, itemInstanceId, targetId, active, "item-effect-already-active");

            var generation = ReadNpcItemEffectGeneration(current.Metadata) + 1;
            var requestId = $"{current.InstanceId}:item-effect:{generation.ToString(CultureInfo.InvariantCulture)}";
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_item_effect_generation"] = generation.ToString(CultureInfo.InvariantCulture),
                ["npc_item_effect_active_request_id"] = requestId,
                ["npc_item_effect_item_instance_id"] = itemInstanceId.Trim(),
                ["npc_item_effect_target_id"] = targetId.Trim(),
                ["npc_item_effect_authority"] = authority.Trim(),
                ["npc_item_effect_admitted_at_utc"] = nowUtc.ToString("O")
            };
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return new(true, current.InstanceId, itemInstanceId.Trim(), targetId.Trim(), requestId, "admitted");
        }
    }

    public bool CompleteNpcItemEffect(
        string instanceId,
        string requestId,
        bool effectApplied,
        string outcome,
        DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            if (!current.Metadata.TryGetValue("npc_item_effect_active_request_id", out var active) ||
                !active.Equals(requestId, StringComparison.Ordinal)) return false;
            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["npc_item_effect_last_request_id"] = requestId,
                ["npc_item_effect_last_applied"] = effectApplied ? "true" : "false",
                ["npc_item_effect_last_outcome"] = string.IsNullOrWhiteSpace(outcome) ? (effectApplied ? "applied" : "not-applied") : outcome.Trim(),
                ["npc_item_effect_completed_at_utc"] = nowUtc.ToString("O")
            };
            foreach (var key in new[] { "npc_item_effect_active_request_id", "npc_item_effect_item_instance_id", "npc_item_effect_target_id", "npc_item_effect_authority", "npc_item_effect_admitted_at_utc" })
                metadata.Remove(key);
            _npcs[current.InstanceId] = current with { Metadata = metadata };
            Save();
            return true;
        }
    }

    private static long ReadNpcItemEffectGeneration(IReadOnlyDictionary<string,string> metadata) =>
        metadata.TryGetValue("npc_item_effect_generation", out var raw) &&
        long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : 0;
}

/// <summary>Two-phase NPC item-effect handoff. The effect-owning subsystem must explicitly complete the admitted request. Item charges or quantity are consumed only after exact successful completion.</summary>
public sealed class RuntimeNpcItemEffectService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;
    public RuntimeNpcItemEffectService(RuntimeLivingWorldService living, RuntimeItemEngine items) { _living = living; _items = items; }

    public RuntimeNpcItemEffectAdmission Admit(string instanceId, string itemInstanceId, string targetId, string authority, DateTimeOffset nowUtc)
    {
        var npc = _living.InspectNpc(instanceId);
        if (npc is null) return new(false, instanceId ?? string.Empty, itemInstanceId ?? string.Empty, targetId ?? string.Empty, null, "instance-not-found");
        try
        {
            var item = _items.RequireInstance(itemInstanceId);
            if (!string.Equals(item.OwnerId, npc.InstanceId, StringComparison.OrdinalIgnoreCase) ||
                (!item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) &&
                 !item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
                return new(false, npc.InstanceId, itemInstanceId, targetId ?? string.Empty, null, "item-is-not-owned-by-npc");
        }
        catch (InvalidOperationException ex)
        {
            return new(false, npc.InstanceId, itemInstanceId ?? string.Empty, targetId ?? string.Empty, null, Normalize(ex.Message));
        }
        return _living.AdmitNpcItemEffect(instanceId, itemInstanceId, targetId, authority, nowUtc);
    }

    public RuntimeNpcItemEffectCompletion Complete(string instanceId, string requestId, bool effectApplied, string outcome, DateTimeOffset nowUtc)
    {
        var npc = _living.InspectNpc(instanceId);
        if (npc is null) return new(false, instanceId ?? string.Empty, string.Empty, requestId ?? string.Empty, effectApplied, "instance-not-found", null);
        if (!npc.Metadata.TryGetValue("npc_item_effect_active_request_id", out var active) || !active.Equals(requestId, StringComparison.Ordinal))
            return new(false, npc.InstanceId, string.Empty, requestId ?? string.Empty, effectApplied, "request-id-mismatch", null);
        var itemId = npc.Metadata.TryGetValue("npc_item_effect_item_instance_id", out var itemValue) ? itemValue : string.Empty;
        RuntimeItemInstance? item = null;
        if (effectApplied)
        {
            try { item = _items.UseNpc(itemId, npc.InstanceId); }
            catch (InvalidOperationException ex) { return new(false, npc.InstanceId, itemId, requestId, true, Normalize(ex.Message), null); }
        }
        if (!_living.CompleteNpcItemEffect(instanceId, requestId, effectApplied, outcome, nowUtc))
            return new(false, npc.InstanceId, itemId, requestId, effectApplied, "completion-race-rejected", item);
        return new(true, npc.InstanceId, itemId, requestId, effectApplied, effectApplied ? "effect-applied-and-item-consumed" : "effect-not-applied-item-preserved", item);
    }

    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcItemEffectSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcItemEffectSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcItemEffectSmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot); var host = new ServerRuntimeHost(projectRoot, options); host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var value) ? value : host.Worlds.Values.First();
            var living = new RuntimeLivingWorldService(projectRoot, world, Path.Combine(Path.GetTempPath(), "reforged-npc-item-effect-" + Guid.NewGuid().ToString("N")));
            var npc = living.InspectNpcs(new(IncludeDead:false)).First(); var catalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id); var items = new RuntimeItemEngine(catalog);
            var def = catalog.Items.First(x => x.Consumable || x.DefaultCharges > 0);
            var owned = items.Create(def.Id, RuntimeItemLocations.Npc, npc.InstanceId, def.Stackable ? 2 : 1, npc.InstanceId);
            var service = new RuntimeNpcItemEffectService(living, items);
            Require(!service.Admit(npc.InstanceId, owned.InstanceId, npc.InstanceId, string.Empty, DateTimeOffset.UtcNow).Admitted, "authority-enforced");
            var admission = service.Admit(npc.InstanceId, owned.InstanceId, npc.InstanceId, "smoke-effect-owner", DateTimeOffset.UtcNow);
            Require(admission.Admitted && admission.RequestId is not null, "exact-item-effect-admitted");
            Require(!service.Complete(npc.InstanceId, admission.RequestId + ":stale", true, "applied", DateTimeOffset.UtcNow).Succeeded, "stale-completion-rejected");
            Require(service.Complete(npc.InstanceId, admission.RequestId!, false, "executor-rejected", DateTimeOffset.UtcNow).Succeeded, "failed-effect-preserves-item");
            var second = service.Admit(npc.InstanceId, owned.InstanceId, npc.InstanceId, "smoke-effect-owner", DateTimeOffset.UtcNow);
            Require(service.Complete(npc.InstanceId, second.RequestId!, true, "applied", DateTimeOffset.UtcNow).Succeeded, "successful-effect-consumes-item");
            report.Checks.AddRange(["authority-enforced","exact-item-effect-admitted","stale-completion-rejected","failed-effect-preserves-item","successful-effect-consumes-item"]);
        }
        catch(Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
