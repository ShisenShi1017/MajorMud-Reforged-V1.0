using System.Text.Json;

namespace Reforged.Server;

public static class RuntimeItemLocations
{
    public const string Character = "character";
    public const string Equipment = "equipment";
    public const string Room = "room";
    public const string Container = "container";
    public const string Corpse = "corpse";
    public const string Npc = "npc";
    public const string Trade = "trade";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(new[] { Character, Equipment, Room, Container, Corpse, Npc, Trade }, StringComparer.OrdinalIgnoreCase);
}

public sealed record RuntimeItemDefinition(string Id, string Name, string ItemType, bool Gettable, bool Container, bool Consumable, bool Stackable, int DefaultCharges);
public sealed record RuntimeEquipmentDefinition(string ItemId, string Slot, bool TwoHanded, bool ClassRestricted, bool RaceRestricted, int MinimumLevel, int MinimumStrength, string? Alignment, string? Gender);
public sealed record RuntimeItemInstance(
    string InstanceId,
    string CanonicalItemId,
    string? OwnerId,
    string LocationKind,
    string LocationId,
    string? EquippedSlot,
    int Charges,
    int StackQuantity,
    int? Durability,
    IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record RuntimeCorpseState(string CorpseId, string CharacterId, string RoomId, long Gold, DateTimeOffset CreatedAtUtc, DateTimeOffset? DecaysAtUtc, bool CleanedUp = false);
public sealed record RuntimeItemState(IReadOnlyList<RuntimeItemInstance> Items, long Gold, IReadOnlyDictionary<string, string> ActiveItemState, IReadOnlyList<RuntimeCorpseState>? Corpses = null);
public sealed record RuntimeItemValidationIssue(string Code, string Message, string? InstanceId = null);
public sealed record RuntimeItemValidationReport(bool Passed, IReadOnlyList<RuntimeItemValidationIssue> Issues);

public sealed class RuntimeItemCatalog
{
    private readonly Dictionary<string, RuntimeItemDefinition> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeEquipmentDefinition> _equipment = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _rules = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Slots => _slots.OrderBy(x => x).ToArray();
    public IReadOnlyCollection<RuntimeItemDefinition> Items => _items.Values.ToArray();
    public IReadOnlyCollection<RuntimeEquipmentDefinition> EquipmentDefinitions => _equipment.Values.ToArray();
    public bool HasRule(string rule) => _rules.Contains(rule);

    public static RuntimeItemCatalog Load(string root, string worldId)
    {
        var catalog = new RuntimeItemCatalog();
        var folder = Path.Combine(root, "Data", worldId);
        catalog.LoadItems(Path.Combine(folder, "items.json"));
        catalog.LoadEquipment(Path.Combine(folder, "equipment.json"));
        catalog.LoadSlots(Path.Combine(folder, "equipment_slots.json"));
        catalog.LoadRules(Path.Combine(folder, "equipment_rules.json"));
        return catalog;
    }

    public RuntimeItemDefinition Require(string id) => _items.TryGetValue(id, out var item) ? item : throw new InvalidOperationException($"Unknown generated item '{id}'.");
    public RuntimeEquipmentDefinition? Equipment(string id) => _equipment.GetValueOrDefault(id);
    public bool IsLegalSlot(string slot) => _slots.Contains(slot);

    private void LoadItems(string path)
    {
        using var document = LoadProjection(path);
        foreach (var x in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = S(x, "id");
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Generated item projection contains a record without id.");
            _items[id] = new(id, S(x, "name") ?? string.Empty, S(x, "item_type") ?? "misc", B(x, "gettable"), B(x, "container"), B(x, "consumable"), B(x, "stackable"), I(x, "default_charges"));
        }
    }

    private void LoadEquipment(string path)
    {
        using var document = LoadProjection(path);
        foreach (var x in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = S(x, "item_id");
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Generated equipment projection contains a record without item_id.");
            _equipment[id] = new(id, S(x, "slot") ?? "NONE", B(x, "two_handed"), B(x, "class_restricted"), B(x, "race_restricted"), I(x, "minimum_level"), I(x, "minimum_strength"), S(x, "alignment"), S(x, "gender"));
        }
    }

    private void LoadSlots(string path)
    {
        using var document = LoadProjection(path);
        foreach (var x in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = S(x, "id");
            if (!string.IsNullOrWhiteSpace(id)) _slots.Add(id);
        }
    }

    private void LoadRules(string path)
    {
        using var document = LoadProjection(path);
        foreach (var x in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var rule = S(x, "rule");
            if (!string.IsNullOrWhiteSpace(rule)) _rules.Add(rule);
        }
    }

    private static JsonDocument LoadProjection(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Librarium item projection is missing.", path);
        var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("authority", out var authority) || authority.GetString() != "Librarium")
        {
            document.Dispose();
            throw new InvalidOperationException("Item projection is not Librarium authoritative.");
        }
        return document;
    }

    private static bool B(JsonElement e, string n) => e.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.True;
    private static int I(JsonElement e, string n) => e.TryGetProperty(n, out var x) && x.TryGetInt32(out var v) ? v : 0;
    private static string? S(JsonElement e, string n) => e.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
}

public sealed class RuntimeItemEngine
{
    private readonly RuntimeItemCatalog _catalog;
    private Dictionary<string, RuntimeItemInstance> _items = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, RuntimeCorpseState> _corpses = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public RuntimeItemEngine(RuntimeItemCatalog catalog, IEnumerable<RuntimeItemInstance>? initial = null, IEnumerable<RuntimeCorpseState>? corpses = null)
    {
        _catalog = catalog;
        foreach (var item in initial ?? Array.Empty<RuntimeItemInstance>()) AddUnique(item);
        foreach (var corpse in corpses ?? Array.Empty<RuntimeCorpseState>()) _corpses.Add(corpse.CorpseId, corpse);
        EnsureValid();
    }

    public IReadOnlyCollection<RuntimeItemInstance> Items { get { lock (_gate) return _items.Values.ToArray(); } }
    public IReadOnlyCollection<RuntimeCorpseState> Corpses { get { lock (_gate) return _corpses.Values.ToArray(); } }
    public RuntimeItemInstance RequireInstance(string instanceId) { lock (_gate) return Require(instanceId); }
    public RuntimeItemInstance? Find(string instanceId) { lock (_gate) return _items.GetValueOrDefault(instanceId); }
    public IReadOnlyList<RuntimeItemInstance> Inventory(string ownerId) { lock (_gate) return _items.Values.Where(x => Same(x.OwnerId, ownerId) && x.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) && x.EquippedSlot is null).ToArray(); }
    public IReadOnlyList<RuntimeItemInstance> Equipment(string ownerId) { lock (_gate) return _items.Values.Where(x => Same(x.OwnerId, ownerId) && x.EquippedSlot is not null).ToArray(); }
    public IReadOnlyList<RuntimeItemInstance> Ground(string roomId) { lock (_gate) return _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, roomId)).ToArray(); }
    public IReadOnlyList<RuntimeItemInstance> Contents(string containerId) { lock (_gate) return _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, containerId)).ToArray(); }

    public RuntimeItemInstance Create(string canonicalId, string locationKind, string locationId, int quantity = 1, string? ownerId = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        lock (_gate)
        {
            var definition = _catalog.Require(canonicalId);
            if (!RuntimeItemLocations.All.Contains(locationKind)) throw new InvalidOperationException("Invalid item location kind.");
            if (quantity < 1 || (!definition.Stackable && quantity != 1)) throw new InvalidOperationException("Invalid stack quantity for generated item definition.");
            var item = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, ownerId, locationKind, locationId, null, definition.DefaultCharges, quantity, null, metadata);
            AddUnique(item);
            EnsureValid();
            return item;
        }
    }

    public RuntimeItemInstance Add(RuntimeItemInstance item) => Atomic(() => { _catalog.Require(item.CanonicalItemId); AddUnique(item); return item; });
    public RuntimeItemInstance Remove(string instanceId) => Atomic(() => { var item = Require(instanceId); if (_items.Values.Any(x => Same(x.LocationId, instanceId) && x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Cannot remove a non-empty container."); _items.Remove(instanceId); return item; });
    public RuntimeItemInstance Get(string instanceId, string characterId) => Atomic(() => TransferCore(instanceId, RuntimeItemLocations.Character, characterId, characterId, null, requireGettable: true));
    public IReadOnlyList<RuntimeItemInstance> GetAll(string roomId, string characterId) => Atomic(() => Ground(roomId).Where(x => _catalog.Require(x.CanonicalItemId).Gettable).Select(x => TransferCore(x.InstanceId, RuntimeItemLocations.Character, characterId, characterId, null, false)).ToArray());
    public RuntimeItemInstance Drop(string instanceId, string characterId, string roomId) => Atomic(() => { RequireOwned(instanceId, characterId); return TransferCore(instanceId, RuntimeItemLocations.Room, roomId, null, null, false); });
    public RuntimeItemInstance Give(string instanceId, string fromCharacter, string toCharacter) => Atomic(() => { RequireOwned(instanceId, fromCharacter); return TransferCore(instanceId, RuntimeItemLocations.Character, toCharacter, toCharacter, null, false); });
    public RuntimeItemInstance MoveToNpc(string instanceId, string ownerId, string npcId) => Atomic(() => { RequireOwned(instanceId, ownerId); return TransferCore(instanceId, RuntimeItemLocations.Npc, npcId, npcId, null, false); });
    public RuntimeItemInstance MoveToTrade(string instanceId, string ownerId, string tradeId) => Atomic(() => { RequireOwned(instanceId, ownerId); return TransferCore(instanceId, RuntimeItemLocations.Trade, tradeId, ownerId, null, false); });

    public RuntimeItemInstance MoveToContainer(string instanceId, string ownerId, string containerInstanceId) => Put(instanceId, ownerId, containerInstanceId);

    public RuntimeItemInstance Put(string instanceId, string ownerId, string containerInstanceId)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, ownerId);
            var container = RequireOwned(containerInstanceId, ownerId);
            if (!_catalog.Require(container.CanonicalItemId).Container) throw new InvalidOperationException("Target is not a generated container.");
            if (!IsContainerOpen(container)) throw new InvalidOperationException("Container is closed.");
            if (Same(item.InstanceId, container.InstanceId) || IsDescendant(container.InstanceId, item.InstanceId)) throw new InvalidOperationException("Container nesting cycle detected.");
            EnforceCapacity(container);
            return TransferCore(item.InstanceId, RuntimeItemLocations.Container, container.InstanceId, ownerId, null, false);
        });
    }

    public RuntimeItemInstance GetFromContainer(string instanceId, string ownerId, string containerInstanceId)
    {
        return Atomic(() =>
        {
            var container = RequireOwned(containerInstanceId, ownerId);
            if (!IsContainerOpen(container)) throw new InvalidOperationException("Container is closed.");
            var item = Require(instanceId);
            if (!item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, containerInstanceId)) throw new InvalidOperationException("Item is not in the requested container.");
            return TransferCore(instanceId, RuntimeItemLocations.Character, ownerId, ownerId, null, false);
        });
    }

    public RuntimeItemInstance Open(string containerInstanceId, string ownerId) => SetContainerOpen(containerInstanceId, ownerId, true);
    public RuntimeItemInstance Close(string containerInstanceId, string ownerId) => SetContainerOpen(containerInstanceId, ownerId, false);

    public RuntimeItemInstance Equip(string instanceId, string characterId, RuntimeCharacterRecord character, string? requestedSlot = null)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, characterId);
            var relationship = _catalog.Equipment(item.CanonicalItemId) ?? throw new InvalidOperationException("Item has no generated equipment relationship.");
            var slot = requestedSlot ?? relationship.Slot;
            if (!_catalog.IsLegalSlot(slot) || !Same(slot, relationship.Slot)) throw new InvalidOperationException($"Illegal slot: generated slot is {relationship.Slot}.");
            if (relationship.MinimumLevel > character.Level) throw new InvalidOperationException("Level restriction failed.");
            var strength = character.BaseStats.TryGetValue("strength", out var strengthValue)
                ? strengthValue
                : character.BaseStats.TryGetValue("str", out var shortStrengthValue)
                    ? shortStrengthValue
                    : character.BaseStats.GetValueOrDefault("STR");
            if (relationship.MinimumStrength > strength) throw new InvalidOperationException("Strength restriction failed.");
            if (!string.IsNullOrWhiteSpace(relationship.Alignment) && (!character.Conditions.TryGetValue("alignment", out var alignment) || !Same(alignment, relationship.Alignment))) throw new InvalidOperationException("Alignment restriction failed.");
            if (!string.IsNullOrWhiteSpace(relationship.Gender) && (!character.Conditions.TryGetValue("gender", out var gender) || !Same(gender, relationship.Gender))) throw new InvalidOperationException("Gender restriction failed.");
            if (relationship.ClassRestricted && !MetadataAllows(item, "allowed_classes", character.ClassId, character.ClassName)) throw new InvalidOperationException("Class restriction failed; generated relationship requires an authoritative runtime allowlist.");
            if (relationship.RaceRestricted && !MetadataAllows(item, "allowed_races", character.RaceId, character.RaceName)) throw new InvalidOperationException("Race restriction failed; generated relationship requires an authoritative runtime allowlist.");
            if (_items.Values.Any(x => Same(x.OwnerId, characterId) && x.EquippedSlot is not null && Same(x.EquippedSlot, slot) && !Same(x.InstanceId, item.InstanceId))) throw new InvalidOperationException("Equipment slot is occupied.");
            EnforceHandRules(characterId, relationship, slot, item.InstanceId);
            return Replace(item with { LocationKind = RuntimeItemLocations.Equipment, LocationId = characterId, OwnerId = characterId, EquippedSlot = slot });
        });
    }

    public RuntimeItemInstance Wear(string instanceId, string characterId, RuntimeCharacterRecord character, string? requestedSlot = null) => Equip(instanceId, characterId, character, requestedSlot);
    public RuntimeItemInstance Unequip(string instanceId, string characterId) => Atomic(() => { var item = RequireOwned(instanceId, characterId); if (item.EquippedSlot is null) throw new InvalidOperationException("Item is not equipped."); return TransferCore(instanceId, RuntimeItemLocations.Character, characterId, characterId, null, false); });
    public RuntimeItemInstance RemoveEquipped(string instanceId, string characterId) => Unequip(instanceId, characterId);

    public RuntimeCorpseState CreateCorpse(string characterId, string roomId, long gold, TimeSpan? decayAfter = null)
    {
        return Atomic(() =>
        {
            var now = DateTimeOffset.UtcNow;
            var corpse = new RuntimeCorpseState(Guid.NewGuid().ToString("N"), characterId, roomId, gold, now, decayAfter is null ? null : now.Add(decayAfter.Value));
            _corpses.Add(corpse.CorpseId, corpse);
            foreach (var item in _items.Values.Where(x => Same(x.OwnerId, characterId)).ToArray()) TransferCore(item.InstanceId, RuntimeItemLocations.Corpse, corpse.CorpseId, null, null, false);
            return corpse;
        });
    }

    public RuntimeItemInstance Loot(string instanceId, string corpseId, string characterId) => Atomic(() => { RequireCorpse(corpseId); var item = Require(instanceId); if (!item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, corpseId)) throw new InvalidOperationException("Item is not in that corpse."); return TransferCore(instanceId, RuntimeItemLocations.Character, characterId, characterId, null, false); });
    public long LootGold(string corpseId) => Atomic(() => { var corpse = RequireCorpse(corpseId); _corpses[corpseId] = corpse with { Gold = 0 }; return corpse.Gold; });
    public int CleanupExpiredCorpses(DateTimeOffset now) => Atomic(() => { var expired = _corpses.Values.Where(x => !x.CleanedUp && x.DecaysAtUtc <= now).ToArray(); foreach (var corpse in expired) { foreach (var item in _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, corpse.CorpseId)).ToArray()) _items.Remove(item.InstanceId); _corpses[corpse.CorpseId] = corpse with { CleanedUp = true, Gold = 0 }; } return expired.Length; });

    public RuntimeItemInstance Use(string instanceId, string characterId)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, characterId);
            var definition = _catalog.Require(item.CanonicalItemId);
            if (!definition.Consumable && item.Charges <= 0) throw new InvalidOperationException("Item is not usable.");
            if (item.Charges > 0) return Replace(item with { Charges = item.Charges - 1 });
            if (item.StackQuantity > 1) return Replace(item with { StackQuantity = item.StackQuantity - 1 });
            _items.Remove(item.InstanceId);
            return item;
        });
    }

    public RuntimeItemState Snapshot(long gold = 0, IReadOnlyDictionary<string, string>? active = null) { lock (_gate) return new(_items.Values.ToArray(), gold, active ?? new Dictionary<string, string>(), _corpses.Values.ToArray()); }
    public RuntimeItemValidationReport Validate() { lock (_gate) return RuntimeItemValidator.Validate(_catalog, _items.Values, _corpses.Values); }

    private RuntimeItemInstance SetContainerOpen(string id, string ownerId, bool open) => Atomic(() => { var container = RequireOwned(id, ownerId); if (!_catalog.Require(container.CanonicalItemId).Container) throw new InvalidOperationException("Item is not a generated container."); var metadata = new Dictionary<string, string>(container.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase) { ["open"] = open ? "true" : "false" }; return Replace(container with { Metadata = metadata }); });
    private bool IsContainerOpen(RuntimeItemInstance container) => container.Metadata is null || !container.Metadata.TryGetValue("open", out var value) || bool.TryParse(value, out var open) && open;
    private void EnforceCapacity(RuntimeItemInstance container) { if (container.Metadata?.TryGetValue("capacity", out var raw) == true && int.TryParse(raw, out var capacity) && capacity >= 0 && Contents(container.InstanceId).Count >= capacity) throw new InvalidOperationException("Container capacity exceeded."); }
    private bool IsDescendant(string candidateContainerId, string ancestorId) { var current = _items.GetValueOrDefault(candidateContainerId); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); while (current is not null && current.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && visited.Add(current.InstanceId)) { if (Same(current.LocationId, ancestorId)) return true; current = _items.GetValueOrDefault(current.LocationId); } return false; }
    private void EnforceHandRules(string ownerId, RuntimeEquipmentDefinition relationship, string slot, string currentId) { if (!_catalog.HasRule("TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD")) return; var equipped = _items.Values.Where(x => Same(x.OwnerId, ownerId) && x.EquippedSlot is not null && !Same(x.InstanceId, currentId)).ToArray(); if (relationship.TwoHanded && equipped.Any(x => Same(x.EquippedSlot, "OFF_HAND") || Same(x.EquippedSlot, "SHIELD"))) throw new InvalidOperationException("Two-handed item conflicts with off-hand or shield."); if ((Same(slot, "OFF_HAND") || Same(slot, "SHIELD")) && equipped.Any(x => _catalog.Equipment(x.CanonicalItemId)?.TwoHanded == true)) throw new InvalidOperationException("Off-hand or shield conflicts with two-handed item."); }
    private static bool MetadataAllows(RuntimeItemInstance item, string key, params string[] candidates)
    {
        if (item.Metadata is null || !item.Metadata.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var allowed = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return candidates.Any(candidate => allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase));
    }
    private RuntimeItemInstance TransferCore(string id, string kind, string location, string? owner, string? slot, bool requireGettable) { var item = Require(id); if (requireGettable && !_catalog.Require(item.CanonicalItemId).Gettable) throw new InvalidOperationException("Item is not gettable according to generated data."); return Replace(item with { LocationKind = kind, LocationId = location, OwnerId = owner, EquippedSlot = slot }); }
    private RuntimeItemInstance Require(string id) => _items.TryGetValue(id, out var item) ? item : throw new InvalidOperationException("Unknown item instance.");
    private RuntimeCorpseState RequireCorpse(string id) => _corpses.TryGetValue(id, out var corpse) && !corpse.CleanedUp ? corpse : throw new InvalidOperationException("Unknown or expired corpse.");
    private RuntimeItemInstance RequireOwned(string id, string owner) { var item = Require(id); if (!Same(item.OwnerId, owner)) throw new InvalidOperationException("Item ownership check failed."); return item; }
    private void AddUnique(RuntimeItemInstance item) { if (!_items.TryAdd(item.InstanceId, item)) throw new InvalidOperationException("Duplicate item instance ID."); }
    private RuntimeItemInstance Replace(RuntimeItemInstance item) { _items[item.InstanceId] = item; return item; }
    private void EnsureValid() { var report = RuntimeItemValidator.Validate(_catalog, _items.Values, _corpses.Values); if (!report.Passed) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(x => x.Message))); }
    private T Atomic<T>(Func<T> operation) { lock (_gate) { var beforeItems = new Dictionary<string, RuntimeItemInstance>(_items, StringComparer.OrdinalIgnoreCase); var beforeCorpses = new Dictionary<string, RuntimeCorpseState>(_corpses, StringComparer.OrdinalIgnoreCase); try { var result = operation(); EnsureValid(); return result; } catch { _items = beforeItems; _corpses = beforeCorpses; throw; } } }
    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public static class RuntimeItemValidator
{
    public static RuntimeItemValidationReport Validate(RuntimeItemCatalog catalog, IEnumerable<RuntimeItemInstance> items, IEnumerable<RuntimeCorpseState>? corpses = null)
    {
        var list = items.ToArray();
        var corpseIds = (corpses ?? Array.Empty<RuntimeCorpseState>()).Where(x => !x.CleanedUp).Select(x => x.CorpseId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issues = new List<RuntimeItemValidationIssue>();
        foreach (var duplicate in list.GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1)) issues.Add(new("DUPLICATE_INSTANCE_ID", "Duplicate item instance ID.", duplicate.Key));
        var ids = list.Select(x => x.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            try { var definition = catalog.Require(item.CanonicalItemId); if (item.StackQuantity < 1 || (!definition.Stackable && item.StackQuantity != 1)) issues.Add(new("INVALID_STACK", "Invalid stack quantity.", item.InstanceId)); } catch (Exception ex) { issues.Add(new("UNKNOWN_CANONICAL_ITEM", ex.Message, item.InstanceId)); }
            if (!RuntimeItemLocations.All.Contains(item.LocationKind) || string.IsNullOrWhiteSpace(item.LocationId)) issues.Add(new("INVALID_LOCATION", "Invalid item location.", item.InstanceId));
            if ((item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(item.OwnerId)) issues.Add(new("INVALID_OWNER", "Owned location requires an owner.", item.InstanceId));
            if (item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && (!ids.Contains(item.LocationId) || Same(item.LocationId, item.InstanceId))) issues.Add(new("INVALID_CONTAINER", "Container reference is invalid.", item.InstanceId));
            if (item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && !corpseIds.Contains(item.LocationId)) issues.Add(new("INVALID_CORPSE", "Corpse reference is invalid.", item.InstanceId));
            if (item.EquippedSlot is not null)
            {
                var relationship = catalog.Equipment(item.CanonicalItemId);
                if (!item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || relationship is null || !catalog.IsLegalSlot(item.EquippedSlot) || !Same(relationship.Slot, item.EquippedSlot)) issues.Add(new("ILLEGAL_EQUIPPED_SLOT", "Illegal equipped slot.", item.InstanceId));
            }
        }
        foreach (var occupied in list.Where(x => x.EquippedSlot is not null).GroupBy(x => $"{x.OwnerId}\0{x.EquippedSlot}", StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1)) foreach (var item in occupied) issues.Add(new("SLOT_CONFLICT", "Multiple items occupy the same equipment slot.", item.InstanceId));
        return new RuntimeItemValidationReport(issues.Count == 0, issues);
    }
    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

public sealed class RuntimeItemWorldRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _path;
    public RuntimeItemWorldRepository(string projectRoot, string worldId) { var folder = Path.Combine(projectRoot, "Saves", "Runtime0137", "worlds"); Directory.CreateDirectory(folder); _path = Path.Combine(folder, RuntimeIdentity.Normalize(worldId, "world") + ".items.json"); }
    public RuntimeItemState Load() => File.Exists(_path) ? JsonSerializer.Deserialize<RuntimeItemState>(File.ReadAllText(_path), Options) ?? Empty() : Empty();
    public void Save(RuntimeItemState state) { var temp = _path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(state, Options)); File.Move(temp, _path, true); }
    private static RuntimeItemState Empty() => new(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
}
