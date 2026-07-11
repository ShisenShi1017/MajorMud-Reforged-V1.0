using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeItemSmokeResult(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeItemSmokeReport
{
    public static RuntimeItemSmokeResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, Action action)
        {
            try { action(); checks.Add(name); }
            catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
        }

        var catalog = RuntimeItemCatalog.Load(projectRoot, worldId);
        var engine = new RuntimeItemEngine(catalog);
        var owner = "smoke:owner";
        var other = "smoke:other";
        var room = "smoke:room";
        var gettable = catalog.Items.First(x => x.Gettable && !x.Container);
        var containerDef = catalog.Items.FirstOrDefault(x => x.Container && x.Gettable);
        var equipDef = catalog.EquipmentDefinitions.First(x => x.Slot != "NONE" && !x.ClassRestricted && !x.RaceRestricted && x.MinimumLevel == 0 && x.MinimumStrength == 0 && string.IsNullOrWhiteSpace(x.Alignment) && string.IsNullOrWhiteSpace(x.Gender));
        var character = SmokeCharacter(owner, worldId);

        RuntimeItemInstance? item = null;
        Check("create-ground", () => item = engine.Create(gettable.Id, RuntimeItemLocations.Room, room));
        Check("get", () => engine.Get(item!.InstanceId, owner));
        Check("find-lookup", () => { if (engine.Find(item!.InstanceId) is null) throw new InvalidOperationException("lookup failed"); });
        Check("drop", () => engine.Drop(item!.InstanceId, owner, room));
        Check("get-all", () => { engine.GetAll(room, owner); if (!engine.Inventory(owner).Any(x => x.InstanceId == item!.InstanceId)) throw new InvalidOperationException("get all failed"); });
        Check("give", () => engine.Give(item!.InstanceId, owner, other));
        Check("give-back", () => engine.Give(item!.InstanceId, other, owner));

        RuntimeItemInstance? equipment = null;
        Check("equip", () => { equipment = engine.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner); engine.Equip(equipment.InstanceId, owner, character); });
        Check("unequip", () => engine.Unequip(equipment!.InstanceId, owner));
        Check("wear-remove", () => { engine.Wear(equipment!.InstanceId, owner, character); engine.RemoveEquipped(equipment.InstanceId, owner); });

        if (containerDef is not null)
        {
            RuntimeItemInstance? container = null;
            Check("container-put-get", () => { container = engine.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "2" }); engine.Put(item!.InstanceId, owner, container.InstanceId); engine.GetFromContainer(item.InstanceId, owner, container.InstanceId); });
            Check("container-open-close", () => { engine.Close(container!.InstanceId, owner); engine.Open(container.InstanceId, owner); });
        }

        RuntimeCorpseState? corpse = null;
        Check("corpse-create-transfer", () => { corpse = engine.CreateCorpse(owner, room, 123); if (!engine.Items.Any(x => x.LocationKind == RuntimeItemLocations.Corpse && x.LocationId == corpse.CorpseId)) throw new InvalidOperationException("corpse transfer failed"); });
        Check("corpse-loot", () =>
        {
            var corpseState = corpse ?? throw new InvalidOperationException("corpse was not created");
            var corpseItem = engine.Items.First(x => x.LocationKind == RuntimeItemLocations.Corpse && x.LocationId == corpseState.CorpseId);
            engine.Loot(corpseItem.InstanceId, corpseState.CorpseId, owner);
            if (engine.LootGold(corpseState.CorpseId) != 123) throw new InvalidOperationException("gold transfer failed");
        });

        Check("duplicate-prevention-and-rollback", () =>
        {
            var existing = engine.Items.First();
            var count = engine.Items.Count;
            try { engine.Add(existing); throw new InvalidOperationException("duplicate accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)) { }
            if (engine.Items.Count != count) throw new InvalidOperationException("rollback failed");
        });

        Check("save-load-world-state", () =>
        {
            var repository = new RuntimeItemWorldRepository(Path.Combine(Path.GetTempPath(), "ReforgedItemSmoke", Guid.NewGuid().ToString("N")), worldId);
            var state = engine.Snapshot();
            repository.Save(state);
            var loaded = repository.Load();
            var restored = new RuntimeItemEngine(catalog, loaded.Items, loaded.Corpses);
            if (restored.Items.Count != engine.Items.Count) throw new InvalidOperationException("item persistence mismatch");
        });

        Check("disconnect-reconnect-snapshot", () =>
        {
            var state = engine.Snapshot();
            var restored = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
            if (!restored.Validate().Passed) throw new InvalidOperationException("reconnect validation failed");
        });
        Check("runtime-validator", () => { var report = engine.Validate(); if (!report.Passed) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(x => x.Message))); });
        return new RuntimeItemSmokeResult(failures.Count == 0, checks, failures);
    }

    private static RuntimeCharacterRecord SmokeCharacter(string id, string worldId) => new(
        id, "smoke", "Smoke", worldId, "smoke-race", "Smoke Race", "smoke-class", "Smoke Class", 100, 0, 100, 100, 100, 100,
        "smoke:room", 1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["strength"] = 100, ["str"] = 100, ["STR"] = 100 },
        new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string?>(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["alignment"] = "neutral", ["gender"] = "neutral" }, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
