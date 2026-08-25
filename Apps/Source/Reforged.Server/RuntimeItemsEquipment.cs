using System.Text.Json;
using System.Security.Cryptography;

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
    public const string Bank = "bank";
    public const string Merchant = "merchant";
    public const string Quest = "quest";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(new[] { Character, Equipment, Room, Container, Corpse, Npc, Trade, Bank, Merchant, Quest }, StringComparer.OrdinalIgnoreCase);
}

public sealed record RuntimeItemDefinition(
    string Id,
    string Name,
    string ItemType,
    bool Gettable,
    bool Container,
    bool Consumable,
    bool Stackable,
    int DefaultCharges,
    int Weight = 0,
    int ArmorClassBonus = 0,
    int DamageResistanceBonus = 0,
    int AccuracyBonus = 0,
    int SpeedBonus = 0,
    bool CursedWhenEquipped = false,
    bool RetainedOnDeath = false,
    bool DeleteAtCleanup = false,
    bool VisibleAtCleanup = false)
{
    public long Cost { get; init; }
    public int MinimumDamage { get; init; }
    public int MaximumDamage { get; init; }
    public int WeaponType { get; init; }
    public int ArmorType { get; init; }
    public bool NotDroppable { get; init; }
    public bool DestroyOnDeath { get; init; }
    public bool RetainAfterUses { get; init; }
    public bool UsesConsumedOnUse { get; init; }
    public int MaximumUses { get; init; }
    public int RechargeAtCleanup { get; init; }
    public int GameLimit { get; init; }
    public int CurrentInstances { get; init; }
    public bool Robable { get; init; }
    public RuntimeCurrencyBundle OpenRewards { get; init; } = RuntimeCurrencyBundle.Zero;
    public IReadOnlySet<int> NegatedAbilityIds { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int, int> EquipmentAbilityModifiers { get; init; } = new Dictionary<int, int>();
}

public sealed record RuntimeEquipmentDefinition(string ItemId, string Slot, bool TwoHanded, bool ClassRestricted, bool RaceRestricted, int MinimumLevel, int MaximumLevel, int MinimumStrength, string? Alignment, string? Gender);
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
public sealed record RuntimeCorpseState(
    string CorpseId,
    string CharacterId,
    string RoomId,
    long Gold,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecaysAtUtc,
    bool CleanedUp = false,
    string? LootOwnerId = null,
    DateTimeOffset? ExclusiveLootUntilUtc = null,
    string? LimitedItemReservationOwnerId = null);
public sealed record RuntimeItemState(IReadOnlyList<RuntimeItemInstance> Items, long Gold, IReadOnlyDictionary<string, string> ActiveItemState, IReadOnlyList<RuntimeCorpseState>? Corpses = null);
public sealed record RuntimeItemValidationIssue(string Code, string Message, string? InstanceId = null);
public sealed record RuntimeItemValidationReport(bool Passed, IReadOnlyList<RuntimeItemValidationIssue> Issues);
public sealed record RuntimeItemRecoveryResult(RuntimeItemState State, string Source, bool RecoveredPrimary, string? PrimaryFailure = null);
public sealed record RuntimeItemDuplicateReconciliationResult(RuntimeItemState State, int RemovedDuplicateItems, int RemovedDuplicateCorpses);
public sealed record RuntimeItemOrphanReconciliationResult(RuntimeItemState State, int RecoveredContainerItems, IReadOnlyList<string> RecoveredInstanceIds);
public sealed record RuntimeItemOwnerLocationReconciliationResult(RuntimeItemState State, int RecoveredItems, IReadOnlyList<string> RecoveredInstanceIds);
public sealed record RuntimeItemContainerOwnerReconciliationResult(RuntimeItemState State, int RecoveredItems, IReadOnlyList<string> RecoveredInstanceIds);
public sealed record RuntimeItemOwnershipSnapshot(
    IReadOnlySet<string> CharacterIds,
    IReadOnlySet<string> NpcIds,
    IReadOnlySet<string> MerchantIds,
    IReadOnlySet<string> QuestIds,
    IReadOnlySet<string> EventIds);
public sealed record RuntimeItemCrossSaveOwnershipIssue(string Code, string Message, string? InstanceId = null, string? OwnerId = null);
public sealed record RuntimeItemCrossSaveOwnershipReport(bool Passed, IReadOnlyList<RuntimeItemCrossSaveOwnershipIssue> Issues);
public sealed record RuntimeItemSaveGenerationSnapshot(string GenerationId, IReadOnlyDictionary<string, string> SubsystemGenerations);
public sealed record RuntimeItemSaveGenerationIssue(string Code, string Message, string? Subsystem = null, string? ObservedGeneration = null);
public sealed record RuntimeItemSaveGenerationReport(bool Passed, IReadOnlyList<RuntimeItemSaveGenerationIssue> Issues);
public sealed record RuntimeItemLimitedReconciliationIssue(string Code, string Message, string CanonicalItemId, string Scope, string? ScopeOwnerId = null, int? ObservedQuantity = null, int? MaximumQuantity = null);
public sealed record RuntimeItemLimitedReconciliationReport(bool Passed, IReadOnlyList<RuntimeItemLimitedReconciliationIssue> Issues, IReadOnlyDictionary<string, int> ReservationQuantities);
public sealed record RuntimeItemConsumptionResult(string InstanceId, int ConsumedQuantity, int RemainingQuantity, bool Destroyed);
public sealed record RuntimeItemDurabilityResult(string InstanceId, int PreviousDurability, int CurrentDurability, bool Broken);
public sealed record RuntimeItemDestructionResult(string InstanceId, string CanonicalItemId, string? PreviousOwnerId, string PreviousLocationKind, string PreviousLocationId, bool AdministrativeOverride);
public sealed record RuntimeItemAdministrativeInspection(RuntimeItemInstance Item, RuntimeItemDefinition Definition, IReadOnlyList<RuntimeItemInstance> Descendants, IReadOnlyList<RuntimeItemValidationIssue> ValidationIssues);
public sealed record RuntimeItemAdministrativeMoveResult(string InstanceId, string PreviousLocationKind, string PreviousLocationId, string? PreviousOwnerId, string CurrentLocationKind, string CurrentLocationId, string? CurrentOwnerId, IReadOnlyList<string> MovedInstanceIds);
public sealed record RuntimeItemAdministrativeAuditResult(RuntimeItemValidationReport RuntimeValidation, RuntimeItemLimitedReconciliationReport LimitedValidation, int ItemCount, int CorpseCount, int ContainerHierarchyCount);
public sealed record RuntimeItemMerchantRestockResult(string MerchantId, string CanonicalItemId, string RestockKey, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeRoomItemResetResult(string RoomId, string CanonicalItemId, string ResetKey, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeContainerRestockResult(string ContainerInstanceId, string CanonicalItemId, string RestockKey, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeNpcItemResetResult(string NpcId, string CanonicalItemId, string ResetKey, string? EquippedSlot, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeNpcCorpseTransferResult(string CorpseId, string NpcId, string RoomId, IReadOnlyList<string> TransferredRootInstanceIds, int TransferredInstanceCount, IReadOnlyList<string> RetainedInstanceIds);
public sealed record RuntimeNpcCorpseLootResult(string CorpseId, string CanonicalItemId, string LootKey, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeNpcLootRollEntry(string CanonicalItemId, string LootKey, int ChanceBasisPoints, int MinimumQuantity, int MaximumQuantity, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record RuntimeNpcLootRollOutcome(string CanonicalItemId, string LootKey, int RollBasisPoints, bool Awarded, int TargetQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeNpcLootTableResult(string CorpseId, string TableKey, int Seed, IReadOnlyList<RuntimeNpcLootRollOutcome> Outcomes);
public sealed record RuntimeNpcCorpseGoldResult(string CorpseId, string GoldKey, int Seed, long MinimumGold, long MaximumGold, long PreviousGold, long TargetGold, long FinalGold, bool Created);
public sealed record RuntimeQuestItemGrantResult(string QuestId, string CharacterId, string CanonicalItemId, string GrantKey, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeQuestItemConsumptionResult(string QuestId, string CharacterId, string CanonicalItemId, string ConsumeKey, int RequestedQuantity, int ConsumedQuantity, IReadOnlyList<string> RemovedInstanceIds, IReadOnlyList<string> ChangedInstanceIds);
public sealed record RuntimeQuestItemCleanupResult(string QuestId, string CharacterId, string CanonicalItemId, string CleanupKey, string LifecycleReason, int TargetQuantity, int ExistingQuantity, int RemovedQuantity, int FinalQuantity, IReadOnlyList<string> RemovedInstanceIds, IReadOnlyList<string> ChangedInstanceIds);
public sealed record RuntimeEventItemGrantResult(string EventId, string CharacterId, string CanonicalItemId, string RewardKey, int TargetQuantity, int ExistingQuantity, int CreatedQuantity, int FinalQuantity, IReadOnlyList<string> CreatedInstanceIds);
public sealed record RuntimeEventItemCleanupResult(string EventId, string CharacterId, string CanonicalItemId, string CleanupKey, string LifecycleReason, int TargetQuantity, int ExistingQuantity, int RemovedQuantity, int FinalQuantity, IReadOnlyList<string> RemovedInstanceIds, IReadOnlyList<string> ChangedInstanceIds);
public sealed record RuntimeCorpseRestorationResult(
    string CorpseId,
    string CharacterId,
    IReadOnlyList<string> RestoredRootInstanceIds,
    int RestoredInstanceCount,
    long RestoredGold,
    bool CorpseFinalized,
    IReadOnlyList<string>? DisplacedEquipmentInstanceIds = null);

public sealed partial class RuntimeItemCatalog
{
    private string _projectRoot = string.Empty;
    private string _worldId = string.Empty;
    private readonly Dictionary<string, RuntimeItemDefinition> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeEquipmentDefinition> _equipment = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _rules = new(StringComparer.OrdinalIgnoreCase);

    internal string ProjectRoot => _projectRoot;
    internal string WorldId => _worldId;
    public IReadOnlyCollection<string> Slots => _slots.OrderBy(x => x).ToArray();
    public IReadOnlyCollection<RuntimeItemDefinition> Items => _items.Values.ToArray();
    public IReadOnlyCollection<RuntimeEquipmentDefinition> EquipmentDefinitions => _equipment.Values.ToArray();
    public bool HasRule(string rule) => _rules.Contains(rule);

    public static RuntimeItemCatalog Load(string root, string worldId)
    {
        var catalog = new RuntimeItemCatalog
        {
            _projectRoot = Path.GetFullPath(root),
            _worldId = RuntimeIdentity.Normalize(worldId, "world")
        };
        var folder = Path.Combine(root, "Data", worldId);
        catalog.LoadItems(Path.Combine(folder, "items.json"));
        catalog.LoadEquipment(Path.Combine(folder, "equipment.json"));
        catalog.LoadSlots(Path.Combine(folder, "equipment_slots.json"));
        catalog.LoadRules(Path.Combine(folder, "equipment_rules.json"));
        return catalog;
    }

    public RuntimeItemDefinition Require(string id) => _items.TryGetValue(id, out var item) ? item : throw new InvalidOperationException($"Unknown generated item '{id}'.");

    /// <summary>
    /// Classic noun matching is word-prefix based.  A player may identify an item by
    /// canonical id, legacy number, full name, leading phrase, or any individual noun
    /// word (for example "gl" for "leather gloves").  Keep this in the authoritative
    /// item catalog so inventory, equipment, shops, loot, and quest consumers all use
    /// the same decoded-data resolver.
    /// </summary>
    public bool Matches(string itemId, string query)
    {
        if (!_items.TryGetValue(itemId, out var item) || string.IsNullOrWhiteSpace(query)) return false;
        var q = NormalizeMatchText(query);
        if (q.Length == 0) return false;
        if (NormalizeMatchText(item.Id) == q || NormalizeMatchText(item.Id).StartsWith(q, StringComparison.OrdinalIgnoreCase)) return true;
        var legacy = item.Id[(item.Id.LastIndexOf('.') + 1)..];
        if (legacy.Equals(q, StringComparison.OrdinalIgnoreCase)) return true;
        var name = NormalizeMatchText(item.Name);
        if (name.Equals(q, StringComparison.OrdinalIgnoreCase) || name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return true;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => word.StartsWith(q, StringComparison.OrdinalIgnoreCase))) return true;
        var queryWords = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return queryWords.Length > 1 && queryWords.Length <= words.Length &&
               queryWords.Select((word, index) => words[index].StartsWith(word, StringComparison.OrdinalIgnoreCase)).All(value => value);
    }

    private static string NormalizeMatchText(string value)
    {
        var words = value.Trim().ToLowerInvariant()
            .Split(new[] { ' ', '\t', '-', '_', '.', ',', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word is not "a" and not "an" and not "the");
        return string.Join(' ', words);
    }

    public RuntimeEquipmentDefinition? Equipment(string id) => _equipment.GetValueOrDefault(id);
    public bool IsLegalSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return false;
        // Equipment relationships are decoded executable authority. A relationship
        // slot is legal even when the separate slot projection omitted that native
        // weapon position (for example WIELD).
        return _slots.Contains(slot) || _equipment.Values.Any(definition =>
            definition.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadItems(string path)
    {
        using var document = LoadProjection(path);
        foreach (var x in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = S(x, "id");
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Generated item projection contains a record without id.");
            var combat = x.TryGetProperty("combat", out var combatElement) && combatElement.ValueKind == JsonValueKind.Object ? combatElement : default;
            _items[id] = new(
                id,
                S(x, "name") ?? string.Empty,
                S(x, "item_type") ?? "misc",
                B(x, "gettable"),
                B(x, "container"),
                B(x, "consumable"),
                B(x, "stackable"),
                Math.Max(I(x, "default_charges"), Math.Max(0, ReadPublicInt(x, "max_uses"))),
                EconomyWeight(x),
                combat.ValueKind == JsonValueKind.Object ? I(combat, "armor_class_bonus") : 0,
                combat.ValueKind == JsonValueKind.Object ? I(combat, "damage_resistance_bonus") : 0,
                combat.ValueKind == JsonValueKind.Object ? I(combat, "accuracy") : 0,
                combat.ValueKind == JsonValueKind.Object ? I(combat, "speed") : 0,
                HasAbility(x, 82) || HasAbility(x, 83),
                HasAbility(x, 83) || HasAbility(x, 100),
                HasAbility(x, 119),
                HasAbility(x, 154))
            {
                Cost = ReadPublicLong(x, "cost"),
                MinimumDamage = ReadPublicInt(x, "min_hit"),
                MaximumDamage = ReadPublicInt(x, "max_hit"),
                WeaponType = ReadPublicPropertyInt(x, "weapon_type"),
                ArmorType = ReadPublicPropertyInt(x, "armor_type"),
                NotDroppable = ReadPublicBool(x, "not_droppable"),
                DestroyOnDeath = ReadPublicBool(x, "destroy_on_death"),
                RetainAfterUses = ReadPublicBool(x, "retain_after_uses"),
                UsesConsumedOnUse = ReadPublicBool(x, "uses_consumed_on_use"),
                MaximumUses = ReadPublicInt(x, "max_uses"),
                RechargeAtCleanup = ReadAbilities(x).GetValueOrDefault(121),
                GameLimit = ReadPublicInt(x, "game_limit"),
                CurrentInstances = ReadPublicInt(x, "current_instances"),
                Robable = ReadPublicPropertyBool(x, "robable"),
                OpenRewards = ReadOpenRewards(x),
                NegatedAbilityIds = ReadNegatedAbilities(x),
                EquipmentAbilityModifiers = ReadAbilities(x)
            };
        }
    }

    private void LoadEquipment(string path)
    {
        using var document = LoadProjection(path);
        foreach (var x in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var id = S(x, "item_id");
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Generated equipment projection contains a record without item_id.");
            _equipment[id] = new(id, S(x, "slot") ?? "NONE", B(x, "two_handed"), B(x, "class_restricted"), B(x, "race_restricted"), I(x, "minimum_level"), I(x, "maximum_level"), I(x, "minimum_strength"), S(x, "alignment"), S(x, "gender"));
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


    private static int EconomyWeight(JsonElement item)
    {
        if (!item.TryGetProperty("economy", out var economy) || economy.ValueKind != JsonValueKind.Object || !economy.TryGetProperty("weight", out var weight)) return 0;
        if (weight.ValueKind == JsonValueKind.Number && weight.TryGetInt32(out var numeric)) return numeric;
        if (weight.ValueKind == JsonValueKind.String && int.TryParse(weight.GetString(), out numeric)) return numeric;
        throw new InvalidOperationException($"Generated item '{S(item, "id")}' has a non-numeric weight.");
    }



    private static JsonElement PublicFields(JsonElement item)
    {
        if (item.TryGetProperty("public_reference_0139", out var reference) && reference.ValueKind == JsonValueKind.Object &&
            reference.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object) return fields;
        return default;
    }

    private static int ReadPublicInt(JsonElement item, string name) => PublicFields(item) is var f && f.ValueKind == JsonValueKind.Object && f.TryGetProperty(name, out var v) ? ReadInt(v) : 0;
    private static long ReadPublicLong(JsonElement item, string name)
    {
        var f=PublicFields(item); if(f.ValueKind!=JsonValueKind.Object||!f.TryGetProperty(name,out var v)) return 0;
        if(v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out var n)) return n;
        return v.ValueKind==JsonValueKind.String&&long.TryParse(v.GetString(),out n)?n:0;
    }
    private static bool ReadPublicBool(JsonElement item, string name) => PublicFields(item) is var f && f.ValueKind == JsonValueKind.Object && f.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    private static int ReadPublicPropertyInt(JsonElement item,string name)
    { var f=PublicFields(item); return f.ValueKind==JsonValueKind.Object&&f.TryGetProperty("properties",out var p)&&p.ValueKind==JsonValueKind.Object&&p.TryGetProperty(name,out var v)?ReadInt(v):0; }
    private static bool ReadPublicPropertyBool(JsonElement item,string name)
    { var f=PublicFields(item); return f.ValueKind==JsonValueKind.Object&&f.TryGetProperty("properties",out var p)&&p.ValueKind==JsonValueKind.Object&&p.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.True; }
    private static IReadOnlySet<int> ReadNegatedAbilities(JsonElement item)
    {
        var result=new HashSet<int>(); var f=PublicFields(item);
        if(f.ValueKind==JsonValueKind.Object&&f.TryGetProperty("properties",out var p)&&p.ValueKind==JsonValueKind.Object&&p.TryGetProperty("negate",out var a)&&a.ValueKind==JsonValueKind.Array)
            foreach(var v in a.EnumerateArray()){var id=ReadInt(v);if(id>0)result.Add(id);}
        return result;
    }
    private static RuntimeCurrencyBundle ReadOpenRewards(JsonElement item)
    {
        var f=PublicFields(item); if(f.ValueKind!=JsonValueKind.Object||!f.TryGetProperty("open_rewards",out var r)||r.ValueKind!=JsonValueKind.Object) return RuntimeCurrencyBundle.Zero;
        return new RuntimeCurrencyBundle(I(r,"copper"),I(r,"silver"),I(r,"gold"),I(r,"platinum"),I(r,"runic"));
    }

    private static IReadOnlyDictionary<int, int> ReadAbilities(JsonElement item)
    {
        var result = new Dictionary<int, int>();
        if (!item.TryGetProperty("public_reference_0139", out var reference) || reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array) return result;
        foreach (var ability in abilities.EnumerateArray())
        {
            if (!ability.TryGetProperty("ability_id", out var idElement)) continue;
            var id = ReadInt(idElement);
            if (id <= 0 || !ability.TryGetProperty("value", out var valueElement)) continue;
            result[id] = ReadInt(valueElement);
        }
        return result;
    }

    private static bool HasAbility(JsonElement item, int abilityId)
    {
        if (!item.TryGetProperty("public_reference_0139", out var reference) || reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array) return false;
        foreach (var ability in abilities.EnumerateArray())
        {
            if (ability.TryGetProperty("ability_id", out var id) && ReadInt(id) == abilityId) return true;
        }
        return false;
    }

    private static bool B(JsonElement e, string n) => e.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.True;
    private static int I(JsonElement e, string n) => e.TryGetProperty(n, out var x) ? ReadInt(x) : 0;
    private static int ReadInt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric)) return numeric;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric)) return numeric;
        return 0;
    }
    private static string? S(JsonElement e, string n) => e.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
}

public sealed partial class RuntimeItemEngine
{
    private readonly RuntimeItemCatalog _catalog;
    private readonly RuntimeCharacterBundleCatalog? _characterAuthority;
    private Dictionary<string, RuntimeItemInstance> _items = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, RuntimeCorpseState> _corpses = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private Dictionary<string, int> _createdQuantities = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeItemEngine(RuntimeItemCatalog catalog, IEnumerable<RuntimeItemInstance>? initial = null, IEnumerable<RuntimeCorpseState>? corpses = null, RuntimeCharacterBundleCatalog? characterAuthority = null)
    {
        _catalog = catalog;
        _characterAuthority = characterAuthority;
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
    public IReadOnlyList<RuntimeItemInstance> Banked(string ownerId, string bankId) { lock (_gate) return _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) && Same(x.OwnerId, ownerId) && Same(x.LocationId, bankId)).ToArray(); }
    public IReadOnlyList<RuntimeItemInstance> MerchantStock(string merchantId) { lock (_gate) return _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase) && Same(x.OwnerId, merchantId) && Same(x.LocationId, merchantId)).ToArray(); }

    public RuntimeItemInstance Create(string canonicalId, string locationKind, string locationId, int quantity = 1, string? ownerId = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        lock (_gate)
        {
            var definition = _catalog.Require(canonicalId);
            if (!RuntimeItemLocations.All.Contains(locationKind)) throw new InvalidOperationException("Invalid item location kind.");
            if (quantity < 1 || (!definition.Stackable && quantity != 1)) throw new InvalidOperationException("Invalid stack quantity for generated item definition.");
            var item = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, ownerId, locationKind, locationId, null, definition.DefaultCharges, quantity, null, metadata);
            EnforceOwnerQuantityLimit(item, ownerId, quantity, null);
            AddCreatedUnique(item);
            if (locationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) && definition.Stackable)
                item = MergeIntoDestinationCore(item);
            EnsureValid();
            return item;
        }
    }

    public RuntimeItemInstance Add(RuntimeItemInstance item) => Atomic(() => { _catalog.Require(item.CanonicalItemId); EnforceOwnerQuantityLimit(item, item.OwnerId, item.StackQuantity, null); AddUnique(item); return item; });
    public RuntimeItemInstance Remove(string instanceId) => Atomic(() => RemoveCore(instanceId, administrativeOverride: false));
    public RuntimeItemDestructionResult Destroy(string instanceId, string ownerId) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, ownerId);
        EnforceDestructionRestrictions(item, administrativeOverride: false);
        var removed = RemoveCore(instanceId, administrativeOverride: false);
        return new RuntimeItemDestructionResult(removed.InstanceId, removed.CanonicalItemId, removed.OwnerId, removed.LocationKind, removed.LocationId, false);
    });
    public RuntimeItemDestructionResult AdministrativeRemove(string instanceId) => Atomic(() =>
    {
        var removed = RemoveCore(instanceId, administrativeOverride: true);
        return new RuntimeItemDestructionResult(removed.InstanceId, removed.CanonicalItemId, removed.OwnerId, removed.LocationKind, removed.LocationId, true);
    });
    public RuntimeItemAdministrativeInspection AdministrativeInspect(string instanceId)
    {
        lock (_gate)
        {
            var item = Require(instanceId);
            var descendants = CollectSubtreeIdsCore(instanceId).Where(x => !Same(x, instanceId)).Select(Require).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
            var issues = RuntimeItemValidator.Validate(_catalog, _items.Values, _corpses.Values).Issues.Where(x => Same(x.InstanceId, instanceId) || descendants.Any(d => Same(d.InstanceId, x.InstanceId))).ToArray();
            return new RuntimeItemAdministrativeInspection(item, _catalog.Require(item.CanonicalItemId), descendants, issues);
        }
    }
    public RuntimeItemAdministrativeMoveResult AdministrativeMove(string instanceId, string locationKind, string locationId, string? ownerId = null, string? equippedSlot = null) => Atomic(() =>
    {
        if (!RuntimeItemLocations.All.Contains(locationKind)) throw new InvalidOperationException("Administrative move requires a canonical item location kind.");
        if (string.IsNullOrWhiteSpace(locationId)) throw new InvalidOperationException("Administrative move requires a destination identity.");
        if (locationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
        {
            var parent = Require(locationId);
            if (!_catalog.Require(parent.CanonicalItemId).Container) throw new InvalidOperationException("Administrative container destination is not a portable container.");
            if (CollectSubtreeIdsCore(instanceId).Any(x => Same(x, locationId))) throw new InvalidOperationException("Administrative move would create a circular container hierarchy.");
            ownerId ??= parent.OwnerId;
            if (!Same(ownerId, parent.OwnerId)) throw new InvalidOperationException("Administrative container move owner must match the parent container owner.");
        }
        if (locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(equippedSlot)) throw new InvalidOperationException("Administrative equipment move requires an equipment slot.");
        if (!locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)) equippedSlot = null;
        if ((locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(ownerId)) throw new InvalidOperationException("Administrative owned-location move requires an owner identity.");
        if (locationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)) ownerId = null;
        var root = Require(instanceId);
        var previous = root;
        var subtree = CollectSubtreeIdsCore(instanceId);
        _items[instanceId] = root with { OwnerId = ownerId, LocationKind = locationKind, LocationId = locationId, EquippedSlot = equippedSlot };
        foreach (var descendantId in subtree.Where(x => !Same(x, instanceId)))
        {
            var descendant = Require(descendantId);
            _items[descendantId] = descendant with { OwnerId = ownerId, EquippedSlot = null };
        }
        return new RuntimeItemAdministrativeMoveResult(instanceId, previous.LocationKind, previous.LocationId, previous.OwnerId, locationKind, locationId, ownerId, subtree);
    });
    public RuntimeItemDurabilityResult AdministrativeRepair(string instanceId, int amount) => Atomic(() =>
    {
        var item = Require(instanceId);
        if (amount <= 0) throw new InvalidOperationException("Administrative repair amount must be greater than zero.");
        if (item.Durability is null) throw new InvalidOperationException("Administrative repair requires projected durability state.");
        var maximum = RequireMaximumDurability(item);
        var current = item.Durability.Value;
        var repaired = checked(Math.Min(maximum, current + amount));
        var metadata = CloneMetadata(item);
        if (repaired > 0) metadata.Remove("broken");
        _items[item.InstanceId] = item with { Durability = repaired, Metadata = metadata };
        return new RuntimeItemDurabilityResult(item.InstanceId, current, repaired, repaired == 0);
    });
    public RuntimeItemAdministrativeAuditResult AdministrativeAudit()
    {
        lock (_gate)
        {
            var runtime = RuntimeItemValidator.Validate(_catalog, _items.Values, _corpses.Values);
            var state = Snapshot();
            var limited = RuntimeItemLimitedReconciliationCoordinator.Validate(state);
            var containerHierarchies = _items.Values.Count(x => _catalog.Require(x.CanonicalItemId).Container);
            return new RuntimeItemAdministrativeAuditResult(runtime, limited, _items.Count, _corpses.Count, containerHierarchies);
        }
    }
    public RuntimeItemInstance Get(string instanceId, string characterId) => Atomic(() => TransferCore(instanceId, RuntimeItemLocations.Character, characterId, characterId, null, requireGettable: true));
    public IReadOnlyList<RuntimeItemInstance> GetAll(string roomId, string characterId) => Atomic(() => Ground(roomId).Where(x => _catalog.Require(x.CanonicalItemId).Gettable).Select(x => TransferCore(x.InstanceId, RuntimeItemLocations.Character, characterId, characterId, null, false)).ToArray());
    public RuntimeItemInstance Drop(string instanceId, string characterId, string roomId) => Atomic(() => { var item = RequireCarried(instanceId, characterId); EnforceTransferRestrictions(item, "drop"); return TransferCore(instanceId, RuntimeItemLocations.Room, roomId, null, null, false); });
    public RuntimeItemInstance Give(string instanceId, string fromCharacter, string toCharacter) => Atomic(() => { var item = RequireCarried(instanceId, fromCharacter); EnforceTransferRestrictions(item, "give"); EnforceOwnershipTransferRestrictions(item, toCharacter, "give"); return TransferCore(instanceId, RuntimeItemLocations.Character, toCharacter, toCharacter, null, false); });
    public RuntimeItemInstance MoveToNpc(string instanceId, string ownerId, string npcId) => Atomic(() => { var item = RequireOwned(instanceId, ownerId); EnforceOwnershipTransferRestrictions(item, npcId, "npc transfer"); return TransferCore(instanceId, RuntimeItemLocations.Npc, npcId, npcId, null, false); });
    public RuntimeItemInstance MoveToTrade(string instanceId, string ownerId, string tradeId) => Atomic(() => { var item = RequireCarried(instanceId, ownerId); EnforceTransferRestrictions(item, "trade"); return TransferCore(instanceId, RuntimeItemLocations.Trade, tradeId, ownerId, null, false); });
    public RuntimeItemInstance Deposit(string instanceId, string ownerId, string bankId) => Deposit(instanceId, ownerId, bankId, RequireInstance(instanceId).StackQuantity);
    public RuntimeItemInstance Deposit(string instanceId, string ownerId, string bankId, int quantity) => Atomic(() =>
    {
        var item = RequireCarried(instanceId, ownerId);
        EnforceTransferRestrictions(item, "bank");
        return TransferQuantityCore(item, quantity, RuntimeItemLocations.Bank, bankId, ownerId);
    });
    public RuntimeItemInstance Withdraw(string instanceId, string ownerId, string bankId) => Withdraw(instanceId, ownerId, bankId, RequireInstance(instanceId).StackQuantity);
    public RuntimeItemInstance Withdraw(string instanceId, string ownerId, string bankId, int quantity) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, ownerId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, bankId)) throw new InvalidOperationException("Item is not stored in the requested bank.");
        return TransferQuantityCore(item, quantity, RuntimeItemLocations.Character, ownerId, ownerId);
    });
    public RuntimeItemInstance SellToMerchant(string instanceId, string ownerId, string merchantId) => SellToMerchant(instanceId, ownerId, merchantId, RequireInstance(instanceId).StackQuantity);
    public RuntimeItemInstance SellToMerchant(string instanceId, string ownerId, string merchantId, int quantity) => Atomic(() =>
    {
        var item = RequireCarried(instanceId, ownerId);
        EnforceTransferRestrictions(item, "sell");
        EnforceOwnershipTransferRestrictions(item, merchantId, "merchant sale");
        return TransferQuantityCore(item, quantity, RuntimeItemLocations.Merchant, merchantId, merchantId);
    });
    public RuntimeItemInstance BuyFromMerchant(string instanceId, string merchantId, string buyerId) => BuyFromMerchant(instanceId, merchantId, buyerId, RequireInstance(instanceId).StackQuantity);
    public RuntimeItemInstance BuyFromMerchant(string instanceId, string merchantId, string buyerId, int quantity) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, merchantId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, merchantId)) throw new InvalidOperationException("Item is not in the requested merchant stock.");
        EnforceOwnershipTransferRestrictions(item, buyerId, "merchant purchase");
        return TransferQuantityCore(item, quantity, RuntimeItemLocations.Character, buyerId, buyerId);
    });

    public RuntimeQuestItemGrantResult GrantQuestItem(string questId, string characterId, string canonicalId, string grantKey, int targetQuantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(questId)) throw new InvalidOperationException("Quest item grant requires a quest identity.");
        if (string.IsNullOrWhiteSpace(characterId)) throw new InvalidOperationException("Quest item grant requires a character identity.");
        if (string.IsNullOrWhiteSpace(grantKey)) throw new InvalidOperationException("Quest item grant requires an authoritative grant key.");
        if (targetQuantity < 0) throw new InvalidOperationException("Quest item grant target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        var projected = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["quest_id"] = questId;
        projected["quest_owner_id"] = characterId;
        projected["quest_grant_key"] = grantKey;
        projected["quest_grant_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matching = _items.Values
            .Where(x => Same(x.CanonicalItemId, definition.Id)
                && Same(x.OwnerId, characterId)
                && x.Metadata?.TryGetValue("quest_id", out var itemQuestId) == true && Same(itemQuestId, questId)
                && x.Metadata?.TryGetValue("quest_grant_key", out var itemGrantKey) == true && Same(itemGrantKey, grantKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("Quest item grant does not remove quantities above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, characterId, RuntimeItemLocations.Character, characterId, null, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            EnforceOwnerQuantityLimit(candidate, characterId, quantity, null);
            AddCreatedUnique(candidate);
            var reconciled = MergeIntoDestinationCore(candidate);
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values.Where(x => Same(x.CanonicalItemId, definition.Id)
            && Same(x.OwnerId, characterId)
            && x.Metadata?.TryGetValue("quest_id", out var itemQuestId) == true && Same(itemQuestId, questId)
            && x.Metadata?.TryGetValue("quest_grant_key", out var itemGrantKey) == true && Same(itemGrantKey, grantKey)).Sum(x => x.StackQuantity);
        return new RuntimeQuestItemGrantResult(questId, characterId, definition.Id, grantKey, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeQuestItemConsumptionResult ConsumeQuestItem(string questId, string characterId, string canonicalId, string consumeKey, int quantity) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(questId)) throw new InvalidOperationException("Quest item consumption requires a quest identity.");
        if (string.IsNullOrWhiteSpace(characterId)) throw new InvalidOperationException("Quest item consumption requires a character identity.");
        if (string.IsNullOrWhiteSpace(consumeKey)) throw new InvalidOperationException("Quest item consumption requires an authoritative consume key.");
        if (quantity <= 0) throw new InvalidOperationException("Quest item consumption quantity must be positive.");
        var definition = _catalog.Require(canonicalId);
        var candidates = _items.Values
            .Where(x => Same(x.CanonicalItemId, definition.Id)
                && Same(x.OwnerId, characterId)
                && (x.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase))
                && x.Metadata?.TryGetValue("quest_id", out var itemQuestId) == true && Same(itemQuestId, questId))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Sum(x => x.StackQuantity) < quantity) throw new InvalidOperationException("Quest item consumption quantity exceeds the character-owned quest quantity.");
        var remaining = quantity;
        var removed = new List<string>();
        var changed = new List<string>();
        foreach (var item in candidates)
        {
            if (remaining == 0) break;
            var take = Math.Min(remaining, item.StackQuantity);
            if (take == item.StackQuantity)
            {
                RemoveCore(item.InstanceId, administrativeOverride: true);
                removed.Add(item.InstanceId);
            }
            else
            {
                _items[item.InstanceId] = item with { StackQuantity = item.StackQuantity - take };
                changed.Add(item.InstanceId);
            }
            remaining -= take;
        }
        return new RuntimeQuestItemConsumptionResult(questId, characterId, definition.Id, consumeKey, quantity, quantity, removed, changed);
    });

    public RuntimeQuestItemCleanupResult ReconcileQuestItemCleanup(string questId, string characterId, string canonicalId, string cleanupKey, string lifecycleReason, int targetQuantity) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(questId)) throw new InvalidOperationException("Quest item cleanup requires a quest identity.");
        if (string.IsNullOrWhiteSpace(characterId)) throw new InvalidOperationException("Quest item cleanup requires a character identity.");
        if (string.IsNullOrWhiteSpace(cleanupKey)) throw new InvalidOperationException("Quest item cleanup requires an authoritative cleanup key.");
        if (string.IsNullOrWhiteSpace(lifecycleReason)) throw new InvalidOperationException("Quest item cleanup requires an authoritative lifecycle reason.");
        if (targetQuantity < 0) throw new InvalidOperationException("Quest item cleanup target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        var candidates = _items.Values
            .Where(x => Same(x.CanonicalItemId, definition.Id)
                && Same(x.OwnerId, characterId)
                && (x.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase))
                && x.Metadata?.TryGetValue("quest_id", out var itemQuestId) == true && Same(itemQuestId, questId))
            .OrderByDescending(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = candidates.Sum(x => x.StackQuantity);
        if (existing <= targetQuantity) return new RuntimeQuestItemCleanupResult(questId, characterId, definition.Id, cleanupKey, lifecycleReason, targetQuantity, existing, 0, existing, Array.Empty<string>(), Array.Empty<string>());
        var remainingToRemove = existing - targetQuantity;
        var removed = new List<string>();
        var changed = new List<string>();
        foreach (var item in candidates)
        {
            if (remainingToRemove == 0) break;
            var take = Math.Min(remainingToRemove, item.StackQuantity);
            if (take == item.StackQuantity)
            {
                if (item.Metadata?.TryGetValue("quest_cleanup_protected", out var protectedValue) == true && bool.TryParse(protectedValue, out var cleanupProtected) && cleanupProtected)
                    throw new InvalidOperationException("Quest item cleanup cannot remove an explicitly protected item.");
                RemoveCore(item.InstanceId, administrativeOverride: true);
                removed.Add(item.InstanceId);
            }
            else
            {
                _items[item.InstanceId] = item with { StackQuantity = item.StackQuantity - take };
                changed.Add(item.InstanceId);
            }
            remainingToRemove -= take;
        }
        var finalQuantity = existing - (existing - targetQuantity);
        return new RuntimeQuestItemCleanupResult(questId, characterId, definition.Id, cleanupKey, lifecycleReason, targetQuantity, existing, existing - targetQuantity, finalQuantity, removed, changed);
    });


    public RuntimeEventItemGrantResult GrantEventItem(string eventId, string characterId, string canonicalId, string rewardKey, int targetQuantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(eventId)) throw new InvalidOperationException("Event item reward requires an event identity.");
        if (string.IsNullOrWhiteSpace(characterId)) throw new InvalidOperationException("Event item reward requires a character identity.");
        if (string.IsNullOrWhiteSpace(rewardKey)) throw new InvalidOperationException("Event item reward requires an authoritative reward key.");
        if (targetQuantity < 0) throw new InvalidOperationException("Event item reward target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        var projected = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["event_id"] = eventId;
        projected["event_owner_id"] = characterId;
        projected["event_reward_key"] = rewardKey;
        projected["event_reward_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matching = _items.Values
            .Where(x => Same(x.CanonicalItemId, definition.Id)
                && Same(x.OwnerId, characterId)
                && x.Metadata?.TryGetValue("event_id", out var itemEventId) == true && Same(itemEventId, eventId)
                && x.Metadata?.TryGetValue("event_reward_key", out var itemRewardKey) == true && Same(itemRewardKey, rewardKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("Event item reward does not remove quantities above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, characterId, RuntimeItemLocations.Character, characterId, null, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            EnforceOwnerQuantityLimit(candidate, characterId, quantity, null);
            AddCreatedUnique(candidate);
            var reconciled = MergeIntoDestinationCore(candidate);
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values.Where(x => Same(x.CanonicalItemId, definition.Id)
            && Same(x.OwnerId, characterId)
            && x.Metadata?.TryGetValue("event_id", out var itemEventId) == true && Same(itemEventId, eventId)
            && x.Metadata?.TryGetValue("event_reward_key", out var itemRewardKey) == true && Same(itemRewardKey, rewardKey)).Sum(x => x.StackQuantity);
        return new RuntimeEventItemGrantResult(eventId, characterId, definition.Id, rewardKey, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeEventItemCleanupResult ReconcileEventItemCleanup(string eventId, string characterId, string canonicalId, string cleanupKey, string lifecycleReason, int targetQuantity) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(eventId)) throw new InvalidOperationException("Event item cleanup requires an event identity.");
        if (string.IsNullOrWhiteSpace(characterId)) throw new InvalidOperationException("Event item cleanup requires a character identity.");
        if (string.IsNullOrWhiteSpace(cleanupKey)) throw new InvalidOperationException("Event item cleanup requires an authoritative cleanup key.");
        if (string.IsNullOrWhiteSpace(lifecycleReason)) throw new InvalidOperationException("Event item cleanup requires an authoritative lifecycle reason.");
        if (targetQuantity < 0) throw new InvalidOperationException("Event item cleanup target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        var candidates = _items.Values
            .Where(x => Same(x.CanonicalItemId, definition.Id)
                && Same(x.OwnerId, characterId)
                && (x.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
                    || x.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase))
                && x.Metadata?.TryGetValue("event_id", out var itemEventId) == true && Same(itemEventId, eventId))
            .OrderByDescending(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = candidates.Sum(x => x.StackQuantity);
        if (existing <= targetQuantity) return new RuntimeEventItemCleanupResult(eventId, characterId, definition.Id, cleanupKey, lifecycleReason, targetQuantity, existing, 0, existing, Array.Empty<string>(), Array.Empty<string>());
        var remainingToRemove = existing - targetQuantity;
        var removed = new List<string>();
        var changed = new List<string>();
        foreach (var item in candidates)
        {
            if (remainingToRemove == 0) break;
            var take = Math.Min(remainingToRemove, item.StackQuantity);
            if (item.Metadata?.TryGetValue("event_cleanup_protected", out var protectedValue) == true && bool.TryParse(protectedValue, out var cleanupProtected) && cleanupProtected)
                throw new InvalidOperationException("Event item cleanup cannot remove an explicitly protected item.");
            if (take == item.StackQuantity)
            {
                RemoveCore(item.InstanceId, administrativeOverride: true);
                removed.Add(item.InstanceId);
            }
            else
            {
                _items[item.InstanceId] = item with { StackQuantity = item.StackQuantity - take };
                changed.Add(item.InstanceId);
            }
            remainingToRemove -= take;
        }
        return new RuntimeEventItemCleanupResult(eventId, characterId, definition.Id, cleanupKey, lifecycleReason, targetQuantity, existing, existing - targetQuantity, targetQuantity, removed, changed);
    });

    public RuntimeItemMerchantRestockResult RestockMerchantStock(string canonicalId, string merchantId, string restockKey, int targetQuantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(merchantId)) throw new InvalidOperationException("Merchant restock requires a merchant identity.");
        if (string.IsNullOrWhiteSpace(restockKey)) throw new InvalidOperationException("Merchant restock requires an authoritative restock key.");
        if (targetQuantity < 0) throw new InvalidOperationException("Merchant restock target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        var projected = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["merchant_restock_key"] = restockKey;
        projected["merchant_restock_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matching = _items.Values
            .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase)
                && Same(x.LocationId, merchantId)
                && Same(x.OwnerId, merchantId)
                && Same(x.CanonicalItemId, definition.Id)
                && x.Metadata?.TryGetValue("merchant_restock_key", out var key) == true
                && Same(key, restockKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("Merchant restock does not remove stock above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, merchantId, RuntimeItemLocations.Merchant, merchantId, null, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            AddCreatedUnique(candidate);
            var reconciled = MergeIntoDestinationCore(candidate);
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values
            .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase)
                && Same(x.LocationId, merchantId)
                && Same(x.OwnerId, merchantId)
                && Same(x.CanonicalItemId, definition.Id)
                && x.Metadata?.TryGetValue("merchant_restock_key", out var key) == true
                && Same(key, restockKey))
            .Sum(x => x.StackQuantity);
        return new RuntimeItemMerchantRestockResult(merchantId, definition.Id, restockKey, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeRoomItemResetResult ResetRoomItem(string canonicalId, string roomId, string resetKey, int targetQuantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new InvalidOperationException("Room item reset requires a room identity.");
        if (string.IsNullOrWhiteSpace(resetKey)) throw new InvalidOperationException("Room item reset requires an authoritative reset key.");
        if (targetQuantity < 0) throw new InvalidOperationException("Room item reset target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        var projected = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["room_reset_key"] = resetKey;
        projected["room_reset_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matching = _items.Values
            .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && Same(x.LocationId, roomId)
                && x.OwnerId is null
                && Same(x.CanonicalItemId, definition.Id)
                && x.Metadata?.TryGetValue("room_reset_key", out var key) == true
                && Same(key, resetKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("Room item reset does not remove instances above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, null, RuntimeItemLocations.Room, roomId, null, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            AddCreatedUnique(candidate);
            var reconciled = MergeIntoDestinationCore(candidate);
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values
            .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && Same(x.LocationId, roomId)
                && x.OwnerId is null
                && Same(x.CanonicalItemId, definition.Id)
                && x.Metadata?.TryGetValue("room_reset_key", out var key) == true
                && Same(key, resetKey))
            .Sum(x => x.StackQuantity);
        return new RuntimeRoomItemResetResult(roomId, definition.Id, resetKey, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeContainerRestockResult RestockPortableContainer(string canonicalId, string containerInstanceId, string ownerId, string restockKey, int targetQuantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(containerInstanceId)) throw new InvalidOperationException("Container restock requires a container instance identity.");
        if (string.IsNullOrWhiteSpace(ownerId)) throw new InvalidOperationException("Container restock requires an authoritative owner identity.");
        if (string.IsNullOrWhiteSpace(restockKey)) throw new InvalidOperationException("Container restock requires an authoritative restock key.");
        if (targetQuantity < 0) throw new InvalidOperationException("Container restock target quantity cannot be negative.");
        var container = RequireOwned(containerInstanceId, ownerId);
        if (!_catalog.Require(container.CanonicalItemId).Container) throw new InvalidOperationException("Container restock target is not a generated container.");
        var definition = _catalog.Require(canonicalId);
        var projected = metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["container_restock_key"] = restockKey;
        projected["container_restock_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matching = _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
            && Same(x.LocationId, containerInstanceId) && Same(x.OwnerId, ownerId) && Same(x.CanonicalItemId, definition.Id)
            && x.Metadata?.TryGetValue("container_restock_key", out var key) == true && Same(key, restockKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("Container restock does not remove contents above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, ownerId, RuntimeItemLocations.Container, containerInstanceId, null, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            if (!definition.Stackable || !_items.Values.Any(x => CanMerge(x, candidate) && x.StackQuantity < MaximumStack(x))) EnforceCapacity(container);
            AddCreatedUnique(candidate);
            var reconciled = MergeIntoDestinationCore(candidate);
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
            && Same(x.LocationId, containerInstanceId) && Same(x.OwnerId, ownerId) && Same(x.CanonicalItemId, definition.Id)
            && x.Metadata?.TryGetValue("container_restock_key", out var key) == true && Same(key, restockKey)).Sum(x => x.StackQuantity);
        return new RuntimeContainerRestockResult(containerInstanceId, definition.Id, restockKey, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeNpcItemResetResult ResetNpcItem(string canonicalId, string npcId, string resetKey, int targetQuantity, string? equippedSlot = null, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(npcId)) throw new InvalidOperationException("NPC item reset requires an NPC identity.");
        if (string.IsNullOrWhiteSpace(resetKey)) throw new InvalidOperationException("NPC item reset requires an authoritative reset key.");
        if (targetQuantity < 0) throw new InvalidOperationException("NPC item reset target quantity cannot be negative.");
        var definition = _catalog.Require(canonicalId);
        if (!string.IsNullOrWhiteSpace(equippedSlot))
        {
            var equipment = _catalog.Equipment(definition.Id) ?? throw new InvalidOperationException("NPC equipment reset requires a generated equipment definition.");
            if (!_catalog.IsLegalSlot(equippedSlot) || !Same(equipment.Slot, equippedSlot)) throw new InvalidOperationException("NPC equipment reset slot is not legal for the item.");
            if (targetQuantity > 1) throw new InvalidOperationException("NPC equipment reset target cannot exceed one item per authoritative slot.");
        }
        var projected = metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["npc_reset_key"] = resetKey;
        projected["npc_reset_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(equippedSlot)) projected["npc_reset_equipped_slot"] = equippedSlot;
        var locationKind = string.IsNullOrWhiteSpace(equippedSlot) ? RuntimeItemLocations.Npc : RuntimeItemLocations.Equipment;
        var matching = _items.Values.Where(x => x.LocationKind.Equals(locationKind, StringComparison.OrdinalIgnoreCase)
            && Same(x.LocationId, npcId) && Same(x.OwnerId, npcId) && Same(x.CanonicalItemId, definition.Id)
            && Same(x.EquippedSlot, equippedSlot)
            && x.Metadata?.TryGetValue("npc_reset_key", out var key) == true && Same(key, resetKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("NPC item reset does not remove inventory or equipment above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable && string.IsNullOrWhiteSpace(equippedSlot) ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, npcId, locationKind, npcId, equippedSlot, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable && string.IsNullOrWhiteSpace(equippedSlot)) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            AddCreatedUnique(candidate);
            var reconciled = string.IsNullOrWhiteSpace(equippedSlot) ? MergeIntoDestinationCore(candidate) : candidate;
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values.Where(x => x.LocationKind.Equals(locationKind, StringComparison.OrdinalIgnoreCase)
            && Same(x.LocationId, npcId) && Same(x.OwnerId, npcId) && Same(x.CanonicalItemId, definition.Id)
            && Same(x.EquippedSlot, equippedSlot)
            && x.Metadata?.TryGetValue("npc_reset_key", out var key) == true && Same(key, resetKey)).Sum(x => x.StackQuantity);
        return new RuntimeNpcItemResetResult(npcId, definition.Id, resetKey, equippedSlot, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeNpcCorpseTransferResult CreateNpcCorpse(string npcId, string roomId, TimeSpan? decayAfter = null, string? lootOwnerId = null, TimeSpan? exclusiveLootFor = null) =>
        CreateNpcCorpseExact(Guid.NewGuid().ToString("N"), npcId, roomId, 0, DateTimeOffset.UtcNow, decayAfter, lootOwnerId, exclusiveLootFor);

    public RuntimeNpcCorpseTransferResult CreateNpcCorpseExact(string corpseId, string npcId, string roomId, long gold, DateTimeOffset createdAtUtc, TimeSpan? decayAfter = null, string? lootOwnerId = null, TimeSpan? exclusiveLootFor = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(corpseId)) throw new InvalidOperationException("Corpse identity is required for NPC corpse creation.");
        if (string.IsNullOrWhiteSpace(npcId)) throw new InvalidOperationException("NPC identity is required for corpse creation.");
        if (string.IsNullOrWhiteSpace(roomId)) throw new InvalidOperationException("Room identity is required for NPC corpse creation.");
        if (gold < 0) throw new InvalidOperationException("NPC corpse gold cannot be negative.");
        if (exclusiveLootFor is not null && exclusiveLootFor.Value < TimeSpan.Zero) throw new InvalidOperationException("Exclusive corpse-loot duration cannot be negative.");
        if (exclusiveLootFor is not null && string.IsNullOrWhiteSpace(lootOwnerId)) throw new InvalidOperationException("Exclusive corpse-loot duration requires an authoritative loot owner.");
        if (_corpses.ContainsKey(corpseId)) throw new InvalidOperationException("Corpse already exists.");
        var corpse = new RuntimeCorpseState(corpseId, npcId, roomId, gold, createdAtUtc, decayAfter is null ? null : createdAtUtc.Add(decayAfter.Value), false, string.IsNullOrWhiteSpace(lootOwnerId) ? null : lootOwnerId, exclusiveLootFor is null ? null : createdAtUtc.Add(exclusiveLootFor.Value), null);
        _corpses.Add(corpse.CorpseId, corpse);
        var roots = _items.Values.Where(x => Same(x.OwnerId, npcId) && !x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        var transferredRoots = new List<string>();
        var retained = new List<string>();
        var transferredCount = 0;
        foreach (var root in roots)
        {
            if (IsMetadataTrue(root, "npc_no_drop") || IsMetadataTrue(root, "no_loot"))
            {
                retained.Add(root.InstanceId);
                continue;
            }
            transferredCount += CountSubtree(root.InstanceId);
            TransferCore(root.InstanceId, RuntimeItemLocations.Corpse, corpse.CorpseId, null, null, false);
            SetContainedDescendantOwnerCore(root.InstanceId, null);
            transferredRoots.Add(root.InstanceId);
        }
        return new RuntimeNpcCorpseTransferResult(corpse.CorpseId, npcId, roomId, transferredRoots, transferredCount, retained);
    });

    public RuntimeNpcCorpseLootResult ReconcileNpcCorpseLoot(string corpseId, string canonicalId, string lootKey, int targetQuantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(corpseId)) throw new InvalidOperationException("NPC corpse loot requires a corpse identity.");
        if (string.IsNullOrWhiteSpace(lootKey)) throw new InvalidOperationException("NPC corpse loot requires an authoritative loot key.");
        if (targetQuantity < 0) throw new InvalidOperationException("NPC corpse loot target quantity cannot be negative.");
        var corpse = RequireCorpse(corpseId);
        if (corpse.CleanedUp) throw new InvalidOperationException("Cannot reconcile loot into a cleaned-up corpse.");
        var definition = _catalog.Require(canonicalId);
        var projected = metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        projected["npc_loot_key"] = lootKey;
        projected["npc_loot_target_quantity"] = targetQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matching = _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)
            && Same(x.LocationId, corpseId) && Same(x.CanonicalItemId, definition.Id)
            && x.Metadata?.TryGetValue("npc_loot_key", out var key) == true && Same(key, lootKey))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = matching.Sum(x => x.StackQuantity);
        if (existing > targetQuantity) throw new InvalidOperationException("NPC corpse loot reconciliation does not remove loot above the projected target.");
        var missing = targetQuantity - existing;
        var createdIds = new List<string>();
        while (missing > 0)
        {
            var quantity = definition.Stackable ? missing : 1;
            var candidate = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, null, RuntimeItemLocations.Corpse, corpseId, null, definition.DefaultCharges, quantity, null, projected);
            if (definition.Stackable) quantity = Math.Min(quantity, MaximumStack(candidate));
            candidate = candidate with { StackQuantity = quantity };
            AddCreatedUnique(candidate);
            var reconciled = MergeIntoDestinationCore(candidate);
            createdIds.Add(reconciled.InstanceId);
            missing -= quantity;
        }
        var finalQuantity = _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)
            && Same(x.LocationId, corpseId) && Same(x.CanonicalItemId, definition.Id)
            && x.Metadata?.TryGetValue("npc_loot_key", out var key) == true && Same(key, lootKey)).Sum(x => x.StackQuantity);
        return new RuntimeNpcCorpseLootResult(corpseId, definition.Id, lootKey, targetQuantity, existing, targetQuantity - existing, finalQuantity, createdIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    });

    public RuntimeNpcLootTableResult ReconcileNpcCorpseLootTable(string corpseId, string tableKey, int seed, IReadOnlyList<RuntimeNpcLootRollEntry> entries) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(corpseId)) throw new InvalidOperationException("NPC loot-table evaluation requires a corpse identity.");
        if (string.IsNullOrWhiteSpace(tableKey)) throw new InvalidOperationException("NPC loot-table evaluation requires an authoritative table key.");
        if (entries is null) throw new InvalidOperationException("NPC loot-table entries are required.");
        var corpse = RequireCorpse(corpseId);
        if (corpse.CleanedUp) throw new InvalidOperationException("Cannot evaluate loot for a cleaned-up corpse.");
        var duplicateKey = entries.GroupBy(x => x.LootKey, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1);
        if (duplicateKey is not null) throw new InvalidOperationException("NPC loot-table entry keys must be nonempty and unique within the table.");
        var outcomes = new List<RuntimeNpcLootRollOutcome>();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            _catalog.Require(entry.CanonicalItemId);
            if (entry.ChanceBasisPoints < 0 || entry.ChanceBasisPoints > 10000) throw new InvalidOperationException("NPC loot chance must be between 0 and 10000 basis points.");
            if (entry.MinimumQuantity < 0 || entry.MaximumQuantity < entry.MinimumQuantity) throw new InvalidOperationException("NPC loot quantity range is invalid.");
            var roll = DeterministicLootValue(corpseId, tableKey, seed, index, "chance") % 10000;
            var awarded = roll < entry.ChanceBasisPoints;
            var quantity = 0;
            IReadOnlyList<string> created = Array.Empty<string>();
            if (awarded && entry.MaximumQuantity > 0)
            {
                var width = checked(entry.MaximumQuantity - entry.MinimumQuantity + 1);
                quantity = entry.MinimumQuantity + (DeterministicLootValue(corpseId, tableKey, seed, index, "quantity") % width);
                if (quantity > 0)
                {
                    var key = $"{tableKey}:{entry.LootKey}";
                    var metadata = entry.Metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(entry.Metadata, StringComparer.OrdinalIgnoreCase);
                    metadata["npc_loot_table_key"] = tableKey;
                    metadata["npc_loot_seed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    metadata["npc_loot_roll_basis_points"] = roll.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var reconciled = ReconcileNpcCorpseLoot(corpseId, entry.CanonicalItemId, key, quantity, metadata);
                    created = reconciled.CreatedInstanceIds;
                }
            }
            outcomes.Add(new RuntimeNpcLootRollOutcome(entry.CanonicalItemId, entry.LootKey, roll, awarded, quantity, created));
        }
        return new RuntimeNpcLootTableResult(corpseId, tableKey, seed, outcomes);
    });

    public RuntimeNpcCorpseGoldResult ReconcileNpcCorpseGold(string corpseId, string goldKey, int seed, long minimumGold, long maximumGold) => Atomic(() =>
    {
        if (string.IsNullOrWhiteSpace(corpseId)) throw new InvalidOperationException("NPC corpse-gold reconciliation requires a corpse identity.");
        if (string.IsNullOrWhiteSpace(goldKey)) throw new InvalidOperationException("NPC corpse-gold reconciliation requires an authoritative gold key.");
        if (minimumGold < 0 || maximumGold < minimumGold) throw new InvalidOperationException("NPC corpse-gold range is invalid.");
        var corpse = RequireCorpse(corpseId);
        if (corpse.CleanedUp) throw new InvalidOperationException("Cannot reconcile gold into a cleaned-up corpse.");
        var width = checked(maximumGold - minimumGold + 1);
        var target = width == 1 ? minimumGold : minimumGold + (DeterministicLootValue(corpseId, goldKey, seed, 0, "gold") % width);
        var previous = corpse.Gold;
        if (previous != 0 && previous != target) throw new InvalidOperationException("NPC corpse-gold reconciliation does not overwrite conflicting existing gold.");
        var created = previous == 0 && target != 0;
        if (previous != target) _corpses[corpseId] = corpse with { Gold = target };
        return new RuntimeNpcCorpseGoldResult(corpseId, goldKey, seed, minimumGold, maximumGold, previous, target, target, created);
    });

    private static int DeterministicLootValue(string corpseId, string tableKey, int seed, int index, string purpose)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{corpseId}|{tableKey}|{seed}|{index}|{purpose}");
        var digest = SHA256.HashData(bytes);
        return (int)(BitConverter.ToUInt32(digest, 0) & 0x7fffffff);
    }

    public RuntimeItemInstance SplitStack(string instanceId, int quantity)
    {
        return Atomic(() =>
        {
            var source = Require(instanceId);
            var definition = _catalog.Require(source.CanonicalItemId);
            if (!definition.Stackable) throw new InvalidOperationException("Item definition is not stackable.");
            if (quantity <= 0 || quantity >= source.StackQuantity) throw new InvalidOperationException("Split quantity must be greater than zero and less than the source stack.");
            var remainder = Replace(source with { StackQuantity = source.StackQuantity - quantity });
            var split = source with { InstanceId = Guid.NewGuid().ToString("N"), StackQuantity = quantity };
            AddUnique(split);
            return split;
        });
    }

    public RuntimeItemInstance MergeStacks(string targetInstanceId, string sourceInstanceId)
    {
        return Atomic(() =>
        {
            if (Same(targetInstanceId, sourceInstanceId)) throw new InvalidOperationException("A stack cannot merge with itself.");
            var target = Require(targetInstanceId);
            var source = Require(sourceInstanceId);
            if (!CanMerge(target, source)) throw new InvalidOperationException("Item stacks do not have identical merge identity.");
            var total = checked(target.StackQuantity + source.StackQuantity);
            var maximum = MaximumStack(target);
            if (total > maximum) throw new InvalidOperationException($"Merged stack exceeds maximum quantity {maximum}.");
            _items.Remove(source.InstanceId);
            return Replace(target with { StackQuantity = total });
        });
    }

    public RuntimeItemInstance TransferQuantity(string instanceId, int quantity, string kind, string location, string? owner = null)
    {
        return Atomic(() =>
        {
            var source = Require(instanceId);
            if (quantity <= 0 || quantity > source.StackQuantity) throw new InvalidOperationException("Transfer quantity is outside the source stack.");
            if (quantity == source.StackQuantity) return TransferCore(instanceId, kind, location, owner, null, false);
            var split = SplitStackCore(source, quantity);
            return TransferCore(split.InstanceId, kind, location, owner, null, false);
        });
    }

    public RuntimeItemInstance MoveToContainer(string instanceId, string ownerId, string containerInstanceId) => Put(instanceId, ownerId, containerInstanceId);

    public RuntimeItemInstance Put(string instanceId, string ownerId, string containerInstanceId)
    {
        return Atomic(() =>
        {
            var item = RequireCarried(instanceId, ownerId);
            EnforceTransferRestrictions(item, "container");
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
            var item = RequireCarried(instanceId, characterId);
            if (IsMetadataTrue(item, "broken")) throw new InvalidOperationException("Broken item cannot be equipped without authoritative override evidence.");
            var relationship = _catalog.Equipment(item.CanonicalItemId) ?? throw new InvalidOperationException("Item has no generated equipment relationship.");
            var slot = requestedSlot ?? relationship.Slot;
            if (!_catalog.IsLegalSlot(slot) || !Same(slot, relationship.Slot)) throw new InvalidOperationException($"Illegal slot: generated slot is {relationship.Slot}.");
            EnforceAuthoritativeCharacterRequirements(item, character, relationship);
            var strength = character.BaseStats.TryGetValue("strength", out var strengthValue)
                ? strengthValue
                : character.BaseStats.TryGetValue("str", out var shortStrengthValue)
                    ? shortStrengthValue
                    : character.BaseStats.GetValueOrDefault("STR");
            if (relationship.MinimumStrength > strength) throw new InvalidOperationException("Strength restriction failed.");
            EnforceAlignmentAbilityRequirements(_catalog.Require(item.CanonicalItemId), character);
            if (!string.IsNullOrWhiteSpace(relationship.Alignment) && (!character.Conditions.TryGetValue("alignment", out var alignment) || !Same(alignment, relationship.Alignment))) throw new InvalidOperationException("Alignment restriction failed.");
            if (!string.IsNullOrWhiteSpace(relationship.Gender) && (!character.Conditions.TryGetValue("gender", out var gender) || !Same(gender, relationship.Gender))) throw new InvalidOperationException("Gender restriction failed.");
            if (_items.Values.Any(x => Same(x.OwnerId, characterId) && x.EquippedSlot is not null && Same(x.EquippedSlot, slot) && !Same(x.InstanceId, item.InstanceId))) throw new InvalidOperationException("Equipment slot is occupied.");
            EnforceHandRules(characterId, relationship, slot, item.InstanceId);
            return Replace(item with { LocationKind = RuntimeItemLocations.Equipment, LocationId = characterId, OwnerId = characterId, EquippedSlot = slot });
        });
    }

    internal static void EnforceAlignmentAbilityRequirements(RuntimeItemDefinition definition, RuntimeCharacterRecord character)
    {
        var abilities = definition.EquipmentAbilityModifiers;
        if (!abilities.ContainsKey(97) && !abilities.ContainsKey(98) && !abilities.ContainsKey(110) &&
            !abilities.ContainsKey(111) && !abilities.ContainsKey(112) && !abilities.ContainsKey(113)) return;

        var reputation = character.SemanticState?.Alignment?.Reputation;
        if (string.IsNullOrWhiteSpace(reputation) && character.Conditions.TryGetValue("alignment", out var legacyAlignment))
            reputation = legacyAlignment;
        if (string.IsNullOrWhiteSpace(reputation))
            throw new InvalidOperationException("Alignment restriction cannot be evaluated because the character has no authoritative reputation.");

        var good = reputation.Equals("Lawful", StringComparison.OrdinalIgnoreCase) ||
                   reputation.Equals("Saint", StringComparison.OrdinalIgnoreCase) ||
                   reputation.Equals("Good", StringComparison.OrdinalIgnoreCase);
        var neutral = reputation.Equals("Neutral", StringComparison.OrdinalIgnoreCase) ||
                      reputation.Equals("Seedy", StringComparison.OrdinalIgnoreCase);
        var evil = reputation.Equals("Outlaw", StringComparison.OrdinalIgnoreCase) ||
                   reputation.Equals("Criminal", StringComparison.OrdinalIgnoreCase) ||
                   reputation.Equals("Villain", StringComparison.OrdinalIgnoreCase) ||
                   reputation.Equals("Fiend", StringComparison.OrdinalIgnoreCase);
        if (!good && !neutral && !evil)
            throw new InvalidOperationException($"Unknown decoded alignment reputation '{reputation}'.");

        if (abilities.ContainsKey(97) && !good) throw new InvalidOperationException("Item is usable only by Good-aligned characters.");
        if (abilities.ContainsKey(98) && !evil) throw new InvalidOperationException("Item is usable only by Evil-aligned characters.");
        if (abilities.ContainsKey(112) && !neutral) throw new InvalidOperationException("Item is usable only by Neutral-aligned characters.");
        if (abilities.ContainsKey(110) && good) throw new InvalidOperationException("Item cannot be used by Good-aligned characters.");
        if (abilities.ContainsKey(111) && evil) throw new InvalidOperationException("Item cannot be used by Evil-aligned characters.");
        if (abilities.ContainsKey(113) && neutral) throw new InvalidOperationException("Item cannot be used by Neutral-aligned characters.");
    }


    private void EnforceAuthoritativeCharacterRequirements(RuntimeItemInstance item, RuntimeCharacterRecord character, RuntimeEquipmentDefinition relationship)
    {
        if (relationship.MinimumLevel > character.Level) throw new InvalidOperationException($"Requires level {relationship.MinimumLevel}.");
        if (relationship.MaximumLevel > 0 && character.Level > relationship.MaximumLevel) throw new InvalidOperationException($"Maximum eligible level is {relationship.MaximumLevel}.");
        if (_characterAuthority is not null)
        {
            var decision = _characterAuthority.ItemRequirements.EvaluateCharacterInputs(character, item.CanonicalItemId);
            if (!decision.Allowed) throw new InvalidOperationException(decision.Reason);

            if (item.CanonicalItemId.Equals("item.1334", StringComparison.OrdinalIgnoreCase))
            {
                var draka = _characterAuthority.WitchunterQuest.EvaluateDrakaBladeUse(character, _characterAuthority.QuestAbilities);
                if (!draka.Eligible) throw new InvalidOperationException(draka.Reason);
            }

            var weapon = _characterAuthority.ClassCapabilities.CanUseWeaponType(character.ClassId, decision.Requirement.WeaponType);
            if (!weapon.Allowed && !weapon.EvidenceStatus.Equals("relationship_unresolved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(weapon.Reason);

            var armour = _characterAuthority.ClassCapabilities.CanUseArmourType(character.ClassId, decision.Requirement.ArmourType);
            if (!armour.Allowed && !armour.EvidenceStatus.Equals("relationship_unresolved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(armour.Reason);
            return;
        }

        // Compatibility fallback for isolated tests and non-Classic callers that do not
        // supply the authoritative Bundle A catalog. Live Classic gameplay always does.
        // The decoded level range was already enforced above.
    }

    public RuntimeItemInstance Wear(string instanceId, string characterId, RuntimeCharacterRecord character, string? requestedSlot = null) => Equip(instanceId, characterId, character, requestedSlot);
    public RuntimeItemInstance Unequip(string instanceId, string characterId) => Atomic(() => { var item = RequireOwned(instanceId, characterId); if (item.EquippedSlot is null) throw new InvalidOperationException("Item is not equipped."); if (_catalog.Require(item.CanonicalItemId).CursedWhenEquipped || IsMetadataTrue(item, "no_remove") || IsMetadataTrue(item, "cursed_no_remove")) throw new InvalidOperationException("Item cannot be removed according to projected instance restrictions."); return TransferCore(instanceId, RuntimeItemLocations.Character, characterId, characterId, null, false); });
    public RuntimeItemInstance RemoveEquipped(string instanceId, string characterId) => Unequip(instanceId, characterId);

    public RuntimeCorpseState CreateCorpse(string characterId, string roomId, long gold, TimeSpan? decayAfter = null) =>
        CreateCorpse(characterId, roomId, gold, decayAfter, null, null);

    public RuntimeCorpseState CreateCorpse(string characterId, string roomId, long gold, TimeSpan? decayAfter, string? lootOwnerId, TimeSpan? exclusiveLootFor)
    {
        return Atomic(() =>
        {
            if (exclusiveLootFor is not null && exclusiveLootFor.Value < TimeSpan.Zero) throw new InvalidOperationException("Exclusive corpse-loot duration cannot be negative.");
            if (exclusiveLootFor is not null && string.IsNullOrWhiteSpace(lootOwnerId)) throw new InvalidOperationException("Exclusive corpse-loot duration requires an authoritative loot owner.");
            var now = DateTimeOffset.UtcNow;
            var corpse = new RuntimeCorpseState(
                Guid.NewGuid().ToString("N"),
                characterId,
                roomId,
                gold,
                now,
                decayAfter is null ? null : now.Add(decayAfter.Value),
                false,
                string.IsNullOrWhiteSpace(lootOwnerId) ? null : lootOwnerId,
                exclusiveLootFor is null ? null : now.Add(exclusiveLootFor.Value),
                characterId);
            _corpses.Add(corpse.CorpseId, corpse);
            var roots = _items.Values
                .Where(x => Same(x.OwnerId, characterId)
                    && !x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
                    && !IsDeathProtected(x))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var root in roots)
            {
                if (!string.IsNullOrWhiteSpace(root.EquippedSlot))
                {
                    var metadata = root.Metadata is null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(root.Metadata, StringComparer.OrdinalIgnoreCase);
                    metadata["death_original_equipped_slot"] = root.EquippedSlot;
                    Replace(root with { Metadata = metadata });
                }
                TransferCore(root.InstanceId, RuntimeItemLocations.Corpse, corpse.CorpseId, null, null, false);
                SetContainedDescendantOwnerCore(root.InstanceId, null);
            }
            return corpse;
        });
    }

    public RuntimeItemInstance Loot(string instanceId, string corpseId, string characterId) => Loot(instanceId, corpseId, characterId, null);

    public RuntimeItemInstance Loot(string instanceId, string corpseId, string characterId, int? quantity) => Atomic(() =>
    {
        var corpse = RequireCorpse(corpseId);
        EnforceCorpseLootAccess(corpse, characterId, DateTimeOffset.UtcNow);
        var item = Require(instanceId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, corpseId)) throw new InvalidOperationException("Item is not in that corpse.");
        var requested = quantity ?? item.StackQuantity;
        if (requested <= 0 || requested > item.StackQuantity) throw new InvalidOperationException("Corpse loot quantity is outside the source stack.");
        if (requested != item.StackQuantity && !_catalog.Require(item.CanonicalItemId).Stackable) throw new InvalidOperationException("Partial corpse loot requires a stackable item definition.");
        var transferSource = requested == item.StackQuantity ? item : SplitStackCore(item, requested);
        var transferred = TransferCore(transferSource.InstanceId, RuntimeItemLocations.Character, characterId, characterId, null, false);
        SetContainedDescendantOwnerCore(transferred.InstanceId, characterId);
        return MergeIntoDestinationCore(transferred);
    });
    public long LootGold(string corpseId) => LootGold(corpseId, null, DateTimeOffset.UtcNow);
    public long LootGold(string corpseId, string characterId) => LootGold(corpseId, characterId, DateTimeOffset.UtcNow);
    public long LootGold(string corpseId, string? characterId, DateTimeOffset now) => Atomic(() =>
    {
        var corpse = RequireCorpse(corpseId);
        EnforceCorpseLootAccess(corpse, characterId, now);
        _corpses[corpseId] = corpse with { Gold = 0 };
        return corpse.Gold;
    });

    public bool CanLootCorpse(string corpseId, string characterId, DateTimeOffset now)
    {
        lock (_gate)
        {
            var corpse = RequireCorpse(corpseId);
            return HasCorpseLootAccess(corpse, characterId, now);
        }
    }

    public RuntimeCorpseRestorationResult RestoreCorpseItems(string corpseId, string characterId) => RestoreCorpseForResurrection(corpseId, characterId, false, false, null);

    public RuntimeCorpseRestorationResult RestoreCorpseForResurrection(string corpseId, string characterId, bool restoreGold) =>
        RestoreCorpseForResurrection(corpseId, characterId, restoreGold, false, null);

    public RuntimeCorpseRestorationResult RestoreCorpseForResurrection(string corpseId, string characterId, bool restoreGold, bool restoreEquipment, RuntimeCharacterRecord? character) =>
        RestoreCorpseForResurrection(corpseId, characterId, restoreGold, restoreEquipment, character, false);

    public RuntimeCorpseRestorationResult RestoreCorpseForResurrection(string corpseId, string characterId, bool restoreGold, bool restoreEquipment, RuntimeCharacterRecord? character, bool displaceEquipmentConflicts) => Atomic(() =>
    {
        var corpse = RequireCorpse(corpseId);
        if (!Same(corpse.CharacterId, characterId)) throw new InvalidOperationException("Only the corpse character can receive resurrection restoration.");
        if (restoreEquipment && character is null) throw new InvalidOperationException("Equipment restoration requires an authoritative character record.");
        var roots = _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, corpseId)).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        var restoredRoots = new List<string>();
        var displacedEquipment = new List<string>();
        var restoredCount = 0;
        foreach (var root in roots)
        {
            var subtreeCount = CollectSubtreeIdsCore(root.InstanceId).Count;
            var originalSlot = root.Metadata?.TryGetValue("death_original_equipped_slot", out var projectedSlot) == true && !string.IsNullOrWhiteSpace(projectedSlot)
                ? projectedSlot
                : null;
            var restored = TransferCore(root.InstanceId, RuntimeItemLocations.Character, characterId, characterId, null, false);
            SetContainedDescendantOwnerCore(restored.InstanceId, characterId);
            if (originalSlot is not null)
            {
                var metadata = restored.Metadata is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(restored.Metadata, StringComparer.OrdinalIgnoreCase);
                metadata.Remove("death_original_equipped_slot");
                restored = Replace(restored with { Metadata = metadata.Count == 0 ? null : metadata });
            }
            var reconciled = MergeIntoDestinationCore(restored);
            if (restoreEquipment && originalSlot is not null)
            {
                if (displaceEquipmentConflicts)
                    displacedEquipment.AddRange(DisplaceEquipmentConflictsForRestorationCore(characterId, reconciled.InstanceId, originalSlot));
                reconciled = Equip(reconciled.InstanceId, characterId, character!, originalSlot);
            }
            restoredRoots.Add(reconciled.InstanceId);
            restoredCount = checked(restoredCount + subtreeCount);
        }
        var restoredGold = restoreGold ? corpse.Gold : 0;
        if (restoreGold && corpse.Gold != 0) corpse = corpse with { Gold = 0 };
        var finalized = !_items.Values.Any(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, corpseId)) && corpse.Gold == 0;
        _corpses[corpseId] = finalized
            ? corpse with { CleanedUp = true, LimitedItemReservationOwnerId = null }
            : corpse;
        return new RuntimeCorpseRestorationResult(corpseId, characterId, restoredRoots, restoredCount, restoredGold, finalized, displacedEquipment);
    });

    public int CleanupExpiredCorpses(DateTimeOffset now) => Atomic(() =>
    {
        var expired = _corpses.Values.Where(x => !x.CleanedUp && x.DecaysAtUtc <= now).ToArray();
        foreach (var corpse in expired)
        {
            var roots = _items.Values
                .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, corpse.CorpseId))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var root in roots) foreach (var id in CollectSubtreeIdsCore(root.InstanceId)) _items.Remove(id);
            _corpses[corpse.CorpseId] = corpse with { CleanedUp = true, Gold = 0 };
        }
        return expired.Length;
    });

    public RuntimeItemInstance SpendCharges(string instanceId, string ownerId, int charges = 1)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, ownerId);
            if (charges <= 0) throw new InvalidOperationException("Charge cost must be greater than zero.");
            if (item.Charges < charges) throw new InvalidOperationException("Item does not have enough charges.");
            return Replace(item with { Charges = item.Charges - charges });
        });
    }

    public RuntimeItemInstance Recharge(string instanceId, string ownerId, int charges)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, ownerId);
            if (charges <= 0) throw new InvalidOperationException("Recharge quantity must be greater than zero.");
            var maximum = MaximumCharges(item);
            var total = checked(item.Charges + charges);
            if (total > maximum) throw new InvalidOperationException($"Recharge exceeds maximum charges {maximum}.");
            return Replace(item with { Charges = total });
        });
    }

    public RuntimeItemConsumptionResult ConsumeQuantity(string instanceId, string ownerId, int quantity = 1)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, ownerId);
            var definition = _catalog.Require(item.CanonicalItemId);
            if (!definition.Consumable) throw new InvalidOperationException("Item definition is not consumable.");
            if (quantity <= 0 || quantity > item.StackQuantity) throw new InvalidOperationException("Consumption quantity is outside the item stack.");
            var remaining = item.StackQuantity - quantity;
            if (remaining == 0)
            {
                if (_items.Values.Any(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, item.InstanceId))) throw new InvalidOperationException("Cannot consume a non-empty container.");
                _items.Remove(item.InstanceId);
                return new RuntimeItemConsumptionResult(item.InstanceId, quantity, 0, true);
            }
            Replace(item with { StackQuantity = remaining });
            return new RuntimeItemConsumptionResult(item.InstanceId, quantity, remaining, false);
        });
    }


    public RuntimeItemDurabilityResult DamageDurability(string instanceId, string ownerId, int amount = 1)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, ownerId);
            if (amount <= 0) throw new InvalidOperationException("Durability damage must be greater than zero.");
            if (item.Durability is null) throw new InvalidOperationException("Item has no projected durability state.");
            var previous = item.Durability.Value;
            var current = Math.Max(0, previous - amount);
            var metadata = new Dictionary<string, string>(item.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            if (current == 0) metadata["broken"] = "true";
            var updated = Replace(item with { Durability = current, Metadata = metadata });
            return new RuntimeItemDurabilityResult(updated.InstanceId, previous, current, current == 0);
        });
    }

    public RuntimeItemDurabilityResult RepairDurability(string instanceId, string ownerId, int amount)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, ownerId);
            if (amount <= 0) throw new InvalidOperationException("Repair amount must be greater than zero.");
            if (item.Durability is null) throw new InvalidOperationException("Item has no projected durability state.");
            var maximum = MaximumDurability(item);
            var previous = item.Durability.Value;
            var current = Math.Min(maximum, checked(previous + amount));
            var metadata = new Dictionary<string, string>(item.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            if (current > 0) metadata.Remove("broken");
            var updated = Replace(item with { Durability = current, Metadata = metadata });
            return new RuntimeItemDurabilityResult(updated.InstanceId, previous, current, current == 0);
        });
    }

    public RuntimeItemInstance Use(string instanceId, string characterId)
    {
        return Atomic(() =>
        {
            var item = RequireOwned(instanceId, characterId);
            var definition = _catalog.Require(item.CanonicalItemId);
            if (item.Charges > 0) return Replace(item with { Charges = item.Charges - 1 });
            if (!definition.Consumable) throw new InvalidOperationException("Item is not usable or has no remaining charges.");
            if (item.StackQuantity > 1) return Replace(item with { StackQuantity = item.StackQuantity - 1 });
            if (_items.Values.Any(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, item.InstanceId))) throw new InvalidOperationException("Cannot consume a non-empty container.");
            _items.Remove(item.InstanceId);
            return item;
        });
    }

    public RuntimeItemState Snapshot(long gold = 0, IReadOnlyDictionary<string, string>? active = null) { lock (_gate) return new(_items.Values.ToArray(), gold, active ?? new Dictionary<string, string>(), _corpses.Values.ToArray()); }
    public void RestoreSnapshot(RuntimeItemState snapshot)
    {
        lock (_gate)
        {
            _items = snapshot.Items.ToDictionary(item => item.InstanceId, StringComparer.OrdinalIgnoreCase);
            _corpses = (snapshot.Corpses ?? Array.Empty<RuntimeCorpseState>()).ToDictionary(corpse => corpse.CorpseId, StringComparer.OrdinalIgnoreCase);
            EnsureValid();
        }
    }
    public RuntimeItemValidationReport Validate() { lock (_gate) return RuntimeItemValidator.Validate(_catalog, _items.Values, _corpses.Values); }

    private RuntimeItemInstance SetContainerOpen(string id, string ownerId, bool open) => Atomic(() => { var container = RequireOwned(id, ownerId); if (!_catalog.Require(container.CanonicalItemId).Container) throw new InvalidOperationException("Item is not a generated container."); var metadata = new Dictionary<string, string>(container.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase) { ["open"] = open ? "true" : "false" }; return Replace(container with { Metadata = metadata }); });
    private bool IsContainerOpen(RuntimeItemInstance container) => container.Metadata is null || !container.Metadata.TryGetValue("open", out var value) || bool.TryParse(value, out var open) && open;
    private void EnforceCapacity(RuntimeItemInstance container) { if (container.Metadata?.TryGetValue("capacity", out var raw) == true && int.TryParse(raw, out var capacity) && capacity >= 0 && Contents(container.InstanceId).Count >= capacity) throw new InvalidOperationException("Container capacity exceeded."); }
    private bool IsDescendant(string candidateContainerId, string ancestorId) { var current = _items.GetValueOrDefault(candidateContainerId); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); while (current is not null && current.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && visited.Add(current.InstanceId)) { if (Same(current.LocationId, ancestorId)) return true; current = _items.GetValueOrDefault(current.LocationId); } return false; }
    private IReadOnlyList<string> DisplaceEquipmentConflictsForRestorationCore(string ownerId, string restoringInstanceId, string slot)
    {
        var restoring = Require(restoringInstanceId);
        var relationship = _catalog.Equipment(restoring.CanonicalItemId) ?? throw new InvalidOperationException("Item has no generated equipment relationship.");
        var conflicts = _items.Values
            .Where(x => Same(x.OwnerId, ownerId) && x.EquippedSlot is not null && !Same(x.InstanceId, restoringInstanceId))
            .Where(x => Same(x.EquippedSlot, slot)
                || (_catalog.HasRule("TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD") && relationship.TwoHanded && (Same(x.EquippedSlot, "OFF_HAND") || Same(x.EquippedSlot, "SHIELD")))
                || (_catalog.HasRule("TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD") && (Same(slot, "OFF_HAND") || Same(slot, "SHIELD")) && _catalog.Equipment(x.CanonicalItemId)?.TwoHanded == true))
            .OrderBy(x => x.EquippedSlot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var conflict in conflicts) Unequip(conflict.InstanceId, ownerId);
        return conflicts.Select(x => x.InstanceId).ToArray();
    }

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
    private void SetContainedDescendantOwnerCore(string parentInstanceId, string? ownerId)
    {
        var children = _items.Values
            .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, parentInstanceId))
            .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var child in children)
        {
            Replace(child with { OwnerId = ownerId });
            SetContainedDescendantOwnerCore(child.InstanceId, ownerId);
        }
    }

    private IReadOnlyList<string> CollectSubtreeIdsCore(string rootInstanceId)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(rootInstanceId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) throw new InvalidOperationException("Circular container hierarchy encountered during recursive item cleanup.");
            result.Add(current);
            foreach (var child in _items.Values
                .Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, current))
                .OrderByDescending(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)) pending.Push(child.InstanceId);
        }
        return result;
    }

    private RuntimeItemInstance TransferQuantityCore(RuntimeItemInstance source, int quantity, string kind, string location, string? owner)
    {
        if (quantity <= 0 || quantity > source.StackQuantity) throw new InvalidOperationException("Transfer quantity is outside the source stack.");
        var transferred = quantity == source.StackQuantity
            ? TransferCore(source.InstanceId, kind, location, owner, null, false)
            : TransferCore(SplitStackCore(source, quantity).InstanceId, kind, location, owner, null, false);
        return MergeIntoDestinationCore(transferred);
    }

    private RuntimeItemInstance MergeIntoDestinationCore(RuntimeItemInstance transferred)
    {
        if (!_catalog.Require(transferred.CanonicalItemId).Stackable) return transferred;
        var remainder = transferred;
        var destinations = _items.Values
            .Where(candidate => !Same(candidate.InstanceId, remainder.InstanceId) && CanMerge(candidate, remainder))
            .OrderBy(candidate => candidate.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var target in destinations)
        {
            var currentTarget = Require(target.InstanceId);
            var capacity = MaximumStack(currentTarget) - currentTarget.StackQuantity;
            if (capacity <= 0) continue;
            var moved = Math.Min(capacity, remainder.StackQuantity);
            Replace(currentTarget with { StackQuantity = checked(currentTarget.StackQuantity + moved) });
            if (moved == remainder.StackQuantity)
            {
                _items.Remove(remainder.InstanceId);
                return Require(currentTarget.InstanceId);
            }
            remainder = Replace(remainder with { StackQuantity = remainder.StackQuantity - moved });
        }

        return remainder;
    }

    private RuntimeItemInstance SplitStackCore(RuntimeItemInstance source, int quantity)
    {
        var definition = _catalog.Require(source.CanonicalItemId);
        if (!definition.Stackable) throw new InvalidOperationException("Item definition is not stackable.");
        if (quantity <= 0 || quantity >= source.StackQuantity) throw new InvalidOperationException("Split quantity must be greater than zero and less than the source stack.");
        Replace(source with { StackQuantity = source.StackQuantity - quantity });
        var split = source with { InstanceId = Guid.NewGuid().ToString("N"), StackQuantity = quantity };
        AddUnique(split);
        return split;
    }

    private bool CanMerge(RuntimeItemInstance target, RuntimeItemInstance source)
    {
        if (!_catalog.Require(target.CanonicalItemId).Stackable || !_catalog.Require(source.CanonicalItemId).Stackable) return false;
        return Same(target.CanonicalItemId, source.CanonicalItemId)
            && Same(target.OwnerId, source.OwnerId)
            && Same(target.LocationKind, source.LocationKind)
            && Same(target.LocationId, source.LocationId)
            && Same(target.EquippedSlot, source.EquippedSlot)
            && target.Charges == source.Charges
            && target.Durability == source.Durability
            && MetadataEqual(target.Metadata, source.Metadata);
    }

    private static int MaximumStack(RuntimeItemInstance item)
    {
        if (item.Metadata?.TryGetValue("maximum_stack", out var raw) == true && int.TryParse(raw, out var parsed) && parsed > 0) return parsed;
        return int.MaxValue;
    }

    private static int MaximumCharges(RuntimeItemInstance item)
    {
        if (item.Metadata?.TryGetValue("maximum_charges", out var raw) == true && int.TryParse(raw, out var parsed) && parsed >= 0) return parsed;
        throw new InvalidOperationException("Recharge requires an authoritative maximum_charges projection.");
    }


    private static int MaximumDurability(RuntimeItemInstance item)
    {
        if (item.Metadata?.TryGetValue("maximum_durability", out var raw) == true && int.TryParse(raw, out var parsed) && parsed >= 0) return parsed;
        throw new InvalidOperationException("Repair requires an authoritative maximum_durability projection.");
    }

    private static int RequireMaximumDurability(RuntimeItemInstance item) => MaximumDurability(item);

    private static Dictionary<string, string> CloneMetadata(RuntimeItemInstance item) =>
        item.Metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase);

    private static bool MetadataEqual(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Count != right.Count) return false;
        return left.All(pair => right.TryGetValue(pair.Key, out var value) && Same(pair.Value, value));
    }

    private RuntimeItemInstance TransferCore(string id, string kind, string location, string? owner, string? slot, bool requireGettable)
    {
        var item = Require(id);
        if (!RuntimeItemLocations.All.Contains(kind) || string.IsNullOrWhiteSpace(location)) throw new InvalidOperationException("Invalid item transfer destination.");
        if (requireGettable && !_catalog.Require(item.CanonicalItemId).Gettable) throw new InvalidOperationException("Item is not gettable according to generated data.");
        if (!Same(item.OwnerId, owner)) EnforceOwnerQuantityLimit(item, owner, item.StackQuantity, item.InstanceId);
        return Replace(item with { LocationKind = kind, LocationId = location, OwnerId = owner, EquippedSlot = slot });
    }

    private void EnforceOwnerQuantityLimit(RuntimeItemInstance item, string? destinationOwnerId, int incomingQuantity, string? excludedInstanceId)
    {
        if (string.IsNullOrWhiteSpace(destinationOwnerId)) return;
        if (item.Metadata?.TryGetValue("maximum_owned_quantity", out var raw) != true) return;
        if (!int.TryParse(raw, out var maximum) || maximum <= 0) throw new InvalidOperationException("Projected maximum_owned_quantity must be a positive integer.");
        var owned = _items.Values
            .Where(candidate => !Same(candidate.InstanceId, excludedInstanceId)
                && Same(ResolveLimitOwnerCore(candidate), destinationOwnerId)
                && Same(candidate.CanonicalItemId, item.CanonicalItemId))
            .Sum(candidate => candidate.StackQuantity);
        if (checked(owned + incomingQuantity) > maximum) throw new InvalidOperationException($"Item owner quantity limit of {maximum} would be exceeded.");
    }

    private static bool IsLimitCountedLocation(string locationKind) =>
        locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase);


    private string? ResolveLimitOwnerCore(RuntimeItemInstance item)
    {
        if (IsLimitCountedLocation(item.LocationKind)) return item.OwnerId;
        if (item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase))
            return _corpses.TryGetValue(item.LocationId, out var corpse) && !corpse.CleanedUp ? (string.IsNullOrWhiteSpace(corpse.LimitedItemReservationOwnerId) ? corpse.CharacterId : corpse.LimitedItemReservationOwnerId) : null;
        if (!item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)) return null;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { item.InstanceId };
        var cursor = item;
        while (cursor.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
        {
            if (!_items.TryGetValue(cursor.LocationId, out var parent) || !visited.Add(parent.InstanceId)) return null;
            cursor = parent;
        }
        return ResolveLimitOwnerCore(cursor);
    }


    private static bool HasCorpseLootAccess(RuntimeCorpseState corpse, string? characterId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(corpse.LootOwnerId)) return true;
        if (corpse.ExclusiveLootUntilUtc is null || now >= corpse.ExclusiveLootUntilUtc.Value) return true;
        return Same(corpse.LootOwnerId, characterId);
    }

    private static void EnforceCorpseLootAccess(RuntimeCorpseState corpse, string? characterId, DateTimeOffset now)
    {
        if (!HasCorpseLootAccess(corpse, characterId, now)) throw new InvalidOperationException("Corpse loot is temporarily reserved for another owner.");
    }

    private RuntimeItemInstance RequireCarried(string id, string owner)
    {
        var item = RequireOwned(id, owner);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || item.EquippedSlot is not null)
            throw new InvalidOperationException("Item must be carried and unequipped for this transition.");
        return item;
    }

    private bool IsDeathProtected(RuntimeItemInstance item) =>
        _catalog.Require(item.CanonicalItemId).RetainedOnDeath ||
        IsMetadataTrue(item, "death_protected") || IsMetadataTrue(item, "no_drop_on_death");

    private static bool IsMetadataTrue(RuntimeItemInstance item, string key) =>
        item.Metadata?.TryGetValue(key, out var raw) == true && bool.TryParse(raw, out var value) && value;

    private static void EnforceTransferRestrictions(RuntimeItemInstance item, string operation)
    {
        var key = operation switch
        {
            "drop" => "no_drop",
            "give" => "no_give",
            "container" => "no_container",
            "trade" => "no_trade",
            "bank" => "no_bank",
            "sell" => "no_sell",
            _ => throw new InvalidOperationException("Unknown item transfer restriction operation.")
        };
        if (IsMetadataTrue(item, key)) throw new InvalidOperationException($"Item restriction prevents {operation}.");
    }
    private static void EnforceOwnershipTransferRestrictions(RuntimeItemInstance item, string destinationOwnerId, string operation)
    {
        if (item.Metadata?.TryGetValue("bound_owner_id", out var boundOwnerId) == true && !string.IsNullOrWhiteSpace(boundOwnerId) && !Same(boundOwnerId, destinationOwnerId))
            throw new InvalidOperationException($"Bound item restriction prevents {operation}.");
        if (item.Metadata?.TryGetValue("quest_owner_id", out var questOwnerId) == true && !string.IsNullOrWhiteSpace(questOwnerId) && !Same(questOwnerId, destinationOwnerId))
            throw new InvalidOperationException($"Quest item ownership restriction prevents {operation}.");
    }

    private static void EnforceDestructionRestrictions(RuntimeItemInstance item, bool administrativeOverride)
    {
        if (administrativeOverride) return;
        if (IsMetadataTrue(item, "no_destroy")) throw new InvalidOperationException("Item restriction prevents destruction.");
        if (IsMetadataTrue(item, "quest_protected")) throw new InvalidOperationException("Quest-protected item cannot be destroyed.");
    }

    private RuntimeItemInstance RemoveCore(string instanceId, bool administrativeOverride)
    {
        var item = Require(instanceId);
        EnforceDestructionRestrictions(item, administrativeOverride);
        if (_items.Values.Any(x => Same(x.LocationId, instanceId) && x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Cannot remove a non-empty container.");
        _items.Remove(instanceId);
        return item;
    }
    private RuntimeItemInstance Require(string id) => _items.TryGetValue(id, out var item) ? item : throw new InvalidOperationException("Unknown item instance.");
    private int CountSubtree(string rootInstanceId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(rootInstanceId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) throw new InvalidOperationException("Circular portable-container hierarchy detected while counting an NPC corpse transfer.");
            foreach (var child in _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, current))) pending.Push(child.InstanceId);
        }
        return visited.Count;
    }

    private RuntimeCorpseState RequireCorpse(string id) => _corpses.TryGetValue(id, out var corpse) && !corpse.CleanedUp ? corpse : throw new InvalidOperationException("Unknown or expired corpse.");
    private RuntimeItemInstance RequireOwned(string id, string owner) { var item = Require(id); if (!Same(item.OwnerId, owner)) throw new InvalidOperationException("Item ownership check failed."); return item; }
    private void AddCreatedUnique(RuntimeItemInstance item)
    {
        var createdBeforeThisOperation = _createdQuantities.GetValueOrDefault(item.CanonicalItemId);
        RuntimeClassicItemLimitCoordinator.EnsureCanCreate(
            _catalog.ProjectRoot,
            _catalog.WorldId,
            _catalog.Require(item.CanonicalItemId),
            item.StackQuantity,
            createdBeforeThisOperation);
        AddUnique(item);
        _createdQuantities[item.CanonicalItemId] = checked(createdBeforeThisOperation + item.StackQuantity);
    }

    private void AddUnique(RuntimeItemInstance item) { if (!_items.TryAdd(item.InstanceId, item)) throw new InvalidOperationException("Duplicate item instance ID."); }
    private RuntimeItemInstance Replace(RuntimeItemInstance item) { _items[item.InstanceId] = item; return item; }
    private void EnsureValid() { var report = RuntimeItemValidator.Validate(_catalog, _items.Values, _corpses.Values); if (!report.Passed) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(x => x.Message))); }
    private T Atomic<T>(Func<T> operation) { lock (_gate) { var beforeItems = new Dictionary<string, RuntimeItemInstance>(_items, StringComparer.OrdinalIgnoreCase); var beforeCorpses = new Dictionary<string, RuntimeCorpseState>(_corpses, StringComparer.OrdinalIgnoreCase); var beforeCreated = new Dictionary<string, int>(_createdQuantities, StringComparer.OrdinalIgnoreCase); try { var result = operation(); EnsureValid(); return result; } catch { _items = beforeItems; _corpses = beforeCorpses; _createdQuantities = beforeCreated; throw; } } }
    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public static class RuntimeItemValidator
{
    public static RuntimeItemValidationReport Validate(RuntimeItemCatalog catalog, IEnumerable<RuntimeItemInstance> items, IEnumerable<RuntimeCorpseState>? corpses = null)
    {
        var list = items.ToArray();
        var corpseList = (corpses ?? Array.Empty<RuntimeCorpseState>()).ToArray();
        var corpseIds = corpseList.Where(x => !x.CleanedUp).Select(x => x.CorpseId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issues = new List<RuntimeItemValidationIssue>();
        foreach (var duplicate in corpseList.GroupBy(x => x.CorpseId, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1)) issues.Add(new("DUPLICATE_CORPSE_ID", "Duplicate corpse identity.", duplicate.Key));
        foreach (var corpse in corpseList)
        {
            if (string.IsNullOrWhiteSpace(corpse.CorpseId) || string.IsNullOrWhiteSpace(corpse.CharacterId) || string.IsNullOrWhiteSpace(corpse.RoomId)) issues.Add(new("INVALID_CORPSE_IDENTITY", "Corpse identity, character, and room are required.", corpse.CorpseId));
            if (corpse.Gold < 0) issues.Add(new("INVALID_CORPSE_GOLD", "Corpse gold cannot be negative.", corpse.CorpseId));
            if (corpse.ExclusiveLootUntilUtc is not null && string.IsNullOrWhiteSpace(corpse.LootOwnerId)) issues.Add(new("INVALID_CORPSE_LOOT_RESERVATION", "Exclusive corpse-loot expiration requires a loot owner.", corpse.CorpseId));
            if (corpse.ExclusiveLootUntilUtc is not null && corpse.ExclusiveLootUntilUtc < corpse.CreatedAtUtc) issues.Add(new("INVALID_CORPSE_LOOT_WINDOW", "Exclusive corpse-loot expiration cannot precede corpse creation.", corpse.CorpseId));
        }
        foreach (var duplicate in list.GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1)) issues.Add(new("DUPLICATE_INSTANCE_ID", "Duplicate item instance ID.", duplicate.Key));
        var ids = list.Select(x => x.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            try { var definition = catalog.Require(item.CanonicalItemId); if (item.StackQuantity < 1 || (!definition.Stackable && item.StackQuantity != 1)) issues.Add(new("INVALID_STACK", "Invalid stack quantity.", item.InstanceId)); } catch (Exception ex) { issues.Add(new("UNKNOWN_CANONICAL_ITEM", ex.Message, item.InstanceId)); }
            if (item.Metadata?.TryGetValue("maximum_owned_quantity", out var maximumOwnedRaw) == true && (!int.TryParse(maximumOwnedRaw, out var maximumOwned) || maximumOwned <= 0)) issues.Add(new("INVALID_MAXIMUM_OWNED_QUANTITY", "Projected maximum_owned_quantity must be a positive integer.", item.InstanceId));
            if (item.Metadata?.TryGetValue("maximum_stack", out var maximumStackRaw) == true)
            {
                if (!int.TryParse(maximumStackRaw, out var maximumStack) || maximumStack <= 0) issues.Add(new("INVALID_MAXIMUM_STACK", "Projected maximum_stack must be a positive integer.", item.InstanceId));
                else if (item.StackQuantity > maximumStack) issues.Add(new("STACK_EXCEEDS_MAXIMUM", "Item stack exceeds projected maximum stack quantity.", item.InstanceId));
            }
            if (item.Charges < 0) issues.Add(new("INVALID_CHARGES", "Item charges cannot be negative.", item.InstanceId));
            if (item.Durability is < 0) issues.Add(new("INVALID_DURABILITY", "Item durability cannot be negative.", item.InstanceId));
            if (item.Metadata?.TryGetValue("maximum_durability", out var maximumDurabilityRaw) == true && int.TryParse(maximumDurabilityRaw, out var maximumDurability) && maximumDurability >= 0 && item.Durability is not null && item.Durability > maximumDurability) issues.Add(new("DURABILITY_EXCEEDS_MAXIMUM", "Item durability exceeds projected maximum durability.", item.InstanceId));
            if (item.Metadata?.TryGetValue("broken", out var brokenRaw) == true && bool.TryParse(brokenRaw, out var broken) && broken && item.Durability != 0) issues.Add(new("BROKEN_STATE_MISMATCH", "Broken metadata requires zero durability.", item.InstanceId));
            foreach (var restrictionKey in new[] { "no_drop", "no_give", "no_container", "no_trade", "no_bank", "no_sell", "no_destroy", "quest_protected", "no_remove", "cursed_no_remove" })
            {
                if (item.Metadata?.TryGetValue(restrictionKey, out var restrictionRaw) == true && !bool.TryParse(restrictionRaw, out _)) issues.Add(new("INVALID_RESTRICTION_METADATA", $"Restriction metadata '{restrictionKey}' must be boolean.", item.InstanceId));
            }
            if (item.Metadata?.TryGetValue("merchant_restock_key", out var restockKey) == true && string.IsNullOrWhiteSpace(restockKey)) issues.Add(new("INVALID_MERCHANT_RESTOCK_KEY", "Merchant restock key cannot be empty.", item.InstanceId));
            if (item.Metadata?.TryGetValue("merchant_restock_target_quantity", out var restockTargetRaw) == true && (!int.TryParse(restockTargetRaw, out var restockTarget) || restockTarget < 0)) issues.Add(new("INVALID_MERCHANT_RESTOCK_TARGET", "Merchant restock target quantity must be a non-negative integer.", item.InstanceId));
            if (item.Metadata?.TryGetValue("room_reset_key", out var roomResetKey) == true && string.IsNullOrWhiteSpace(roomResetKey)) issues.Add(new("INVALID_ROOM_RESET_KEY", "Room item reset key cannot be empty.", item.InstanceId));
            if (item.Metadata?.TryGetValue("room_reset_target_quantity", out var roomResetTargetRaw) == true && (!int.TryParse(roomResetTargetRaw, out var roomResetTarget) || roomResetTarget < 0)) issues.Add(new("INVALID_ROOM_RESET_TARGET", "Room item reset target quantity must be a non-negative integer.", item.InstanceId));
            if (item.Metadata?.TryGetValue("npc_reset_key", out var npcResetKey) == true && string.IsNullOrWhiteSpace(npcResetKey)) issues.Add(new("INVALID_NPC_RESET_KEY", "NPC item reset key cannot be empty.", item.InstanceId));
            if (item.Metadata?.TryGetValue("npc_loot_key", out var npcLootKey) == true && string.IsNullOrWhiteSpace(npcLootKey)) issues.Add(new("INVALID_NPC_LOOT_KEY", "NPC corpse loot key cannot be empty.", item.InstanceId));
            if (item.Metadata?.TryGetValue("npc_loot_target_quantity", out var npcLootTargetRaw) == true && (!int.TryParse(npcLootTargetRaw, out var npcLootTarget) || npcLootTarget < 0)) issues.Add(new("INVALID_NPC_LOOT_TARGET", "NPC corpse loot target quantity must be a nonnegative integer.", item.InstanceId));
            if (item.Metadata?.TryGetValue("quest_id", out var questId) == true && string.IsNullOrWhiteSpace(questId)) issues.Add(new("INVALID_QUEST_ITEM_QUEST_ID", "Quest item quest identity cannot be empty.", item.InstanceId));
            if (item.Metadata?.TryGetValue("quest_grant_key", out var questGrantKey) == true && string.IsNullOrWhiteSpace(questGrantKey)) issues.Add(new("INVALID_QUEST_ITEM_GRANT_KEY", "Quest item grant key cannot be empty.", item.InstanceId));
            if (item.Metadata?.TryGetValue("quest_grant_target_quantity", out var questGrantTargetRaw) == true && (!int.TryParse(questGrantTargetRaw, out var questGrantTarget) || questGrantTarget < 0)) issues.Add(new("INVALID_QUEST_ITEM_GRANT_TARGET", "Quest item grant target quantity must be a nonnegative integer.", item.InstanceId));
            if (item.Metadata?.TryGetValue("npc_reset_target_quantity", out var npcResetTargetRaw) == true && (!int.TryParse(npcResetTargetRaw, out var npcResetTarget) || npcResetTarget < 0)) issues.Add(new("INVALID_NPC_RESET_TARGET", "NPC item reset target quantity must be a non-negative integer.", item.InstanceId));
            if (item.Metadata?.TryGetValue("npc_reset_equipped_slot", out var npcResetSlot) == true && (string.IsNullOrWhiteSpace(npcResetSlot) || !catalog.IsLegalSlot(npcResetSlot))) issues.Add(new("INVALID_NPC_RESET_SLOT", "NPC item reset equipment slot must be a legal generated slot.", item.InstanceId));
            foreach (var ownerKey in new[] { "bound_owner_id", "quest_owner_id" })
            {
                if (item.Metadata?.TryGetValue(ownerKey, out var projectedOwner) == true && string.IsNullOrWhiteSpace(projectedOwner)) issues.Add(new("INVALID_OWNER_RESTRICTION_METADATA", $"Ownership restriction metadata '{ownerKey}' cannot be empty.", item.InstanceId));
            }
            if (item.Metadata?.TryGetValue("bound_owner_id", out var boundOwner) == true && !string.IsNullOrWhiteSpace(item.OwnerId) && IsOwnedLocation(item.LocationKind) && !Same(boundOwner, item.OwnerId)) issues.Add(new("BOUND_OWNER_MISMATCH", "Bound item owner differs from its projected bound owner.", item.InstanceId));
            if (item.Metadata?.TryGetValue("quest_owner_id", out var questOwner) == true && !string.IsNullOrWhiteSpace(item.OwnerId) && IsOwnedLocation(item.LocationKind) && !Same(questOwner, item.OwnerId)) issues.Add(new("QUEST_OWNER_MISMATCH", "Quest item owner differs from its projected quest owner.", item.InstanceId));
            if (item.Metadata?.TryGetValue("maximum_charges", out var maximumChargesRaw) == true && int.TryParse(maximumChargesRaw, out var maximumCharges) && maximumCharges >= 0 && item.Charges > maximumCharges) issues.Add(new("CHARGES_EXCEED_MAXIMUM", "Item charges exceed projected maximum charges.", item.InstanceId));
            if (!RuntimeItemLocations.All.Contains(item.LocationKind) || string.IsNullOrWhiteSpace(item.LocationId)) issues.Add(new("INVALID_LOCATION", "Invalid item location.", item.InstanceId));
            if (IsOwnerLocationIdScoped(item.LocationKind) && !string.IsNullOrWhiteSpace(item.OwnerId) && !Same(item.LocationId, item.OwnerId)) issues.Add(new("OWNER_LOCATION_ID_MISMATCH", "Owned item location identity differs from its authoritative owner.", item.InstanceId));
            if ((item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(item.OwnerId)) issues.Add(new("NONOWNED_LOCATION_HAS_OWNER", "Room- or corpse-held item must not retain an owner.", item.InstanceId));
            if (!item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && item.EquippedSlot is not null) issues.Add(new("NON_EQUIPMENT_HAS_SLOT", "Only equipment-held items may retain an equipped slot.", item.InstanceId));
            if (item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && item.EquippedSlot is null) issues.Add(new("EQUIPMENT_MISSING_SLOT", "Equipment-held item is missing its authoritative slot.", item.InstanceId));
            if ((item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(item.OwnerId)) issues.Add(new("INVALID_OWNER", "Owned location requires an owner.", item.InstanceId));
            if (item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && (!ids.Contains(item.LocationId) || Same(item.LocationId, item.InstanceId))) issues.Add(new("INVALID_CONTAINER", "Container reference is invalid.", item.InstanceId));
            if (item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && !corpseIds.Contains(item.LocationId)) issues.Add(new("INVALID_CORPSE", "Corpse reference is invalid.", item.InstanceId));
            if (item.EquippedSlot is not null)
            {
                var relationship = catalog.Equipment(item.CanonicalItemId);
                if (!item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || relationship is null || !catalog.IsLegalSlot(item.EquippedSlot) || !Same(relationship.Slot, item.EquippedSlot)) issues.Add(new("ILLEGAL_EQUIPPED_SLOT", "Illegal equipped slot.", item.InstanceId));
            }
        }
        var byId = list.GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var item in list.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)))
        {
            if (byId.TryGetValue(item.LocationId, out var parent))
            {
                try { if (!catalog.Require(parent.CanonicalItemId).Container) issues.Add(new("NON_CONTAINER_PARENT", "Container location points to an item that is not a container.", item.InstanceId)); } catch { }
                if (!Same(item.OwnerId, parent.OwnerId)) issues.Add(new("CONTAINER_OWNER_MISMATCH", "Contained item owner differs from its parent container owner.", item.InstanceId));
            }
            else issues.Add(new("MISSING_CONTAINER_PARENT", "Contained item references a missing parent container instance.", item.InstanceId));
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { item.InstanceId };
            var cursor = item;
            while (cursor.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && byId.TryGetValue(cursor.LocationId, out var next))
            {
                if (!visited.Add(next.InstanceId)) { issues.Add(new("CIRCULAR_CONTAINER", "Circular container hierarchy detected.", item.InstanceId)); break; }
                cursor = next;
            }
        }
        foreach (var item in list.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && !corpseIds.Contains(x.LocationId)))
            issues.Add(new("MISSING_CORPSE_PARENT", "Corpse-held item references a missing or cleaned-up corpse.", item.InstanceId));
        foreach (var occupied in list.Where(x => x.EquippedSlot is not null).GroupBy(x => $"{x.OwnerId}\0{x.EquippedSlot}", StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1)) foreach (var item in occupied) issues.Add(new("SLOT_CONFLICT", "Multiple items occupy the same equipment slot.", item.InstanceId));
        var corpseById = corpseList.Where(x => !x.CleanedUp).ToDictionary(x => x.CorpseId, StringComparer.OrdinalIgnoreCase);
        string? ResolveLimitOwner(RuntimeItemInstance item)
        {
            if (IsLimitCountedLocation(item.LocationKind)) return item.OwnerId;
            if (item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase))
                return corpseById.TryGetValue(item.LocationId, out var corpse) ? (string.IsNullOrWhiteSpace(corpse.LimitedItemReservationOwnerId) ? corpse.CharacterId : corpse.LimitedItemReservationOwnerId) : null;
            if (!item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)) return null;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { item.InstanceId };
            var cursor = item;
            while (cursor.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && byId.TryGetValue(cursor.LocationId, out var parent))
            {
                if (!visited.Add(parent.InstanceId)) return null;
                cursor = parent;
            }
            return ResolveLimitOwner(cursor);
        }
        foreach (var group in list.Where(x => x.Metadata?.ContainsKey("maximum_owned_quantity") == true && !string.IsNullOrWhiteSpace(ResolveLimitOwner(x)))
            .GroupBy(x => $"{ResolveLimitOwner(x)}\0{x.CanonicalItemId}", StringComparer.OrdinalIgnoreCase))
        {
            var limits = group.Select(x => x.Metadata!["maximum_owned_quantity"]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (limits.Length != 1 || !int.TryParse(limits[0], out var maximum) || maximum <= 0) continue;
            if (group.Sum(x => x.StackQuantity) > maximum) foreach (var item in group) issues.Add(new("OWNER_QUANTITY_LIMIT_EXCEEDED", "Owner quantity exceeds projected item limit, including active corpse reservations.", item.InstanceId));
        }
        return new RuntimeItemValidationReport(issues.Count == 0, issues);
    }
    private static bool IsLimitCountedLocation(string locationKind) => locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase);
    private static bool IsOwnedLocation(string locationKind) => locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase);
    private static bool IsOwnerLocationIdScoped(string locationKind) => locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase);
    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

public static class RuntimeItemStateReconciler
{
    public static RuntimeItemDuplicateReconciliationResult ReconcileExactDuplicates(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        var removedItems = 0;
        var items = new List<RuntimeItemInstance>();
        foreach (var group in state.Items.GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (group.Any(candidate => !Equivalent(first, candidate))) throw new InvalidDataException($"Conflicting duplicate item instance ID '{group.Key}' cannot be reconciled safely.");
            items.Add(first);
            removedItems += group.Count() - 1;
        }

        var removedCorpses = 0;
        var corpses = new List<RuntimeCorpseState>();
        foreach (var group in (state.Corpses ?? Array.Empty<RuntimeCorpseState>()).GroupBy(x => x.CorpseId, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (group.Any(candidate => candidate != first)) throw new InvalidDataException($"Conflicting duplicate corpse ID '{group.Key}' cannot be reconciled safely.");
            corpses.Add(first);
            removedCorpses += group.Count() - 1;
        }

        var repaired = state with { Items = items, Corpses = corpses };
        var report = RuntimeItemValidator.Validate(catalog, repaired.Items, repaired.Corpses);
        if (!report.Passed) throw new InvalidDataException("Duplicate reconciliation did not produce valid item state: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.InstanceId}:{x.Message}")));
        return new RuntimeItemDuplicateReconciliationResult(repaired, removedItems, removedCorpses);
    }

    public static RuntimeItemOrphanReconciliationResult ReconcileMissingContainerParents(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        var byId = state.Items.GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var recovered = new List<string>();
        var repairedItems = new List<RuntimeItemInstance>(state.Items.Count);
        foreach (var item in state.Items.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase))
        {
            if (!item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) || byId.ContainsKey(item.LocationId))
            {
                repairedItems.Add(item);
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.OwnerId))
                throw new InvalidDataException($"Orphaned contained item '{item.InstanceId}' has no authoritative owner and cannot be reconciled safely.");
            var metadata = item.Metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase);
            metadata["recovered_from_missing_container_id"] = item.LocationId;
            metadata["recovery_policy"] = "owner_inventory";
            repairedItems.Add(item with
            {
                LocationKind = RuntimeItemLocations.Character,
                LocationId = item.OwnerId,
                EquippedSlot = null,
                Metadata = metadata
            });
            recovered.Add(item.InstanceId);
        }
        var repaired = state with { Items = repairedItems };
        var report = RuntimeItemValidator.Validate(catalog, repaired.Items, repaired.Corpses);
        if (!report.Passed) throw new InvalidDataException("Missing-container reconciliation did not produce valid item state: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.InstanceId}:{x.Message}")));
        return new RuntimeItemOrphanReconciliationResult(repaired, recovered.Count, recovered);
    }

    public static RuntimeItemOwnerLocationReconciliationResult ReconcileOwnerLocationState(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        var recovered = new List<string>();
        var repairedItems = new List<RuntimeItemInstance>(state.Items.Count);
        foreach (var item in state.Items.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase))
        {
            var repaired = item;
            var metadata = item.Metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase);
            var changed = false;
            if (IsOwnedRecoveryLocation(item.LocationKind) && !string.IsNullOrWhiteSpace(item.OwnerId) && !string.Equals(item.LocationId, item.OwnerId, StringComparison.OrdinalIgnoreCase))
            {
                metadata["recovered_from_location_id"] = item.LocationId;
                repaired = repaired with { LocationId = item.OwnerId };
                changed = true;
            }
            if ((item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) || item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(item.OwnerId))
            {
                metadata["recovered_from_owner_id"] = item.OwnerId;
                repaired = repaired with { OwnerId = null };
                changed = true;
            }
            if (item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && item.EquippedSlot is null)
            {
                if (string.IsNullOrWhiteSpace(item.OwnerId)) throw new InvalidDataException($"Equipment item '{item.InstanceId}' has no authoritative owner and cannot be reconciled safely.");
                metadata["recovery_policy"] = "owner_inventory_missing_equipment_slot";
                repaired = repaired with { LocationKind = RuntimeItemLocations.Character, LocationId = item.OwnerId, EquippedSlot = null };
                changed = true;
            }
            else if (!item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && item.EquippedSlot is not null)
            {
                metadata["recovered_from_equipped_slot"] = item.EquippedSlot;
                repaired = repaired with { EquippedSlot = null };
                changed = true;
            }
            if (changed)
            {
                metadata["owner_location_recovery"] = "authoritative_location_policy";
                repaired = repaired with { Metadata = metadata };
                recovered.Add(item.InstanceId);
            }
            repairedItems.Add(repaired);
        }
        var repairedState = state with { Items = repairedItems };
        var report = RuntimeItemValidator.Validate(catalog, repairedState.Items, repairedState.Corpses);
        if (!report.Passed) throw new InvalidDataException("Owner/location reconciliation did not produce valid item state: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.InstanceId}:{x.Message}")));
        return new RuntimeItemOwnerLocationReconciliationResult(repairedState, recovered.Count, recovered);
    }


    public static RuntimeItemContainerOwnerReconciliationResult ReconcileContainerOwnerMismatches(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        var byId = state.Items.ToDictionary(x => x.InstanceId, StringComparer.OrdinalIgnoreCase);
        var repaired = state.Items.ToDictionary(x => x.InstanceId, x => x, StringComparer.OrdinalIgnoreCase);
        var recovered = new List<string>();
        string? ResolveAuthoritativeOwner(RuntimeItemInstance item, HashSet<string> visited)
        {
            if (!visited.Add(item.InstanceId)) throw new InvalidDataException($"Circular container hierarchy prevents owner reconciliation for '{item.InstanceId}'.");
            if (!item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)) return item.OwnerId;
            if (!byId.TryGetValue(item.LocationId, out var parent)) throw new InvalidDataException($"Contained item '{item.InstanceId}' has no parent container and cannot be reconciled by owner inheritance.");
            if (!catalog.Require(parent.CanonicalItemId).Container) throw new InvalidDataException($"Contained item '{item.InstanceId}' points to a non-container parent.");
            return ResolveAuthoritativeOwner(parent, visited);
        }
        foreach (var item in state.Items.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase))
        {
            var authoritativeOwner = ResolveAuthoritativeOwner(item, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(authoritativeOwner))
            {
                if (!string.IsNullOrWhiteSpace(item.OwnerId)) throw new InvalidDataException($"Contained item '{item.InstanceId}' has an owner but its container hierarchy is ownerless; ownership cannot be inferred safely.");
                continue;
            }
            if (string.Equals(item.OwnerId, authoritativeOwner, StringComparison.OrdinalIgnoreCase)) continue;
            var metadata = item.Metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(item.OwnerId)) metadata["recovered_from_owner_id"] = item.OwnerId;
            metadata["container_owner_recovery"] = "inherit_authoritative_parent_owner";
            metadata["container_owner_source_instance_id"] = item.LocationId;
            repaired[item.InstanceId] = item with { OwnerId = authoritativeOwner, Metadata = metadata };
            recovered.Add(item.InstanceId);
        }
        var repairedState = state with { Items = repaired.Values.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray() };
        var report = RuntimeItemValidator.Validate(catalog, repairedState.Items, repairedState.Corpses);
        if (!report.Passed) throw new InvalidDataException("Container owner reconciliation did not produce valid item state: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.InstanceId}:{x.Message}")));
        return new RuntimeItemContainerOwnerReconciliationResult(repairedState, recovered.Count, recovered);
    }

    private static bool IsOwnedRecoveryLocation(string locationKind) => locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase) || locationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase);

    private static bool Equivalent(RuntimeItemInstance left, RuntimeItemInstance right) =>
        string.Equals(left.InstanceId, right.InstanceId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.CanonicalItemId, right.CanonicalItemId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.OwnerId, right.OwnerId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.LocationKind, right.LocationKind, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.LocationId, right.LocationId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.EquippedSlot, right.EquippedSlot, StringComparison.OrdinalIgnoreCase)
        && left.Charges == right.Charges
        && left.StackQuantity == right.StackQuantity
        && left.Durability == right.Durability
        && MetadataEqual(left.Metadata, right.Metadata);

    private static bool MetadataEqual(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Count != right.Count) return false;
        return left.All(pair => right.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));
    }
}


public static class RuntimeItemCrossSaveOwnershipCoordinator
{
    public static RuntimeItemCrossSaveOwnershipReport Validate(RuntimeItemState state, RuntimeItemOwnershipSnapshot ownership)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ownership);
        var issues = new List<RuntimeItemCrossSaveOwnershipIssue>();
        foreach (var item in state.Items)
        {
            if (string.IsNullOrWhiteSpace(item.OwnerId)) continue;
            if (IsCharacterScoped(item.LocationKind) && !ownership.CharacterIds.Contains(item.OwnerId))
                issues.Add(new("MISSING_CHARACTER_OWNER", "Item owner does not exist in the authoritative Character save.", item.InstanceId, item.OwnerId));
            else if (item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) && !ownership.NpcIds.Contains(item.OwnerId))
                issues.Add(new("MISSING_NPC_OWNER", "Item owner does not exist in the authoritative NPC save.", item.InstanceId, item.OwnerId));
            else if (item.LocationKind.Equals(RuntimeItemLocations.Merchant, StringComparison.OrdinalIgnoreCase) && !ownership.MerchantIds.Contains(item.OwnerId))
                issues.Add(new("MISSING_MERCHANT_OWNER", "Item owner does not exist in the authoritative Economy merchant state.", item.InstanceId, item.OwnerId));

            if (item.Metadata?.TryGetValue("quest_id", out var questId) == true && !string.IsNullOrWhiteSpace(questId) && !ownership.QuestIds.Contains(questId))
                issues.Add(new("MISSING_QUEST_REFERENCE", "Item references a quest absent from the authoritative Quest save.", item.InstanceId, questId));
            if (item.Metadata?.TryGetValue("event_id", out var eventId) == true && !string.IsNullOrWhiteSpace(eventId) && !ownership.EventIds.Contains(eventId))
                issues.Add(new("MISSING_EVENT_REFERENCE", "Item references an event absent from the authoritative Event save.", item.InstanceId, eventId));
        }

        foreach (var corpse in state.Corpses ?? Array.Empty<RuntimeCorpseState>())
        {
            if (corpse.CleanedUp) continue;
            if (!string.IsNullOrWhiteSpace(corpse.CharacterId) && !ownership.CharacterIds.Contains(corpse.CharacterId) && !ownership.NpcIds.Contains(corpse.CharacterId))
                issues.Add(new("MISSING_CORPSE_SUBJECT", "Corpse subject does not exist in Character or NPC authoritative state.", corpse.CorpseId, corpse.CharacterId));
            if (!string.IsNullOrWhiteSpace(corpse.LootOwnerId) && !ownership.CharacterIds.Contains(corpse.LootOwnerId))
                issues.Add(new("MISSING_CORPSE_LOOT_OWNER", "Corpse loot owner does not exist in authoritative Character state.", corpse.CorpseId, corpse.LootOwnerId));
            if (!string.IsNullOrWhiteSpace(corpse.LimitedItemReservationOwnerId) && !ownership.CharacterIds.Contains(corpse.LimitedItemReservationOwnerId))
                issues.Add(new("MISSING_LIMIT_RESERVATION_OWNER", "Corpse limited-item reservation owner does not exist in authoritative Character state.", corpse.CorpseId, corpse.LimitedItemReservationOwnerId));
        }
        return new RuntimeItemCrossSaveOwnershipReport(issues.Count == 0, issues);
    }

    public static void EnsureValid(RuntimeItemState state, RuntimeItemOwnershipSnapshot ownership)
    {
        var report = Validate(state, ownership);
        if (!report.Passed)
            throw new InvalidDataException("Cross-save item ownership validation failed: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.InstanceId}:{x.OwnerId}:{x.Message}")));
    }

    private static bool IsCharacterScoped(string locationKind) =>
        locationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase)
        || locationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase);
}


public static class RuntimeItemLimitedReconciliationCoordinator
{
    public static RuntimeItemLimitedReconciliationReport Validate(RuntimeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var issues = new List<RuntimeItemLimitedReconciliationIssue>();
        var byId = state.Items.GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var corpses = (state.Corpses ?? Array.Empty<RuntimeCorpseState>()).ToDictionary(x => x.CorpseId, StringComparer.OrdinalIgnoreCase);
        var reservations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ownerLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var worldLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in state.Items.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase))
        {
            if (item.StackQuantity <= 0) continue;
            var ownerLimit = ParsePositiveLimit(item, "maximum_owned_quantity", issues, "owner");
            var worldLimit = ParsePositiveLimit(item, "maximum_world_quantity", issues, "world");
            if (ownerLimit is null && worldLimit is null) continue;

            if (ownerLimit is not null)
            {
                var owner = ResolveReservationOwner(item, byId, corpses, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(owner))
                {
                    issues.Add(new("LIMITED_ITEM_OWNER_UNRESOLVED", "A limited item has no authoritative reservation owner.", item.CanonicalItemId, "owner", null, item.StackQuantity, ownerLimit));
                }
                else
                {
                    var key = $"owner|{owner}|{item.CanonicalItemId}";
                    if (ownerLimits.TryGetValue(key, out var prior) && prior != ownerLimit)
                        issues.Add(new("INCONSISTENT_OWNER_LIMIT", "Instances of the same owner-scoped limited item project conflicting maximum quantities.", item.CanonicalItemId, "owner", owner, null, ownerLimit));
                    ownerLimits[key] = ownerLimit.Value;
                    reservations[key] = checked(reservations.GetValueOrDefault(key) + item.StackQuantity);
                }
            }

            if (worldLimit is not null)
            {
                var key = $"world|{item.CanonicalItemId}";
                if (worldLimits.TryGetValue(key, out var prior) && prior != worldLimit)
                    issues.Add(new("INCONSISTENT_WORLD_LIMIT", "Instances of the same world-scoped limited item project conflicting maximum quantities.", item.CanonicalItemId, "world", null, null, worldLimit));
                worldLimits[key] = worldLimit.Value;
                reservations[key] = checked(reservations.GetValueOrDefault(key) + item.StackQuantity);
            }
        }

        foreach (var pair in ownerLimits)
        {
            var observed = reservations.GetValueOrDefault(pair.Key);
            if (observed > pair.Value)
            {
                var parts = pair.Key.Split('|', 3);
                issues.Add(new("OWNER_LIMIT_EXCEEDED", "Persisted owner-scoped limited-item quantity exceeds the authoritative maximum.", parts[2], "owner", parts[1], observed, pair.Value));
            }
        }
        foreach (var pair in worldLimits)
        {
            var observed = reservations.GetValueOrDefault(pair.Key);
            if (observed > pair.Value)
            {
                var canonical = pair.Key.Substring("world|".Length);
                issues.Add(new("WORLD_LIMIT_EXCEEDED", "Persisted world-scoped limited-item quantity exceeds the authoritative maximum.", canonical, "world", null, observed, pair.Value));
            }
        }
        return new RuntimeItemLimitedReconciliationReport(issues.Count == 0, issues, reservations);
    }

    public static void EnsureValid(RuntimeItemState state)
    {
        var report = Validate(state);
        if (!report.Passed)
            throw new InvalidDataException("Global limited-item reconciliation failed: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.Scope}:{x.ScopeOwnerId}:{x.CanonicalItemId}:{x.ObservedQuantity}/{x.MaximumQuantity}:{x.Message}")));
    }

    private static int? ParsePositiveLimit(RuntimeItemInstance item, string key, List<RuntimeItemLimitedReconciliationIssue> issues, string scope)
    {
        if (item.Metadata?.TryGetValue(key, out var raw) != true) return null;
        if (!int.TryParse(raw, out var parsed) || parsed <= 0)
        {
            issues.Add(new("MALFORMED_LIMIT", $"Projected {key} must be a positive integer.", item.CanonicalItemId, scope));
            return null;
        }
        return parsed;
    }

    private static string? ResolveReservationOwner(RuntimeItemInstance item, IReadOnlyDictionary<string, RuntimeItemInstance> byId, IReadOnlyDictionary<string, RuntimeCorpseState> corpses, HashSet<string> visited)
    {
        if (!visited.Add(item.InstanceId)) return null;
        if (item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
            return byId.TryGetValue(item.LocationId, out var parent) ? ResolveReservationOwner(parent, byId, corpses, visited) : null;
        if (item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase))
        {
            if (!corpses.TryGetValue(item.LocationId, out var corpse) || corpse.CleanedUp) return null;
            return string.IsNullOrWhiteSpace(corpse.LimitedItemReservationOwnerId) ? corpse.CharacterId : corpse.LimitedItemReservationOwnerId;
        }
        if (item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
            || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
            || item.LocationKind.Equals(RuntimeItemLocations.Trade, StringComparison.OrdinalIgnoreCase)
            || item.LocationKind.Equals(RuntimeItemLocations.Bank, StringComparison.OrdinalIgnoreCase)) return item.OwnerId;
        return null;
    }
}

public static class RuntimeItemSaveGenerationCoordinator
{
    private static readonly string[] RequiredSubsystems = { "character", "economy", "quests", "events", "npc", "world" };

    public static RuntimeItemState Stamp(RuntimeItemState state, RuntimeItemSaveGenerationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureSnapshotValid(snapshot);
        var active = new Dictionary<string, string>(state.ActiveItemState, StringComparer.OrdinalIgnoreCase)
        {
            ["coordinated_save_generation"] = snapshot.GenerationId
        };
        return state with { ActiveItemState = active };
    }

    public static RuntimeItemSaveGenerationReport Validate(RuntimeItemState state, RuntimeItemSaveGenerationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(state);
        var issues = new List<RuntimeItemSaveGenerationIssue>();
        if (string.IsNullOrWhiteSpace(snapshot.GenerationId))
            issues.Add(new("MISSING_COORDINATED_GENERATION", "The coordinated save generation is missing."));
        foreach (var subsystem in RequiredSubsystems)
        {
            if (!snapshot.SubsystemGenerations.TryGetValue(subsystem, out var observed) || string.IsNullOrWhiteSpace(observed))
                issues.Add(new("MISSING_SUBSYSTEM_GENERATION", "A required subsystem save generation is missing.", subsystem));
            else if (!string.Equals(observed, snapshot.GenerationId, StringComparison.OrdinalIgnoreCase))
                issues.Add(new("SUBSYSTEM_GENERATION_MISMATCH", "The subsystem save generation does not match the coordinated generation.", subsystem, observed));
        }
        if (!state.ActiveItemState.TryGetValue("coordinated_save_generation", out var itemGeneration) || string.IsNullOrWhiteSpace(itemGeneration))
            issues.Add(new("MISSING_ITEM_GENERATION", "The persisted item state does not contain a coordinated save generation.", "items"));
        else if (!string.Equals(itemGeneration, snapshot.GenerationId, StringComparison.OrdinalIgnoreCase))
            issues.Add(new("ITEM_GENERATION_MISMATCH", "The persisted item generation does not match the coordinated generation.", "items", itemGeneration));
        return new RuntimeItemSaveGenerationReport(issues.Count == 0, issues);
    }

    public static void EnsureValid(RuntimeItemState state, RuntimeItemSaveGenerationSnapshot snapshot)
    {
        var report = Validate(state, snapshot);
        if (!report.Passed)
            throw new InvalidDataException("Coordinated item save-generation validation failed: " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.Subsystem}:{x.ObservedGeneration}:{x.Message}")));
    }

    private static void EnsureSnapshotValid(RuntimeItemSaveGenerationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.GenerationId)) throw new ArgumentException("A coordinated save generation is required.", nameof(snapshot));
        ArgumentNullException.ThrowIfNull(snapshot.SubsystemGenerations);
    }
}

public sealed class RuntimeItemWorldRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _path;
    private string TemporaryPath => _path + ".tmp";
    private string BackupPath => _path + ".bak";
    public RuntimeItemWorldRepository(string projectRoot, string worldId) { var folder = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "worlds"); Directory.CreateDirectory(folder); _path = Path.Combine(folder, RuntimeIdentity.Normalize(worldId, "world") + ".items.json"); }
    public RuntimeItemState Load() => File.Exists(_path) ? Deserialize(_path) : Empty();
    public RuntimeItemDuplicateReconciliationResult ReconcileExactDuplicateRecords(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        if (!File.Exists(_path)) throw new FileNotFoundException("Authoritative item state does not exist.", _path);
        var raw = Deserialize(_path);
        var result = RuntimeItemStateReconciler.ReconcileExactDuplicates(catalog, raw);
        if (result.RemovedDuplicateItems > 0 || result.RemovedDuplicateCorpses > 0) WriteAtomically(result.State);
        return result;
    }
    public RuntimeItemOrphanReconciliationResult ReconcileMissingContainerParents(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        if (!File.Exists(_path)) throw new FileNotFoundException("Authoritative item state does not exist.", _path);
        var raw = Deserialize(_path);
        var result = RuntimeItemStateReconciler.ReconcileMissingContainerParents(catalog, raw);
        if (result.RecoveredContainerItems > 0) WriteAtomically(result.State);
        return result;
    }
    public RuntimeItemOwnerLocationReconciliationResult ReconcileOwnerLocationState(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        if (!File.Exists(_path)) throw new FileNotFoundException("Authoritative item state does not exist.", _path);
        var raw = Deserialize(_path);
        var result = RuntimeItemStateReconciler.ReconcileOwnerLocationState(catalog, raw);
        if (result.RecoveredItems > 0) WriteAtomically(result.State);
        return result;
    }

    public RuntimeItemContainerOwnerReconciliationResult ReconcileContainerOwnerMismatches(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        if (!File.Exists(_path)) throw new FileNotFoundException("Authoritative item state does not exist.", _path);
        var raw = Deserialize(_path);
        var result = RuntimeItemStateReconciler.ReconcileContainerOwnerMismatches(catalog, raw);
        if (result.RecoveredItems > 0) WriteAtomically(result.State);
        return result;
    }

    public RuntimeItemState LoadValidated(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        var state = Load();
        ValidateOrThrow(catalog, state, "Persisted item state failed validation");
        return state;
    }
    public RuntimeItemRecoveryResult LoadValidatedWithRecovery(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        string? primaryFailure = null;
        if (File.Exists(_path))
        {
            try
            {
                var primary = Deserialize(_path);
                ValidateOrThrow(catalog, primary, "Persisted item state failed validation");
                return new RuntimeItemRecoveryResult(primary, "primary", false);
            }
            catch (InvalidDataException ex) { primaryFailure = ex.Message; }
        }
        else primaryFailure = "Primary item-state save does not exist.";
        if (!File.Exists(BackupPath)) throw new InvalidDataException(primaryFailure + " No validated backup item-state save is available.");
        RuntimeItemState backup;
        try
        {
            backup = Deserialize(BackupPath);
            ValidateOrThrow(catalog, backup, "Backup item state failed validation");
        }
        catch (InvalidDataException backupError)
        {
            throw new InvalidDataException(primaryFailure + " Backup recovery also failed: " + backupError.Message, backupError);
        }
        RestoreBackupAtomically();
        return new RuntimeItemRecoveryResult(backup, "backup", true, primaryFailure);
    }
    public RuntimeItemState LoadValidatedAcrossSaves(RuntimeItemCatalog catalog, RuntimeItemOwnershipSnapshot ownership)
    {
        var state = LoadValidated(catalog);
        RuntimeItemCrossSaveOwnershipCoordinator.EnsureValid(state, ownership);
        return state;
    }
    public void SaveValidatedAcrossSaves(RuntimeItemCatalog catalog, RuntimeItemState state, RuntimeItemOwnershipSnapshot ownership)
    {
        ValidateOrThrow(catalog, state, "Refusing to persist invalid item state");
        RuntimeItemCrossSaveOwnershipCoordinator.EnsureValid(state, ownership);
        WriteAtomically(state);
    }
    public RuntimeItemRecoveryResult LoadValidatedAcrossSaveGenerationsAndLimits(RuntimeItemCatalog catalog, RuntimeItemOwnershipSnapshot ownership, RuntimeItemSaveGenerationSnapshot generations)
    {
        var recovered = LoadValidatedAcrossSaveGenerations(catalog, ownership, generations);
        RuntimeItemLimitedReconciliationCoordinator.EnsureValid(recovered.State);
        return recovered;
    }

    public RuntimeItemState SaveValidatedAcrossSaveGenerationsAndLimits(RuntimeItemCatalog catalog, RuntimeItemState state, RuntimeItemOwnershipSnapshot ownership, RuntimeItemSaveGenerationSnapshot generations)
    {
        RuntimeItemLimitedReconciliationCoordinator.EnsureValid(state);
        return SaveValidatedAcrossSaveGenerations(catalog, state, ownership, generations);
    }

    public RuntimeItemRecoveryResult LoadValidatedAcrossSaveGenerations(RuntimeItemCatalog catalog, RuntimeItemOwnershipSnapshot ownership, RuntimeItemSaveGenerationSnapshot generations)
    {
        var recovered = LoadValidatedWithIntegrityRecovery(catalog);
        RuntimeItemCrossSaveOwnershipCoordinator.EnsureValid(recovered.State, ownership);
        RuntimeItemSaveGenerationCoordinator.EnsureValid(recovered.State, generations);
        return recovered;
    }
    public RuntimeItemState SaveValidatedAcrossSaveGenerations(RuntimeItemCatalog catalog, RuntimeItemState state, RuntimeItemOwnershipSnapshot ownership, RuntimeItemSaveGenerationSnapshot generations)
    {
        var stamped = RuntimeItemSaveGenerationCoordinator.Stamp(state, generations);
        ValidateOrThrow(catalog, stamped, "Refusing to persist invalid item state");
        RuntimeItemCrossSaveOwnershipCoordinator.EnsureValid(stamped, ownership);
        RuntimeItemSaveGenerationCoordinator.EnsureValid(stamped, generations);
        SaveValidatedWithIntegrityBackup(catalog, stamped);
        return stamped;
    }
    public void Save(RuntimeItemState state) => WriteAtomically(state);
    public void SaveValidated(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        ValidateOrThrow(catalog, state, "Refusing to persist invalid item state");
        WriteAtomically(state);
    }
    public void SaveValidatedWithBackup(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        ValidateOrThrow(catalog, state, "Refusing to persist invalid item state");
        CleanupStaleTemporaryFile();
        if (File.Exists(_path))
        {
            var previous = Deserialize(_path);
            ValidateOrThrow(catalog, previous, "Refusing to rotate an invalid authoritative item state into backup");
            File.Copy(_path, BackupPath, true);
        }
        WriteAtomically(state);
    }
    public void SaveValidatedWithIntegrity(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        ValidateOrThrow(catalog, state, "Refusing to persist invalid item state");
        WriteAtomically(state);
        WriteIntegrityAtomically(_path, IntegrityPath);
    }
    public RuntimeItemState LoadValidatedWithIntegrity(RuntimeItemCatalog catalog)
    {
        if (!File.Exists(_path)) throw new FileNotFoundException("Authoritative item state does not exist.", _path);
        if (!File.Exists(IntegrityPath)) throw new InvalidDataException("Authoritative item-state integrity record is missing.");
        VerifyIntegrity(_path, IntegrityPath);
        var state = Deserialize(_path);
        ValidateOrThrow(catalog, state, "Persisted item state failed validation");
        return state;
    }
    public void SaveValidatedWithIntegrityBackup(RuntimeItemCatalog catalog, RuntimeItemState state)
    {
        ValidateOrThrow(catalog, state, "Refusing to persist invalid item state");
        CleanupStaleTemporaryFile();
        if (File.Exists(_path))
        {
            if (!File.Exists(IntegrityPath)) throw new InvalidDataException("Refusing to rotate an authoritative item state without its integrity record.");
            VerifyIntegrity(_path, IntegrityPath);
            var previous = Deserialize(_path);
            ValidateOrThrow(catalog, previous, "Refusing to rotate an invalid authoritative item state into integrity backup");
            File.Copy(_path, BackupPath, true);
            File.Copy(IntegrityPath, BackupIntegrityPath, true);
            VerifyIntegrity(BackupPath, BackupIntegrityPath);
        }
        WriteAtomically(state);
        WriteIntegrityAtomically(_path, IntegrityPath);
    }
    public RuntimeItemRecoveryResult LoadValidatedWithIntegrityRecovery(RuntimeItemCatalog catalog)
    {
        CleanupStaleTemporaryFile();
        string? primaryFailure = null;
        try
        {
            if (!File.Exists(_path)) throw new FileNotFoundException("Authoritative item state does not exist.", _path);
            if (!File.Exists(IntegrityPath)) throw new InvalidDataException("Authoritative item-state integrity record is missing.");
            VerifyIntegrity(_path, IntegrityPath);
            var primary = Deserialize(_path);
            ValidateOrThrow(catalog, primary, "Persisted item state failed validation");
            return new RuntimeItemRecoveryResult(primary, "primary", false);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException) { primaryFailure = ex.Message; }
        if (!File.Exists(BackupPath) || !File.Exists(BackupIntegrityPath)) throw new InvalidDataException(primaryFailure + " No integrity-protected backup item-state save is available.");
        RuntimeItemState backup;
        try
        {
            VerifyIntegrity(BackupPath, BackupIntegrityPath);
            backup = Deserialize(BackupPath);
            ValidateOrThrow(catalog, backup, "Integrity backup item state failed validation");
        }
        catch (InvalidDataException backupError)
        {
            throw new InvalidDataException(primaryFailure + " Integrity backup recovery also failed: " + backupError.Message, backupError);
        }
        RestoreIntegrityBackupAtomically();
        return new RuntimeItemRecoveryResult(backup, "integrity-backup", true, primaryFailure);
    }
    public void CleanupStaleTemporaryFile()
    {
        if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
    }
    private string IntegrityPath => _path + ".sha256";
    private string BackupIntegrityPath => BackupPath + ".sha256";
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static void VerifyIntegrity(string dataPath, string integrityPath)
    {
        var expected = File.ReadAllText(integrityPath).Trim();
        if (expected.Length != 64 || expected.Any(x => !Uri.IsHexDigit(x))) throw new InvalidDataException("Item-state integrity record is malformed.");
        var actual = ComputeSha256(dataPath);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Item-state integrity verification failed.");
    }
    private static void WriteIntegrityAtomically(string dataPath, string integrityPath)
    {
        var temporaryIntegrityPath = integrityPath + ".tmp";
        if (File.Exists(temporaryIntegrityPath)) File.Delete(temporaryIntegrityPath);
        try
        {
            File.WriteAllText(temporaryIntegrityPath, ComputeSha256(dataPath) + Environment.NewLine);
            File.Move(temporaryIntegrityPath, integrityPath, true);
        }
        catch
        {
            if (File.Exists(temporaryIntegrityPath)) File.Delete(temporaryIntegrityPath);
            throw;
        }
    }
    private void RestoreIntegrityBackupAtomically()
    {
        var integrityTemporaryPath = IntegrityPath + ".tmp";
        CleanupStaleTemporaryFile();
        if (File.Exists(integrityTemporaryPath)) File.Delete(integrityTemporaryPath);
        try
        {
            File.Copy(BackupPath, TemporaryPath, true);
            File.Copy(BackupIntegrityPath, integrityTemporaryPath, true);
            VerifyIntegrity(TemporaryPath, integrityTemporaryPath);
            using (var stream = new FileStream(TemporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read)) _ = JsonSerializer.Deserialize<RuntimeItemState>(stream, Options) ?? throw new InvalidDataException("Integrity backup item-state copy did not deserialize.");
            File.Move(TemporaryPath, _path, true);
            File.Move(integrityTemporaryPath, IntegrityPath, true);
            VerifyIntegrity(_path, IntegrityPath);
        }
        catch
        {
            if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
            if (File.Exists(integrityTemporaryPath)) File.Delete(integrityTemporaryPath);
            throw;
        }
    }
    private static void ValidateOrThrow(RuntimeItemCatalog catalog, RuntimeItemState state, string prefix)
    {
        var report = RuntimeItemValidator.Validate(catalog, state.Items, state.Corpses);
        if (!report.Passed) throw new InvalidDataException(prefix + ": " + string.Join("; ", report.Issues.Select(x => $"{x.Code}:{x.InstanceId}:{x.Message}")));
    }
    private RuntimeItemState Deserialize(string path)
    {
        try { return JsonSerializer.Deserialize<RuntimeItemState>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Persisted item state deserialized to null."); }
        catch (JsonException ex) { throw new InvalidDataException($"Persisted item state is corrupt: {path}", ex); }
    }
    private void RestoreBackupAtomically()
    {
        CleanupStaleTemporaryFile();
        try
        {
            File.Copy(BackupPath, TemporaryPath, true);
            using (var stream = new FileStream(TemporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read)) _ = JsonSerializer.Deserialize<RuntimeItemState>(stream, Options) ?? throw new InvalidDataException("Backup item-state copy did not deserialize.");
            File.Move(TemporaryPath, _path, true);
        }
        catch
        {
            if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
            throw;
        }
    }
    private void WriteAtomically(RuntimeItemState state)
    {
        CleanupStaleTemporaryFile();
        try
        {
            File.WriteAllText(TemporaryPath, JsonSerializer.Serialize(state, Options));
            using (var stream = new FileStream(TemporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read)) _ = JsonSerializer.Deserialize<RuntimeItemState>(stream, Options) ?? throw new InvalidDataException("Temporary item-state write did not deserialize.");
            File.Move(TemporaryPath, _path, true);
        }
        catch
        {
            if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
            throw;
        }
    }
    private static RuntimeItemState Empty() => new(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
}

// Combined-cumulative compatibility surface retained from Character and NPC/AI passes.
public sealed record RuntimeEquipmentModifierTotals(int ArmorClassBonus, int DamageResistanceBonus, int AccuracyBonus, int SpeedBonus, IReadOnlyList<string> SourceInstanceIds)
{
    public IReadOnlyDictionary<int, int> AbilityModifiers { get; init; } = new Dictionary<int, int>();
}

public sealed record RuntimeInventoryWeightEntry(string InstanceId, string CanonicalItemId, int UnitWeight, int Quantity, long TotalWeight, string LocationKind, string LocationId);
public sealed record RuntimeInventoryWeightTotals(string OwnerId, long TotalWeight, IReadOnlyList<RuntimeInventoryWeightEntry> Entries, string Formula, string EvidenceClassification);
public sealed record RuntimeCorpseRecoveryResult(string CorpseId, int RootItemsRecovered, long GoldRecovered);
public sealed record RuntimeStackTransferResult(RuntimeItemInstance Source, RuntimeItemInstance Destination, int QuantityMoved, bool SourceRemoved);
public sealed record RuntimeInventoryOwnershipResult(string RootInstanceId, string? OwnerId, IReadOnlyList<string> UpdatedInstanceIds);
public sealed record RuntimeGoldTransfer(long SourceBefore, long SourceAfter, long TargetBefore, long TargetAfter, long Amount, string EvidenceClassification);

public static class RuntimeCharacterGold
{
    public const string EvidenceClassification = "STRONGLY CORRELATED";
    public const string Formula = "single non-negative carried-gold scalar; checked whole-unit debit/credit";

    public static long Validate(long value) => value >= 0 ? value : throw new InvalidOperationException("Carried gold cannot be negative.");
    public static long Credit(long current, long amount)
    {
        Validate(current);
        if (amount < 0) throw new InvalidOperationException("Gold credit cannot be negative.");
        return checked(current + amount);
    }
    public static long Debit(long current, long amount)
    {
        Validate(current);
        if (amount < 0) throw new InvalidOperationException("Gold debit cannot be negative.");
        if (amount > current) throw new InvalidOperationException("Insufficient carried gold.");
        return checked(current - amount);
    }
    public static RuntimeGoldTransfer Transfer(long source, long target, long amount)
    {
        if (amount <= 0) throw new InvalidOperationException("Gold transfer amount must be positive.");
        var sourceAfter = Debit(source, amount);
        var targetAfter = Credit(target, amount);
        if (checked(sourceAfter + targetAfter) != checked(source + target)) throw new InvalidOperationException("Gold conservation failed.");
        return new RuntimeGoldTransfer(source, sourceAfter, target, targetAfter, amount, EvidenceClassification);
    }
}

public sealed partial class RuntimeItemEngine
{
    public RuntimeEquipmentModifierTotals EquipmentModifierTotals(string ownerId)
    {
        lock (_gate)
        {
            var equipped = _items.Values.Where(x => Same(x.OwnerId, ownerId) && x.EquippedSlot is not null).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
            var armorClass = 0;
            var damageResistance = 0;
            var accuracy = 0;
            var speed = 0;
            var abilities = new Dictionary<int, int>();
            foreach (var item in equipped)
            {
                var definition = _catalog.Require(item.CanonicalItemId);
                armorClass = checked(armorClass + definition.ArmorClassBonus);
                damageResistance = checked(damageResistance + definition.DamageResistanceBonus);
                accuracy = checked(accuracy + definition.AccuracyBonus);
                speed = checked(speed + definition.SpeedBonus);
                foreach (var modifier in definition.EquipmentAbilityModifiers)
                    abilities[modifier.Key] = checked(abilities.GetValueOrDefault(modifier.Key) + modifier.Value);
            }
            return new RuntimeEquipmentModifierTotals(armorClass, damageResistance, accuracy, speed, equipped.Select(x => x.InstanceId).ToArray())
            {
                AbilityModifiers = abilities.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            };
        }
    }

    public RuntimeInventoryWeightTotals InventoryWeight(string ownerId)
    {
        lock (_gate)
        {
            var entries = _items.Values
                .Where(x => Same(ResolveCompatibilityOwner(x), ownerId) && CompatibilityCarriedLocation(x))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(x =>
                {
                    var definition = _catalog.Require(x.CanonicalItemId);
                    var total = checked((long)definition.Weight * x.StackQuantity);
                    return new RuntimeInventoryWeightEntry(x.InstanceId, x.CanonicalItemId, definition.Weight, x.StackQuantity, total, x.LocationKind, x.LocationId);
                }).ToArray();
            var totalWeight = entries.Aggregate(0L, (sum, entry) => checked(sum + entry.TotalWeight));
            return new RuntimeInventoryWeightTotals(ownerId, totalWeight, entries, "sum(item.economy.weight * stack_quantity) across character, equipment, and nested-container ownership", "PROVEN");
        }
    }

    public RuntimeItemInstance TransferOwnedTree(string instanceId, string fromOwnerId, string locationKind, string locationId, string? toOwnerId)
    {
        return Atomic(() =>
        {
            var root = RequireOwned(instanceId, fromOwnerId);
            if (root.EquippedSlot is not null) throw new InvalidOperationException("Equipped items must be removed before transfer.");
            var updated = TransferCore(instanceId, locationKind, locationId, toOwnerId, null, false);
            var changed = new List<string> { updated.InstanceId };
            foreach (var descendantId in CollectSubtreeIdsCore(instanceId).Where(x => !Same(x, instanceId)))
            {
                var descendant = Require(descendantId);
                Replace(descendant with { OwnerId = toOwnerId, EquippedSlot = null });
                changed.Add(descendantId);
            }
            return updated;
        });
    }

    public RuntimeStackTransferResult MergeStack(string sourceInstanceId, string destinationInstanceId, int quantity)
    {
        return Atomic(() =>
        {
            if (Same(sourceInstanceId, destinationInstanceId)) throw new InvalidOperationException("A stack cannot merge with itself.");
            var source = Require(sourceInstanceId);
            var destination = Require(destinationInstanceId);
            if (quantity <= 0 || quantity > source.StackQuantity) throw new InvalidOperationException("Merge quantity is outside the source stack.");
            if (!CompatibilityMergeIdentity(source, destination)) throw new InvalidOperationException("Item stacks do not have identical merge identity.");
            var maximum = CompatibilityMaximumStack(destination);
            var destinationQuantity = checked(destination.StackQuantity + quantity);
            if (destinationQuantity > maximum) throw new InvalidOperationException($"Merged stack exceeds maximum quantity {maximum}.");
            var sourceRemoved = quantity == source.StackQuantity;
            var updatedSource = sourceRemoved ? source with { StackQuantity = 0 } : source with { StackQuantity = source.StackQuantity - quantity };
            var updatedDestination = destination with { StackQuantity = destinationQuantity };
            if (sourceRemoved) _items.Remove(source.InstanceId); else Replace(updatedSource);
            Replace(updatedDestination);
            return new RuntimeStackTransferResult(updatedSource, updatedDestination, quantity, sourceRemoved);
        });
    }

    public IReadOnlyList<RuntimeItemInstance> RemoveCurse(string characterId) => Atomic(() =>
    {
        var removed = new List<RuntimeItemInstance>();
        foreach (var item in _items.Values
                     .Where(x => Same(x.OwnerId, characterId) && x.EquippedSlot is not null && (_catalog.Require(x.CanonicalItemId).CursedWhenEquipped || CompatibilityMetadataTrue(x, "cursed_no_remove")))
                     .OrderBy(x => x.EquippedSlot, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                     .ToArray())
        {
            removed.Add(TransferCore(item.InstanceId, RuntimeItemLocations.Character, characterId, characterId, null, false));
        }
        return removed;
    });

    public RuntimeCorpseRecoveryResult RecoverOwnedCorpse(string corpseId, string characterId) => Atomic(() =>
    {
        var corpse = RequireCorpse(corpseId);
        if (!Same(corpse.CharacterId, characterId)) throw new InvalidOperationException("Corpse ownership does not match the recovering character.");
        var roots = _items.Values.Where(item => item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(item.LocationId, corpseId)).OrderBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var item in roots)
        {
            TransferCore(item.InstanceId, RuntimeItemLocations.Character, characterId, characterId, null, false);
            foreach (var descendantId in CollectSubtreeIdsCore(item.InstanceId).Where(x => !Same(x, item.InstanceId)))
            {
                var descendant = Require(descendantId);
                Replace(descendant with { OwnerId = characterId, EquippedSlot = null });
            }
        }
        var gold = corpse.Gold;
        _corpses[corpseId] = corpse with { Gold = 0, CleanedUp = true };
        return new RuntimeCorpseRecoveryResult(corpseId, roots.Length, gold);
    });

    public IReadOnlyList<RuntimeItemInstance> CorpseInventory(string corpseId)
    {
        lock (_gate)
        {
            RequireCorpse(corpseId);
            return _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, corpseId)).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public IReadOnlyList<RuntimeItemInstance> NpcInventory(string npcInstanceId)
    {
        lock (_gate) return _items.Values.Where(x => Same(x.OwnerId, npcInstanceId) && x.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) && x.EquippedSlot is null).OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<RuntimeItemInstance> NpcEquipment(string npcInstanceId)
    {
        lock (_gate) return _items.Values.Where(x => Same(x.OwnerId, npcInstanceId) && x.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) && x.EquippedSlot is not null).OrderBy(x => x.EquippedSlot, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public RuntimeItemInstance MoveFromNpc(string instanceId, string npcId, string destinationOwnerId) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, npcId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, npcId)) throw new InvalidOperationException("Item is not owned by that NPC.");
        return TransferCore(instanceId, RuntimeItemLocations.Character, destinationOwnerId, destinationOwnerId, null, false);
    });

    public RuntimeItemInstance DropFromNpc(string instanceId, string npcId, string roomId) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, npcId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || !Same(item.LocationId, npcId)) throw new InvalidOperationException("Item is not owned by that NPC.");
        return TransferCore(instanceId, RuntimeItemLocations.Room, roomId, null, null, false);
    });

    public RuntimeItemInstance CreateCorpseItem(string corpseId, string canonicalId, int quantity, IReadOnlyDictionary<string, string>? metadata = null) => Atomic(() =>
    {
        RequireCorpse(corpseId);
        var definition = _catalog.Require(canonicalId);
        if (quantity < 1 || (!definition.Stackable && quantity != 1)) throw new InvalidOperationException("Invalid corpse item quantity.");
        var item = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, null, RuntimeItemLocations.Corpse, corpseId, null, definition.DefaultCharges, quantity, null, metadata);
        AddCreatedUnique(item);
        return item;
    });

    public RuntimeItemInstance EquipNpc(string instanceId, string npcInstanceId, string exactSlot) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, npcInstanceId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || item.EquippedSlot is not null) throw new InvalidOperationException("NPC item must be carried before equipping.");
        var equipment = _catalog.Equipment(item.CanonicalItemId) ?? throw new InvalidOperationException("Item has no generated equipment relationship.");
        if (!_catalog.IsLegalSlot(exactSlot) || !Same(equipment.Slot, exactSlot)) throw new InvalidOperationException("NPC equipment slot does not match the generated relationship.");
        if (_items.Values.Any(x => Same(x.OwnerId, npcInstanceId) && Same(x.EquippedSlot, exactSlot))) throw new InvalidOperationException("NPC equipment slot is occupied.");
        return TransferCore(instanceId, RuntimeItemLocations.Equipment, npcInstanceId, npcInstanceId, exactSlot, false);
    });

    public RuntimeItemInstance UnequipNpc(string instanceId, string npcInstanceId) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, npcInstanceId);
        if (item.EquippedSlot is null) throw new InvalidOperationException("NPC item is not equipped.");
        return TransferCore(instanceId, RuntimeItemLocations.Npc, npcInstanceId, npcInstanceId, null, false);
    });

    public int CleanupCorpseExact(string corpseId, DateTimeOffset now) => Atomic(() =>
    {
        var corpse = RequireCorpse(corpseId);
        if (corpse.DecaysAtUtc is null || corpse.DecaysAtUtc > now) throw new InvalidOperationException("Corpse is not due for cleanup.");
        var ids = _items.Values.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && Same(x.LocationId, corpseId)).Select(x => x.InstanceId).ToArray();
        foreach (var id in ids) foreach (var subtreeId in CollectSubtreeIdsCore(id)) _items.Remove(subtreeId);
        _corpses[corpseId] = corpse with { CleanedUp = true, Gold = 0 };
        return ids.Length;
    });

    public RuntimeItemInstance ConsumeNpcQuantity(string instanceId, string npcInstanceId, int quantity) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, npcInstanceId);
        if (quantity <= 0 || quantity > item.StackQuantity) throw new InvalidOperationException("NPC consumption quantity is outside the stack.");
        if (quantity == item.StackQuantity)
        {
            _items.Remove(item.InstanceId);
            return item with { StackQuantity = 0 };
        }
        return Replace(item with { StackQuantity = item.StackQuantity - quantity });
    });

    public RuntimeItemInstance UseNpc(string instanceId, string npcInstanceId) => Atomic(() =>
    {
        var item = RequireOwned(instanceId, npcInstanceId);
        if (!item.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) && !item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("NPC item is not in an executable NPC location.");
        var definition = _catalog.Require(item.CanonicalItemId);
        if (!definition.Consumable && item.Charges <= 0) throw new InvalidOperationException("Item is not usable.");
        if (item.Charges > 0) return Replace(item with { Charges = item.Charges - 1 });
        if (item.StackQuantity > 1) return Replace(item with { StackQuantity = item.StackQuantity - 1 });
        _items.Remove(item.InstanceId);
        return item with { StackQuantity = 0 };
    });

    private string? ResolveCompatibilityOwner(RuntimeItemInstance item)
    {
        if (!string.IsNullOrWhiteSpace(item.OwnerId)) return item.OwnerId;
        var cursor = item;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (cursor.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) && visited.Add(cursor.InstanceId) && _items.TryGetValue(cursor.LocationId, out var parent))
        {
            if (!string.IsNullOrWhiteSpace(parent.OwnerId)) return parent.OwnerId;
            cursor = parent;
        }
        return null;
    }

    private static bool CompatibilityCarriedLocation(RuntimeItemInstance item) =>
        item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) ||
        item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) ||
        item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase);

    private static bool CompatibilityMetadataTrue(RuntimeItemInstance item, string key) =>
        item.Metadata?.TryGetValue(key, out var raw) == true && bool.TryParse(raw, out var value) && value;

    private static bool CompatibilityMergeIdentity(RuntimeItemInstance left, RuntimeItemInstance right)
    {
        if (!Same(left.CanonicalItemId, right.CanonicalItemId) || left.Charges != right.Charges || left.Durability != right.Durability || left.EquippedSlot is not null || right.EquippedSlot is not null) return false;
        var l = left.Metadata ?? new Dictionary<string, string>();
        var r = right.Metadata ?? new Dictionary<string, string>();
        return l.Count == r.Count && l.All(pair => r.TryGetValue(pair.Key, out var value) && Same(pair.Value, value));
    }

    private static int CompatibilityMaximumStack(RuntimeItemInstance item)
    {
        if (item.Metadata?.TryGetValue("maximum_stack", out var raw) == true && int.TryParse(raw, out var value) && value > 0) return value;
        return int.MaxValue;
    }
}
