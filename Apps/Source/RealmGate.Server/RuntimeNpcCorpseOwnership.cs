using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcCorpseOwnershipTransferResult(
    bool Succeeded,
    string InstanceId,
    string CorpseId,
    string RoomId,
    int ItemCount,
    long Gold,
    string Reason,
    RuntimeCorpseState? Corpse);

/// <summary>
/// Exact ownership bridge from a dead living-world NPC instance to the item runtime corpse.
/// It transfers only items already owned by the NPC and uses the living-world corpse identity,
/// room, creation time, and expiry supplied by the authoritative lifecycle owner. It does not
/// generate loot, infer gold, alter drop flags, or invent corpse access/decay rules.
/// </summary>
public sealed class RuntimeNpcCorpseOwnershipService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;

    public RuntimeNpcCorpseOwnershipService(RuntimeLivingWorldService living, RuntimeItemEngine items)
    {
        _living = living ?? throw new ArgumentNullException(nameof(living));
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public RuntimeNpcCorpseOwnershipTransferResult TransferDeathInventory(
        string npcInstanceId,
        string corpseId,
        string roomId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc,
        long explicitGold,
        string authority)
    {
        var npc = _living.InspectNpc(npcInstanceId);
        if (npc is null) return Reject(npcInstanceId, corpseId, roomId, "instance-not-found");
        if (npc.Alive) return Reject(npc.InstanceId, corpseId, roomId, "npc-is-still-alive");
        if (string.IsNullOrWhiteSpace(authority)) return Reject(npc.InstanceId, corpseId, roomId, "authority-required");
        if (string.IsNullOrWhiteSpace(corpseId)) return Reject(npc.InstanceId, corpseId, roomId, "corpse-id-required");
        if (!npc.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase)) return Reject(npc.InstanceId, corpseId, roomId, "room-ownership-mismatch");
        if (explicitGold < 0) return Reject(npc.InstanceId, corpseId, roomId, "negative-gold-rejected");
        var before = _items.NpcInventory(npc.InstanceId).Count;
        try
        {
            var decayAfter = expiresAtUtc is null ? (TimeSpan?)null : expiresAtUtc.Value - createdAtUtc;
            var transfer = _items.CreateNpcCorpseExact(corpseId, npc.InstanceId, roomId, explicitGold, createdAtUtc, decayAfter);
            var corpse = _items.Corpses.First(x => x.CorpseId.Equals(transfer.CorpseId, StringComparison.OrdinalIgnoreCase));
            return new(true, npc.InstanceId, corpse.CorpseId, corpse.RoomId, before, corpse.Gold, "transferred", corpse);
        }
        catch (InvalidOperationException ex)
        {
            return Reject(npc.InstanceId, corpseId, roomId, Normalize(ex.Message));
        }
    }

    private static RuntimeNpcCorpseOwnershipTransferResult Reject(string? instanceId, string? corpseId, string? roomId, string reason) =>
        new(false, instanceId ?? string.Empty, corpseId ?? string.Empty, roomId ?? string.Empty, 0, 0, reason, null);

    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcCorpseOwnershipSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcCorpseOwnershipSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcCorpseOwnershipSmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var host = new ServerRuntimeHost(projectRoot, options);
            host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured) ? configured : host.Worlds.Values.First();
            var temp = Path.Combine(Path.GetTempPath(), "reforged-npc-corpse-" + Guid.NewGuid().ToString("N"));
            var living = new RuntimeLivingWorldService(projectRoot, world, temp);
            var npc = living.InspectNpcs(new(IncludeDead: false)).First();
            var catalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id);
            var engine = new RuntimeItemEngine(catalog);
            var definition = catalog.Items.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).First();
            var created = engine.Create(definition.Id, RuntimeItemLocations.Character, "actor.smoke", ownerId: "actor.smoke");
            var inventory = new RuntimeNpcInventoryOwnershipService(living, engine);
            Require(inventory.Give(created.InstanceId, "actor.smoke", npc.InstanceId, npc.RoomId).Succeeded, "seed-npc-inventory");
            var service = new RuntimeNpcCorpseOwnershipService(living, engine);
            var aliveReject = service.TransferDeathInventory(npc.InstanceId, "corpse:" + npc.InstanceId, npc.RoomId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), 0, "smoke");
            Require(!aliveReject.Succeeded && aliveReject.Reason == "npc-is-still-alive", "living-npc-rejected");
            Require(living.ApplyNpcDamage(npc.InstanceId, npc.MaximumHitPoints, DateTimeOffset.UtcNow), "npc-death-admitted");
            var dead = living.InspectNpc(npc.InstanceId)!;
            var transferred = service.TransferDeathInventory(dead.InstanceId, "corpse:" + dead.InstanceId, dead.RoomId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), 0, "smoke");
            Require(transferred.Succeeded && transferred.ItemCount == 1, "exact-death-inventory-transfer");
            Require(engine.NpcInventory(dead.InstanceId).Count == 0, "npc-inventory-cleared");
            Require(engine.Items.Count(x => x.LocationKind == RuntimeItemLocations.Corpse && x.LocationId == transferred.CorpseId) == 1, "corpse-item-owned");
            var duplicate = service.TransferDeathInventory(dead.InstanceId, transferred.CorpseId, dead.RoomId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), 0, "smoke");
            Require(!duplicate.Succeeded && duplicate.Reason == "corpse-already-exists", "duplicate-corpse-rejected");
            report.Checks.AddRange(["seed-npc-inventory", "living-npc-rejected", "npc-death-admitted", "exact-death-inventory-transfer", "npc-inventory-cleared", "corpse-item-owned", "duplicate-corpse-rejected"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
