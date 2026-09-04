using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcSpawnInventoryEntry(string CanonicalItemId, int Quantity);
public sealed record RuntimeNpcSpawnInventoryRequest(string RequestId, string InstanceId, string Authority, IReadOnlyList<RuntimeNpcSpawnInventoryEntry> Entries);
public sealed record RuntimeNpcSpawnInventoryResult(bool Succeeded, string RequestId, string InstanceId, int CreatedCount, string Reason, IReadOnlyList<RuntimeItemInstance> Items);

/// <summary>
/// Exact, non-generative spawn-inventory admission boundary. The caller owns the manifest.
/// This service validates a living NPC, canonical item identifiers, quantities, authority, and
/// replay identity before creating items directly under NPC ownership. It does not infer loadouts,
/// equipment, loot, probability, or respawn replenishment behavior.
/// </summary>
public sealed class RuntimeNpcSpawnInventoryService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;
    private readonly RuntimeItemCatalog _catalog;

    public RuntimeNpcSpawnInventoryService(RuntimeLivingWorldService living, RuntimeItemEngine items, RuntimeItemCatalog catalog)
    {
        _living = living ?? throw new ArgumentNullException(nameof(living));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public RuntimeNpcSpawnInventoryResult Admit(RuntimeNpcSpawnInventoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject(request, "request-id-required");
        if (string.IsNullOrWhiteSpace(request.Authority)) return Reject(request, "authority-required");
        var npc = _living.InspectNpc(request.InstanceId);
        if (npc is null) return Reject(request, "instance-not-found");
        if (!npc.Alive) return Reject(request, "npc-is-not-alive");
        if (request.Entries is null || request.Entries.Count == 0) return Reject(request, "manifest-required");

        var existing = _items.NpcInventory(npc.InstanceId)
            .Where(x => x.Metadata is not null && x.Metadata.TryGetValue("spawn_inventory_request_id", out var id) && id.Equals(request.RequestId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.CanonicalItemId, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        if (existing.Length > 0) return new(true, request.RequestId, npc.InstanceId, 0, "already-applied", existing);

        foreach (var entry in request.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.CanonicalItemId)) return Reject(request, "canonical-item-id-required");
            if (entry.Quantity < 1) return Reject(request, "quantity-must-be-positive");
            try { _catalog.Require(entry.CanonicalItemId); }
            catch (InvalidOperationException) { return Reject(request, "canonical-item-not-found"); }
        }

        var created = new List<RuntimeItemInstance>();
        try
        {
            foreach (var entry in request.Entries.OrderBy(x => x.CanonicalItemId, StringComparer.OrdinalIgnoreCase))
            {
                var metadata = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["spawn_inventory_request_id"] = request.RequestId,
                    ["spawn_inventory_authority"] = request.Authority,
                    ["spawn_inventory_npc_instance_id"] = npc.InstanceId
                };
                created.Add(_items.Create(entry.CanonicalItemId, RuntimeItemLocations.Npc, npc.InstanceId, entry.Quantity, npc.InstanceId, metadata));
            }
            return new(true, request.RequestId, npc.InstanceId, created.Count, "applied", created);
        }
        catch (InvalidOperationException ex)
        {
            foreach (var item in created.AsEnumerable().Reverse())
            {
                try { _items.Remove(item.InstanceId); } catch { }
            }
            return Reject(request, Normalize(ex.Message));
        }
    }

    private static RuntimeNpcSpawnInventoryResult Reject(RuntimeNpcSpawnInventoryRequest request, string reason) =>
        new(false, request.RequestId ?? string.Empty, request.InstanceId ?? string.Empty, 0, reason, Array.Empty<RuntimeItemInstance>());
    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcSpawnInventorySmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcSpawnInventorySmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcSpawnInventorySmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var host = new ServerRuntimeHost(projectRoot, options); host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured) ? configured : host.Worlds.Values.First();
            var living = new RuntimeLivingWorldService(projectRoot, world, Path.Combine(Path.GetTempPath(), "reforged-npc-spawn-inventory-" + Guid.NewGuid().ToString("N")));
            var npc = living.InspectNpcs(new(IncludeDead:false)).First();
            var catalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id);
            var items = new RuntimeItemEngine(catalog);
            var definition = catalog.Items.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).First();
            var service = new RuntimeNpcSpawnInventoryService(living, items, catalog);
            var denied = service.Admit(new("smoke.denied", npc.InstanceId, string.Empty, [new(definition.Id, 1)]));
            Require(!denied.Succeeded && denied.Reason == "authority-required", "authority-enforced");
            var applied = service.Admit(new("smoke.apply", npc.InstanceId, "smoke-host", [new(definition.Id, 1)]));
            Require(applied.Succeeded && applied.CreatedCount == 1 && applied.Items[0].OwnerId == npc.InstanceId, "exact-manifest-applied");
            var replay = service.Admit(new("smoke.apply", npc.InstanceId, "smoke-host", [new(definition.Id, 1)]));
            Require(replay.Succeeded && replay.CreatedCount == 0 && replay.Reason == "already-applied", "request-replay-suppressed");
            report.Checks.AddRange(["authority-enforced","exact-manifest-applied","request-replay-suppressed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
