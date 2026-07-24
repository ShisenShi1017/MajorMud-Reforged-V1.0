using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcCorpseAccessResult(bool Succeeded, string CorpseId, string CharacterId, string RoomId, int ItemCount, long Gold, string Reason);

/// <summary>Explicit host-authorized access and cleanup boundary for already materialized NPC corpses. It does not infer native loot rights or decay duration.</summary>
public sealed class RuntimeNpcCorpseAccessService
{
    private readonly RuntimeItemEngine _items;
    public RuntimeNpcCorpseAccessService(RuntimeItemEngine items) => _items = items ?? throw new ArgumentNullException(nameof(items));

    public IReadOnlyList<RuntimeItemInstance> Inspect(string corpseId) => _items.CorpseInventory(corpseId);

    public RuntimeNpcCorpseAccessResult LootItem(string corpseId, string itemInstanceId, string characterId, string actorRoomId, bool accessAuthorized, string authority)
    {
        var corpse = _items.Corpses.FirstOrDefault(x => x.CorpseId.Equals(corpseId, StringComparison.OrdinalIgnoreCase) && !x.CleanedUp);
        if (corpse is null) return Reject(corpseId, characterId, actorRoomId, "corpse-not-found");
        if (!accessAuthorized || string.IsNullOrWhiteSpace(authority)) return Reject(corpseId, characterId, actorRoomId, "access-authority-required");
        if (!corpse.RoomId.Equals(actorRoomId, StringComparison.OrdinalIgnoreCase)) return Reject(corpseId, characterId, actorRoomId, "room-ownership-mismatch");
        if (string.IsNullOrWhiteSpace(characterId)) return Reject(corpseId, characterId, actorRoomId, "character-id-required");
        try { _items.Loot(itemInstanceId, corpseId, characterId); return Snapshot(corpseId, characterId, actorRoomId, "item-looted"); }
        catch (InvalidOperationException ex) { return Reject(corpseId, characterId, actorRoomId, Normalize(ex.Message)); }
    }

    public RuntimeNpcCorpseAccessResult LootGold(string corpseId, string characterId, string actorRoomId, bool accessAuthorized, string authority)
    {
        var corpse = _items.Corpses.FirstOrDefault(x => x.CorpseId.Equals(corpseId, StringComparison.OrdinalIgnoreCase) && !x.CleanedUp);
        if (corpse is null) return Reject(corpseId, characterId, actorRoomId, "corpse-not-found");
        if (!accessAuthorized || string.IsNullOrWhiteSpace(authority)) return Reject(corpseId, characterId, actorRoomId, "access-authority-required");
        if (!corpse.RoomId.Equals(actorRoomId, StringComparison.OrdinalIgnoreCase)) return Reject(corpseId, characterId, actorRoomId, "room-ownership-mismatch");
        var gold = _items.LootGold(corpseId);
        return new(true, corpseId, characterId, actorRoomId, _items.CorpseInventory(corpseId).Count, gold, "gold-looted");
    }

    public RuntimeNpcCorpseAccessResult CleanupExact(string corpseId, DateTimeOffset nowUtc, bool cleanupAuthorized, string authority)
    {
        var corpse = _items.Corpses.FirstOrDefault(x => x.CorpseId.Equals(corpseId, StringComparison.OrdinalIgnoreCase) && !x.CleanedUp);
        if (corpse is null) return Reject(corpseId, string.Empty, string.Empty, "corpse-not-found");
        if (!cleanupAuthorized || string.IsNullOrWhiteSpace(authority)) return Reject(corpseId, string.Empty, corpse.RoomId, "cleanup-authority-required");
        if (corpse.DecaysAtUtc is null || corpse.DecaysAtUtc > nowUtc) return Reject(corpseId, string.Empty, corpse.RoomId, "corpse-not-due");
        var count = _items.CleanupCorpseExact(corpseId, nowUtc);
        return new(true, corpseId, string.Empty, corpse.RoomId, count, 0, "corpse-cleaned");
    }

    private RuntimeNpcCorpseAccessResult Snapshot(string corpseId, string characterId, string roomId, string reason)
    {
        var corpse = _items.Corpses.First(x => x.CorpseId.Equals(corpseId, StringComparison.OrdinalIgnoreCase));
        return new(true, corpseId, characterId, roomId, _items.CorpseInventory(corpseId).Count, corpse.Gold, reason);
    }
    private static RuntimeNpcCorpseAccessResult Reject(string? corpseId, string? characterId, string? roomId, string reason) => new(false, corpseId ?? string.Empty, characterId ?? string.Empty, roomId ?? string.Empty, 0, 0, reason);
    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcCorpseAccessSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];
    public static RuntimeNpcCorpseAccessSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcCorpseAccessSmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot); var host = new ServerRuntimeHost(projectRoot, options); host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured) ? configured : host.Worlds.Values.First();
            var living = new RuntimeLivingWorldService(projectRoot, world, Path.Combine(Path.GetTempPath(), "reforged-npc-corpse-access-" + Guid.NewGuid().ToString("N")));
            var npc = living.InspectNpcs(new(IncludeDead:false)).First(); var catalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id); var engine = new RuntimeItemEngine(catalog);
            var item = engine.Create(catalog.Items.OrderBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).First().Id, RuntimeItemLocations.Npc, npc.InstanceId, ownerId:npc.InstanceId);
            Require(living.ApplyNpcDamage(npc.InstanceId, npc.MaximumHitPoints, DateTimeOffset.UtcNow), "npc-death-admitted");
            var dead=living.InspectNpc(npc.InstanceId)!; var owner=new RuntimeNpcCorpseOwnershipService(living,engine); var now=DateTimeOffset.UtcNow;
            var created=owner.TransferDeathInventory(dead.InstanceId,"corpse:"+dead.InstanceId,dead.RoomId,now,now.AddMinutes(1),7,"smoke"); Require(created.Succeeded,"corpse-created");
            var access=new RuntimeNpcCorpseAccessService(engine); Require(access.Inspect(created.CorpseId).Count==1,"stable-corpse-inspection");
            Require(!access.LootItem(created.CorpseId,item.InstanceId,"actor",dead.RoomId,false,string.Empty).Succeeded,"access-authority-enforced");
            Require(access.LootItem(created.CorpseId,item.InstanceId,"actor",dead.RoomId,true,"smoke").Succeeded,"exact-item-loot");
            Require(access.LootGold(created.CorpseId,"actor",dead.RoomId,true,"smoke").Gold==7,"exact-gold-loot");
            Require(!access.CleanupExact(created.CorpseId,now,false,string.Empty).Succeeded,"cleanup-authority-enforced");
            Require(access.CleanupExact(created.CorpseId,now.AddMinutes(2),true,"smoke").Succeeded,"exact-due-cleanup");
            report.Checks.AddRange(["npc-death-admitted","corpse-created","stable-corpse-inspection","access-authority-enforced","exact-item-loot","exact-gold-loot","cleanup-authority-enforced","exact-due-cleanup"]);
        } catch(Exception ex){ report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
    private static void Require(bool condition,string code){if(!condition)throw new InvalidOperationException(code);}
}
