using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicGangShopSlot(int Slot, string ItemId, int Quantity, int Price, int CurrencyType);
public sealed record RuntimeClassicGangShopState(string ShopId, int MarkupPercent, IReadOnlyList<RuntimeClassicGangShopSlot> Slots, DateTimeOffset UpdatedAtUtc);

public sealed class RuntimeClassicGangShopStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _root;
    private readonly string _projectRoot;
    private readonly string _worldId;
    private readonly object _gate = new();

    public RuntimeClassicGangShopStore(string projectRoot, string worldId)
    {
        _projectRoot = projectRoot;
        _worldId = worldId;
        _root = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "ClassicGangShops");
        Directory.CreateDirectory(_root);
    }

    public RuntimeClassicGangShopState Read(RuntimeServiceDefinition shop)
    {
        lock (_gate) return LoadOrCreate(shop);
    }

    public T Execute<T>(RuntimeServiceDefinition shop, Func<RuntimeClassicGangShopState, (RuntimeClassicGangShopState State, T Result)> action)
    {
        lock (_gate)
        {
            var before = LoadOrCreate(shop);
            var outcome = action(before);
            Validate(outcome.State, shop.Id);
            Save(outcome.State with { UpdatedAtUtc = DateTimeOffset.UtcNow });
            return outcome.Result;
        }
    }

    private RuntimeClassicGangShopState LoadOrCreate(RuntimeServiceDefinition shop)
    {
        var path = PathFor(shop.Id);
        if (File.Exists(path))
        {
            var loaded = JsonSerializer.Deserialize<RuntimeClassicGangShopState>(File.ReadAllText(path), JsonOptions);
            if (loaded is not null)
            {
                Validate(loaded, shop.Id);
                return loaded;
            }
        }

        var markup = ReadInteger(shop.GeneratedRecord, "markup_percent");
        var initialSlots = LoadInitialSlots(shop);
        var created = new RuntimeClassicGangShopState(shop.Id, Math.Clamp(markup, 0, 1000), initialSlots, DateTimeOffset.UtcNow);
        Save(created);
        return created;
    }

    private IReadOnlyList<RuntimeClassicGangShopSlot> LoadInitialSlots(RuntimeServiceDefinition shop)
    {
        if (!shop.GeneratedRecord.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuntimeClassicGangShopSlot>();
        var defaults = LoadItemDefaults();
        var slots = new List<RuntimeClassicGangShopSlot>();
        var index = 0;
        foreach (var entry in inventory.EnumerateArray().Take(10))
        {
            var itemId = entry.TryGetProperty("item_ref", out var itemRef) ? itemRef.GetString() : null;
            var quantity = Math.Clamp(ReadInteger(entry, "normal_qty"), 0, 20);
            if (!string.IsNullOrWhiteSpace(itemId) && quantity > 0)
            {
                var value = defaults.GetValueOrDefault(itemId, (Price: 0, Currency: 0));
                slots.Add(new RuntimeClassicGangShopSlot(index, itemId, quantity, value.Price, value.Currency));
            }
            index++;
        }
        return slots;
    }

    private Dictionary<string, (int Price, int Currency)> LoadItemDefaults()
    {
        var result = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(_projectRoot, "Data", _worldId, "items.json");
        if (!File.Exists(path)) return result;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return result;
        foreach (var record in records.EnumerateArray())
        {
            var id = record.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var economy = record.TryGetProperty("economy", out var economyValue) && economyValue.ValueKind == JsonValueKind.Object ? economyValue : record;
            var price = Math.Clamp(ReadInteger(economy, "cost"), 0, 65535);
            var currency = Math.Clamp(ReadInteger(economy, "cost_type"), 0, 4);
            result[id] = (price, currency);
        }
        return result;
    }

    private void Save(RuntimeClassicGangShopState state)
    {
        var path = PathFor(state.ShopId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, path, true);
    }

    private string PathFor(string shopId) => Path.Combine(_root, RuntimeIdentity.Normalize(shopId, "shop") + ".json");

    private static void Validate(RuntimeClassicGangShopState state, string shopId)
    {
        if (!state.ShopId.Equals(shopId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("classic-gang-shop-id-mismatch");
        if (state.MarkupPercent is < 0 or > 1000) throw new InvalidDataException("classic-gang-shop-markup-out-of-range");
        if (state.Slots.Count > 10) throw new InvalidDataException("classic-gang-shop-slot-overflow");
        if (state.Slots.Select(x => x.Slot).Distinct().Count() != state.Slots.Count) throw new InvalidDataException("classic-gang-shop-duplicate-slot");
        if (state.Slots.Any(x => x.Slot is < 0 or >= 10 || x.Quantity is < 1 or > 20 || x.Price is < 0 or > 65535 || x.CurrencyType is < 0 or > 4 || string.IsNullOrWhiteSpace(x.ItemId)))
            throw new InvalidDataException("classic-gang-shop-invalid-slot");
    }

    private static int ReadInteger(JsonElement record, string property)
    {
        if (!record.TryGetProperty(property, out var value)) return 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number
            : int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : 0;
    }
}

/// <summary>
/// Exact Classic type-11 gang-shop management recovered from WCCMMUD.DLL 0x452B1E-0x453918.
/// The native shop record owns ten merchandise slots. Each slot stores item id, quantity (1..20),
/// a WORD price (native item defaults may reach 65535; explicit STOCK prices clamp to 9999),
/// and currency type (0..4); MARKUP is a separate 0..1000 percent field.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly Dictionary<string, RuntimeClassicGangShopStore> _classicGangShopStores = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _classicGangShopStoreGate = new();

    private RuntimeClassicGangShopStore GangShopStore(string worldId)
    {
        lock (_classicGangShopStoreGate)
        {
            if (!_classicGangShopStores.TryGetValue(worldId, out var store))
            {
                store = new RuntimeClassicGangShopStore(_characters.ProjectRoot, worldId);
                _classicGangShopStores[worldId] = store;
            }
            return store;
        }
    }

    public string ExecuteClassicGangShopCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("A persistent character is required.");
        var service = ResolveCurrentClassicGangShop(session);
        if (service is null)
        {
            return command.Name.ToUpperInvariant() switch
            {
                "STOCK" => "You cannot STOCK if you are not in your gangs shop!",
                "UNSTOCK" => "You cannot UNSTOCK if you are not in your gangs shop!",
                "MARKUP" => "You cannot MARKUP if you are not in your gangs shop!",
                _ => "This is not a gang owned shop."
            };
        }

        if (!HasClassicGangShopController(session, actor.Id))
        {
            return command.Name.ToUpperInvariant() switch
            {
                "STOCK" => "You do not have the correct item to stock this shop.",
                "UNSTOCK" => "You do not have the correct item to unstock this shop.",
                "MARKUP" => "You do not have the correct item to set the markup value for this shop.",
                _ => "This is not a gang owned shop."
            };
        }

        return command.Name switch
        {
            "stock" => ExecuteClassicGangShopStock(session, actor.Id, service, command),
            "unstock" => ExecuteClassicGangShopUnstock(session, actor.Id, service, command),
            "markup" => ExecuteClassicGangShopMarkup(session, service, command),
            _ => throw new InvalidOperationException("Unsupported Classic gang-shop management command.")
        };
    }

    private RuntimeServiceDefinition? ResolveCurrentClassicGangShop(RuntimeSession session)
    {
        var catalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        return ResolveServicesAtRoom(catalog, session.CurrentRoom.RoomNumber).FirstOrDefault(IsClassicGangShopService);
    }

    private static bool IsClassicGangShopService(RuntimeServiceDefinition service)
    {
        var record = service.GeneratedRecord;
        var type = ReadGeneratedInteger(record, "decoded_shop_type");
        if (type == 0) type = ReadGeneratedInteger(record, "service_type");
        return type == 11;
    }

    private bool HasClassicGangShopController(RuntimeSession session, string characterId)
    {
        var gangHousePath = Path.Combine(_characters.ProjectRoot, "Data", session.World.Definition.Id, "gang_houses.json");
        var houses = RuntimeClassicGangHouseCatalog.Load(gangHousePath);
        var house = houses.Records.FirstOrDefault(record => record.Locations.Any(location => location.MapId == session.CurrentRoom.MapId && location.RoomNumber == session.CurrentRoom.RoomNumber));
        if (house is null) return false;
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var controllerIds = RuntimeClassicGangHouseAbilityAuthority.Resolve(items, houses)
            .Where(link => link.IsShopController && link.HouseId.Equals(house.HouseId, StringComparison.OrdinalIgnoreCase))
            .Select(link => link.ItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (controllerIds.Count == 0) return false;
        var character = _characters.LoadCharacter(characterId);
        var state = character?.ItemState;
        return state is not null && state.Items.Any(item => item.OwnerId?.Equals(characterId, StringComparison.OrdinalIgnoreCase) == true
            && item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
            && controllerIds.Contains(item.CanonicalItemId));
    }

    private string ExecuteClassicGangShopStock(RuntimeSession session, string actorId, RuntimeServiceDefinition shop, RuntimeCommand command)
    {
        if (command.Arguments.Count == 0) return "Syntax: STOCK {item} {price} {currency}";
        var character = _characters.LoadCharacter(actorId) ?? throw new InvalidOperationException("Character save was not found.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var parse = ResolveClassicGangStockItem(command.Arguments, state, actorId, catalog);
        if (parse.Item is null) return "You may not stock that item!";
        var definition = catalog.Require(parse.Item.CanonicalItemId);
        if (definition.GameLimit != 0) return "You may not stock limited items!";
        if (definition.EquipmentAbilityModifiers.ContainsKey(100) || definition.NotDroppable) return "You may not stock that item!";
        if (parse.Item.EquippedSlot is not null && definition.CursedWhenEquipped) return "You may not stock that item!";

        var remainder = parse.Remainder;
        var generated = RequireGeneratedItemRecord(_characters.ProjectRoot, session.World.Definition.Id, definition.Id);
        var price = checked((int)Math.Clamp(ReadItemCost(generated), 0L, 65535L));
        var currency = Math.Clamp(ReadItemCostType(generated), 0, 4);
        ParseClassicGangShopPriceCurrency(remainder, ref price, ref currency);

        var result = GangShopStore(session.World.Definition.Id).Execute(shop, before =>
        {
            var slots = before.Slots.OrderBy(x => x.Slot).ToList();
            RuntimeClassicGangShopSlot? target = slots.FirstOrDefault(x => x.ItemId.Equals(definition.Id, StringComparison.OrdinalIgnoreCase) && x.Quantity < 20);
            if (target is null)
            {
                var free = Enumerable.Range(0, 10).FirstOrDefault(index => slots.All(x => x.Slot != index), -1);
                if (free < 0) return (before, "No more new items may be stocked in this shop.");
                target = new RuntimeClassicGangShopSlot(free, definition.Id, 0, price, currency);
                slots.Add(target);
            }

            var updatedSlot = target with { Quantity = target.Quantity + 1, Price = price, CurrencyType = currency };
            slots.RemoveAll(x => x.Slot == target.Slot);
            slots.Add(updatedSlot);

            var remainingItems = state.Items.Select(item =>
                    item.InstanceId.Equals(parse.Item.InstanceId, StringComparison.OrdinalIgnoreCase) && item.StackQuantity > 1
                        ? item with { StackQuantity = item.StackQuantity - 1 }
                        : item)
                .Where(item => !item.InstanceId.Equals(parse.Item.InstanceId, StringComparison.OrdinalIgnoreCase) || parse.Item.StackQuantity > 1)
                .ToArray();
            var nextItemState = state with { Items = remainingItems };
            _characters.SaveCharacter(character with { ItemState = nextItemState, UpdatedAtUtc = DateTimeOffset.UtcNow },
                $"classic-gangshop:stock:{shop.Id}");

            var next = before with { Slots = slots.OrderBy(x => x.Slot).ToArray() };
            return (next, $"You add the {definition.Name} to your shops stock.");
        });

        if (result.StartsWith("You add ", StringComparison.Ordinal))
            _sessions.BroadcastRoom(session, $"You see {session.Actor.DisplayName} add a {definition.Name} to the shops stock.", includeSource: false);
        return result;
    }

    private string ExecuteClassicGangShopUnstock(RuntimeSession session, string actorId, RuntimeServiceDefinition shop, RuntimeCommand command)
    {
        if (command.Arguments.Count == 0) return "Syntax: UNSTOCK {item}";
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var argument = command.ArgumentText.Trim();
        if (argument.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return GangShopStore(session.World.Definition.Id).Execute(shop, before =>
            {
                if (before.Slots.Count == 0 || before.Slots.All(x => x.Quantity <= 0)) return (before, "There are no stocked items to remove.");
                foreach (var slot in before.Slots.OrderBy(x => x.Slot))
                {
                    for (var i = 0; i < slot.Quantity; i++)
                    {
                        var ground = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), slot.ItemId, null,
                            RuntimeItemLocations.Room, session.CurrentRoom.Id, null, catalog.Require(slot.ItemId).DefaultCharges, 1, null,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["classic_gangshop_unstock"] = shop.Id });
                        Living(session).DropGroundItem(ground, session.CurrentRoom.Id);
                    }
                }
                _sessions.BroadcastRoom(session, $"You see {session.Actor.DisplayName} remove all items from the gang shop and place them on the floor.", includeSource: false);
                return (before with { Slots = Array.Empty<RuntimeClassicGangShopSlot>() }, "You remove all the items from your shop and place them on the floor.");
            });
        }

        return GangShopStore(session.World.Definition.Id).Execute(shop, before =>
        {
            var matches = before.Slots.Where(slot => catalog.Matches(slot.ItemId, argument)).OrderBy(slot => slot.Quantity).ThenBy(slot => slot.Slot).ToArray();
            if (matches.Length == 0) return (before, "This item is not currently in stock.");
            var slot = matches[0];
            var character = _characters.LoadCharacter(actorId) ?? throw new InvalidOperationException("Character save was not found.");
            var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
            engine.Create(slot.ItemId, RuntimeItemLocations.Character, actorId, ownerId: actorId,
                metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["classic_gangshop_unstock"] = shop.Id });
            _characters.SaveCharacter(character with { ItemState = engine.Snapshot(state.Gold, state.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow },
                $"classic-gangshop:unstock:{shop.Id}");

            var slots = before.Slots.ToList();
            slots.RemoveAll(x => x.Slot == slot.Slot);
            if (slot.Quantity > 1) slots.Add(slot with { Quantity = slot.Quantity - 1 });
            var name = catalog.Require(slot.ItemId).Name;
            _sessions.BroadcastRoom(session, $"You see {session.Actor.DisplayName} remove a {name} from the shops stock.", includeSource: false);
            return (before with { Slots = slots.OrderBy(x => x.Slot).ToArray() }, $"You remove {name} from the shops stock.");
        });
    }

    private string ExecuteClassicGangShopMarkup(RuntimeSession session, RuntimeServiceDefinition shop, RuntimeCommand command)
    {
        if (command.Arguments.Count == 0) return "Syntax: MARKUP {percentage value}";
        var parsed = int.TryParse(command.Arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        var markup = Math.Clamp(parsed, 0, 1000);
        return GangShopStore(session.World.Definition.Id).Execute(shop, before =>
            (before with { MarkupPercent = markup }, $"New gang shop markup value set to {markup} percent."));
    }

    public bool TryRenderClassicGangShopInventory(RuntimeSession session, out string output)
    {
        output = string.Empty;
        var shop = ResolveCurrentClassicGangShop(session);
        if (shop is null) return false;
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var listingCharm = session.Actor is RuntimeCharacterActor actor
            ? ResolveEconomyCharm(_characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found."))
            : 0;
        var state = GangShopStore(session.World.Definition.Id).Read(shop);
        if (state.Slots.Count == 0) { output = "There are no items for sale here."; return true; }
        var lines = new List<string> { "The following items are for sale here:", string.Empty, $"{"Item",-30}{"Quantity",-12}Price", new string('-', 54) };
        foreach (var group in state.Slots.GroupBy(x => new { x.ItemId, x.Price, x.CurrencyType }).OrderBy(x => x.Min(y => y.Slot)))
        {
            var qty = group.Sum(x => x.Quantity);
            var unit = checked((long)group.Key.Price * CostTypeCopperMultiplier(group.Key.CurrencyType));
            var marked = RuntimeClassicEconomyPricing.QuoteBuyCopper(unit, state.MarkupPercent, listingCharm);
            lines.Add($"{catalog.Require(group.Key.ItemId).Name,-30}{qty,-12}{(marked == 0 ? "Free" : RuntimeBundleCEconomyAuthority.FormatCopperValue(marked))}");
        }
        output = string.Join(Environment.NewLine, lines);
        return true;
    }


    public bool TryRejectClassicGangShopSell(RuntimeSession session, out string output)
    {
        output = string.Empty;
        if (ResolveCurrentClassicGangShop(session) is null) return false;
        // Native SELL owner 0x41C43C checks shop_type == 11 at 0x41C486 and exits
        // immediately with this exact message. Player-managed gang shops are BUY-only.
        output = "You may not sell items to a gang shop.";
        return true;
    }

    public bool TryBuyClassicGangShopItem(RuntimeSession session, string query, out string output)
    {
        output = string.Empty;
        if (session.Actor is not RuntimeCharacterActor actor) return false;
        var shop = ResolveCurrentClassicGangShop(session);
        if (shop is null) return false;
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        output = GangShopStore(session.World.Definition.Id).Execute(shop, before =>
        {
            var matches = before.Slots.Where(slot => catalog.Matches(slot.ItemId, query.Trim())).OrderBy(slot => slot.Slot).ToArray();
            if (matches.Length == 0) return (before, $"No stocked item matches '{query.Trim()}'.");
            var canonical = matches.Select(x => x.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (canonical.Length != 1) return (before, $"Multiple stocked items match '{query.Trim()}'.");
            var slot = matches[0];
            var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
            var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            var unit = checked((long)slot.Price * CostTypeCopperMultiplier(slot.CurrencyType));
            var total = RuntimeClassicEconomyPricing.QuoteBuyCopper(unit, before.MarkupPercent, ResolveEconomyCharm(character));
            var settled = RuntimeBundleCEconomyAuthority.DebitCopperValue(state, total);
            var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
            engine.Create(slot.ItemId, RuntimeItemLocations.Character, actor.Id, ownerId: actor.Id,
                metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["classic_gangshop_purchase"] = shop.Id });
            _characters.SaveCharacter(character with { ItemState = engine.Snapshot(settled.Gold, settled.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow },
                $"classic-gangshop:buy:{shop.Id}");
            var slots = before.Slots.ToList();
            slots.RemoveAll(x => x.Slot == slot.Slot);
            if (slot.Quantity > 1) slots.Add(slot with { Quantity = slot.Quantity - 1 });
            var priceText = total == 0 ? "free" : RuntimeBundleCEconomyAuthority.FormatCopperValue(total);
            return (before with { Slots = slots.OrderBy(x => x.Slot).ToArray() }, $"You buy 1 {catalog.Require(slot.ItemId).Name} for {priceText}. {slot.Quantity - 1} remain.");
        });
        return true;
    }

    private static (RuntimeItemInstance? Item, IReadOnlyList<string> Remainder) ResolveClassicGangStockItem(
        IReadOnlyList<string> arguments, RuntimeItemState state, string actorId, RuntimeItemCatalog catalog)
    {
        var carried = state.Items.Where(item => item.OwnerId?.Equals(actorId, StringComparison.OrdinalIgnoreCase) == true
            && (item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.EquippedSlot is null ? 0 : 1)
            .ToArray();
        for (var length = arguments.Count; length >= 1; length--)
        {
            var query = string.Join(" ", arguments.Take(length));
            var exact = carried.FirstOrDefault(item => item.InstanceId.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return (exact, arguments.Skip(length).ToArray());
            var matches = carried.Where(item => catalog.Matches(item.CanonicalItemId, query)).ToArray();
            if (matches.Length > 0 && matches.Select(item => item.CanonicalItemId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
                return (matches[0], arguments.Skip(length).ToArray());
        }
        return (null, arguments);
    }

    private static void ParseClassicGangShopPriceCurrency(IReadOnlyList<string> remainder, ref int price, ref int currency)
    {
        if (remainder.Count == 0) return;
        var joined = string.Join(" ", remainder).Trim();
        var currencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["copper farthings"] = 0, ["copper"] = 0,
            ["silver nobles"] = 1, ["silver"] = 1,
            ["gold crowns"] = 2, ["gold"] = 2,
            ["platinum pieces"] = 3, ["platinum"] = 3,
            ["user defined currency type"] = 4, ["user"] = 4
        };
        foreach (var pair in currencies.OrderByDescending(x => x.Key.Length))
        {
            if (!joined.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase)) continue;
            currency = pair.Value;
            joined = joined[..^pair.Key.Length].Trim();
            break;
        }
        if (joined.Length == 0) return;
        if (joined.Equals("free", StringComparison.OrdinalIgnoreCase)) { price = 0; return; }
        var parsed = int.TryParse(joined.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        price = Math.Clamp(parsed, 0, 9999);
    }
}
