using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcInventorySnapshot(
    string WorldId,
    string InstanceId,
    string NpcId,
    string RoomId,
    bool Alive,
    IReadOnlyList<RuntimeItemInstance> Items);

public sealed record RuntimeNpcInventoryTransferResult(
    bool Succeeded,
    string InstanceId,
    string ItemInstanceId,
    string Operation,
    string Reason,
    RuntimeItemInstance? Item);

/// <summary>
/// Evidence-backed ownership boundary between the living NPC registry and the item runtime.
/// This service does not invent spawn inventory, equipment choices, loot tables, or item-use AI.
/// It only admits exact transfers whose NPC instance, room, life state, item owner, and destination
/// are already authoritative in the supplied runtime services.
/// </summary>
public sealed class RuntimeNpcInventoryOwnershipService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;

    public RuntimeNpcInventoryOwnershipService(RuntimeLivingWorldService living, RuntimeItemEngine items)
    {
        _living = living ?? throw new ArgumentNullException(nameof(living));
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public RuntimeNpcInventorySnapshot? Inspect(string instanceId)
    {
        var npc = _living.InspectNpc(instanceId);
        if (npc is null) return null;
        var items = _items.NpcInventory(instanceId)
            .OrderBy(x => x.CanonicalItemId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(_living.WorldId, npc.InstanceId, npc.NpcId, npc.RoomId, npc.Alive, items);
    }

    public RuntimeNpcInventoryTransferResult Give(string itemInstanceId, string fromOwnerId, string npcInstanceId, string actorRoomId)
    {
        var npc = _living.InspectNpc(npcInstanceId);
        if (npc is null) return Reject(npcInstanceId, itemInstanceId, "give", "instance-not-found");
        if (!npc.Alive) return Reject(npc.InstanceId, itemInstanceId, "give", "npc-is-not-alive");
        if (!npc.RoomId.Equals(actorRoomId, StringComparison.OrdinalIgnoreCase)) return Reject(npc.InstanceId, itemInstanceId, "give", "room-ownership-mismatch");
        if (string.IsNullOrWhiteSpace(fromOwnerId)) return Reject(npc.InstanceId, itemInstanceId, "give", "source-owner-required");
        try
        {
            var moved = _items.MoveToNpc(itemInstanceId, fromOwnerId, npc.InstanceId);
            return new(true, npc.InstanceId, moved.InstanceId, "give", "transferred", moved);
        }
        catch (InvalidOperationException ex)
        {
            return Reject(npc.InstanceId, itemInstanceId, "give", Normalize(ex.Message));
        }
    }

    public RuntimeNpcInventoryTransferResult ReturnToOwner(string itemInstanceId, string npcInstanceId, string destinationOwnerId, string authority)
    {
        var npc = _living.InspectNpc(npcInstanceId);
        if (npc is null) return Reject(npcInstanceId, itemInstanceId, "return", "instance-not-found");
        if (string.IsNullOrWhiteSpace(destinationOwnerId)) return Reject(npc.InstanceId, itemInstanceId, "return", "destination-owner-required");
        if (string.IsNullOrWhiteSpace(authority)) return Reject(npc.InstanceId, itemInstanceId, "return", "authority-required");
        try
        {
            var moved = _items.MoveFromNpc(itemInstanceId, npc.InstanceId, destinationOwnerId);
            return new(true, npc.InstanceId, moved.InstanceId, "return", "transferred", moved);
        }
        catch (InvalidOperationException ex)
        {
            return Reject(npc.InstanceId, itemInstanceId, "return", Normalize(ex.Message));
        }
    }

    public RuntimeNpcInventoryTransferResult DropToCurrentRoom(string itemInstanceId, string npcInstanceId, string authority)
    {
        var npc = _living.InspectNpc(npcInstanceId);
        if (npc is null) return Reject(npcInstanceId, itemInstanceId, "drop", "instance-not-found");
        if (string.IsNullOrWhiteSpace(authority)) return Reject(npc.InstanceId, itemInstanceId, "drop", "authority-required");
        try
        {
            var moved = _items.DropFromNpc(itemInstanceId, npc.InstanceId, npc.RoomId);
            return new(true, npc.InstanceId, moved.InstanceId, "drop", "transferred", moved);
        }
        catch (InvalidOperationException ex)
        {
            return Reject(npc.InstanceId, itemInstanceId, "drop", Normalize(ex.Message));
        }
    }

    private static RuntimeNpcInventoryTransferResult Reject(string? npcId, string? itemId, string operation, string reason) =>
        new(false, npcId ?? string.Empty, itemId ?? string.Empty, operation, reason, null);

    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcInventoryOwnershipSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcInventoryOwnershipSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcInventoryOwnershipSmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var host = new ServerRuntimeHost(projectRoot, options);
            host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured) ? configured : host.Worlds.Values.First();
            var temp = Path.Combine(Path.GetTempPath(), "reforged-npc-inventory-" + Guid.NewGuid().ToString("N"));
            var living = new RuntimeLivingWorldService(projectRoot, world, temp);
            var npc = living.InspectNpcs(new(IncludeDead: false)).First();
            var catalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id);
            var engine = new RuntimeItemEngine(catalog);
            var definition = catalog.Items.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).First();
            var created = engine.Create(definition.Id, RuntimeItemLocations.Character, "actor.smoke", ownerId: "actor.smoke");
            var service = new RuntimeNpcInventoryOwnershipService(living, engine);

            var wrongRoom = service.Give(created.InstanceId, "actor.smoke", npc.InstanceId, "room.invalid");
            Require(!wrongRoom.Succeeded && wrongRoom.Reason == "room-ownership-mismatch", "room-ownership-enforced");
            var given = service.Give(created.InstanceId, "actor.smoke", npc.InstanceId, npc.RoomId);
            Require(given.Succeeded && given.Item?.LocationKind == RuntimeItemLocations.Npc && given.Item.OwnerId == npc.InstanceId, "exact-give-to-npc");
            var inventory = service.Inspect(npc.InstanceId);
            Require(inventory is not null && inventory.Items.Count == 1 && inventory.Items[0].InstanceId == created.InstanceId, "stable-npc-inventory-inspection");
            var unauthorized = service.ReturnToOwner(created.InstanceId, npc.InstanceId, "actor.smoke", string.Empty);
            Require(!unauthorized.Succeeded && unauthorized.Reason == "authority-required", "return-authority-enforced");
            var returned = service.ReturnToOwner(created.InstanceId, npc.InstanceId, "actor.smoke", "smoke-owner-return");
            Require(returned.Succeeded && returned.Item?.LocationKind == RuntimeItemLocations.Character && returned.Item.OwnerId == "actor.smoke", "exact-return-from-npc");
            Require(service.Inspect("missing-instance") is null, "unknown-npc-safe-null");
            report.Checks.AddRange(["room-ownership-enforced", "exact-give-to-npc", "stable-npc-inventory-inspection", "return-authority-enforced", "exact-return-from-npc", "unknown-npc-safe-null"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
