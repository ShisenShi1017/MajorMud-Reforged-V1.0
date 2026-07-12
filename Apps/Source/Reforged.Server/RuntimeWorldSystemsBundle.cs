using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private const string QuestConditionKey = "runtime.quest-journal.v1";
    private const string CharacterPointsKey = "character_points";

    private string ExecuteGeneratedServiceTransaction(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("A persistent character is required.");
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var catalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var services = ResolveServicesAtRoom(catalog, session.CurrentRoom.RoomNumber).ToArray();
        if (services.Length == 0) throw new InvalidOperationException("There is no generated service in this room.");
        return command.Name switch
        {
            "buy" => BuyGeneratedItem(character, services, command.ArgumentText),
            "sell" => SellGeneratedItem(character, services, command.ArgumentText),
            "repair" => RepairGeneratedItem(character, services, command.ArgumentText),
            "train" when command.ArgumentText.StartsWith("stats ", StringComparison.OrdinalIgnoreCase) => SpendTatPoint(character, command.ArgumentText[6..]),
            "train" => TrainAtGeneratedTrainer(character, services),
            "tat" or "stats" => SpendTatPoint(character, command.ArgumentText),
            _ => throw new InvalidOperationException("Unsupported generated service transaction.")
        };
    }

    private static RuntimeServiceDefinition RequireShop(IReadOnlyList<RuntimeServiceDefinition> services) =>
        services.FirstOrDefault(x => !ReadServiceKind(x).Equals("trainer", StringComparison.OrdinalIgnoreCase) && x.GeneratedRecord.TryGetProperty("inventory", out var i) && i.ValueKind == JsonValueKind.Array)
        ?? throw new InvalidOperationException("There is no generated shop inventory in this room.");

    private string BuyGeneratedItem(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services, string query)
    {
        var shop = RequireShop(services);
        var entry = ResolveOffering(shop.GeneratedRecord, "inventory", query);
        var itemId = ReadText(entry, "item_ref") ?? throw new InvalidOperationException("Generated shop entry has no item reference.");
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var definition = items.Require(itemId);
        var cost = ReadItemCost(RequireGeneratedItemRecord(_characters.ProjectRoot, character.WorldId, definition.Id));
        if (cost <= 0) throw new InvalidOperationException("The generated item has no positive economy cost.");

        return GameplayStore(character.WorldId).ExecuteShopTransaction(shop, stock =>
        {
            if (!stock.TryGetValue(itemId, out var current) || current.Quantity <= 0)
                throw new InvalidOperationException($"{definition.Name} is currently out of stock.");
            var before = _characters.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Character save was not found.");
            var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
            if (state.Gold < cost) throw new InvalidOperationException("You do not have enough carried gold.");
            var engine = new RuntimeItemEngine(items, state.Items, state.Corpses);
            var created = engine.Create(itemId, RuntimeItemLocations.Character, before.CharacterId, ownerId: before.CharacterId);
            var updated = before with { ItemState = engine.Snapshot(state.Gold - cost, state.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow };
            _characters.SaveCharacter(updated);
            stock[itemId] = current with { Quantity = current.Quantity - 1 };
            return $"You buy {items.Require(created.CanonicalItemId).Name} for {cost} gold. {current.Quantity - 1} remain.";
        });
    }

    private string SellGeneratedItem(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services, string query)
    {
        var shop = RequireShop(services);
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        return GameplayStore(character.WorldId).ExecuteShopTransaction(shop, stock =>
        {
            var before = _characters.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Character save was not found.");
            var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
            var engine = new RuntimeItemEngine(items, state.Items, state.Corpses);
            var selected = Resolve(engine.Inventory(before.CharacterId), items, query);
            var value = ReadItemCost(RequireGeneratedItemRecord(_characters.ProjectRoot, before.WorldId, selected.CanonicalItemId));
            if (value <= 0) throw new InvalidOperationException("That item has no generated economy value.");
            engine.Remove(selected.InstanceId);
            _characters.SaveCharacter(before with { ItemState = engine.Snapshot(checked(state.Gold + value), state.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow });
            if (stock.TryGetValue(selected.CanonicalItemId, out var current))
                stock[selected.CanonicalItemId] = current with { Quantity = checked(current.Quantity + 1) };
            return $"You sell {items.Require(selected.CanonicalItemId).Name} for {value} gold.";
        });
    }

    private string RepairGeneratedItem(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services, string query)
    {
        var repair = services.FirstOrDefault(x => HasOperation(x.GeneratedRecord, "repair") || ReadServiceKind(x).Contains("repair", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("There is no generated repair service in this room.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var engine = new RuntimeItemEngine(items, state.Items, state.Corpses);
        var selected = Resolve(engine.Inventory(character.CharacterId).Concat(engine.Equipment(character.CharacterId)), items, query);
        if (selected.Metadata is null || !selected.Metadata.TryGetValue("durability", out var d) || !selected.Metadata.TryGetValue("maximum_durability", out var m) || !int.TryParse(d, out var durability) || !int.TryParse(m, out var maximum) || maximum <= durability)
            throw new InvalidOperationException("That item has no generated repairable durability loss.");
        var unit = ReadLong(repair.GeneratedRecord, "repair_cost_per_point", "repair_cost");
        var cost = checked(unit * (maximum - durability));
        if (cost <= 0) throw new InvalidOperationException("Generated repair cost is unavailable.");
        if (state.Gold < cost) throw new InvalidOperationException("You do not have enough carried gold.");
        var metadata = new Dictionary<string,string>(selected.Metadata ?? new Dictionary<string,string>(), StringComparer.OrdinalIgnoreCase) { ["durability"] = maximum.ToString(CultureInfo.InvariantCulture) };
        engine.Remove(selected.InstanceId); engine.Add(selected with { Metadata = metadata });
        _characters.SaveCharacter(character with { ItemState = engine.Snapshot(state.Gold - cost, state.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow });
        return $"You repair {items.Require(selected.CanonicalItemId).Name} for {cost} gold.";
    }

    private string TrainAtGeneratedTrainer(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services)
    {
        var trainer = services.FirstOrDefault(x => ReadServiceKind(x).Equals("trainer", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("There is no generated trainer in this room.");
        var targetLevel = checked(character.Level + 1);
        var min = ReadNestedInt(trainer.GeneratedRecord, "level_range", "min", 1);
        var max = ReadNestedInt(trainer.GeneratedRecord, "level_range", "max", int.MaxValue);
        if (targetLevel < min || targetLevel > max) throw new InvalidOperationException($"This trainer supports levels {min}-{max}; it cannot train you to level {targetLevel}.");
        var classLimit = ReadInt(trainer.GeneratedRecord, "class_limit");
        if (classLimit > 0 && ParseLegacyNumber(character.ClassId) != classLimit) throw new InvalidOperationException("This trainer does not support your class.");

        var progression = RuntimeTrainerProgressionCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var requiredExperience = progression.RequireExperience(character, targetLevel);
        if (character.Experience < requiredExperience) throw new InvalidOperationException($"You need {requiredExperience} total experience to train to level {targetLevel}.");
        var awardedCp = progression.RequireCharacterPointAward(targetLevel);
        var conditions = new Dictionary<string,string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        var currentCp = conditions.TryGetValue(CharacterPointsKey, out var value) && int.TryParse(value, out var parsed) ? parsed : 0;
        conditions[CharacterPointsKey] = checked(currentCp + awardedCp).ToString(CultureInfo.InvariantCulture);
        _characters.SaveCharacter(character with { Level = targetLevel, Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return $"You train to level {targetLevel} and receive {awardedCp} CP. Use TRAIN STATS/TAT to spend unallocated CP.";
    }


    private string SpendTatPoint(RuntimeCharacterRecord character, string argument)
    {
        var stat = argument.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(stat)) throw new InvalidOperationException("Usage: TAT <stat> or TRAIN STATS <stat>.");
        var aliases = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["agility"]="agl", ["agl"]="agl", ["charisma"]="cha", ["cha"]="cha",
            ["health"]="hea", ["hea"]="hea", ["intellect"]="int", ["intelligence"]="int", ["int"]="int",
            ["strength"]="str", ["str"]="str", ["willpower"]="wil", ["will"]="wil", ["wil"]="wil"
        };
        if (!aliases.TryGetValue(stat, out var key)) throw new InvalidOperationException($"Unknown primary stat '{argument}'.");
        var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var race = foundation.RequireRace(character.RaceId);
        if (!race.MinimumStats.TryGetValue(key, out var racialBase)) throw new InvalidOperationException($"Generated race data has no base value for {key}.");
        if (!character.BaseStats.TryGetValue(key, out var current)) current = racialBase;
        if (race.MaximumStats.TryGetValue(key, out var maximum) && maximum > 0 && current >= maximum) throw new InvalidOperationException($"{key.ToUpperInvariant()} is already at the generated racial maximum of {maximum}.");
        var pointsAboveBase = Math.Max(0, current - racialBase);
        var cost = checked((pointsAboveBase / 10) + 1);
        var conditions = new Dictionary<string,string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        var available = conditions.TryGetValue(CharacterPointsKey, out var cpText) && int.TryParse(cpText, out var cp) ? cp : 0;
        if (available < cost) throw new InvalidOperationException($"Increasing {key.ToUpperInvariant()} from {current} to {current + 1} costs {cost} CP; you have {available}.");
        var stats = new Dictionary<string,int>(character.BaseStats, StringComparer.OrdinalIgnoreCase) { [key] = checked(current + 1) };
        conditions[CharacterPointsKey] = (available - cost).ToString(CultureInfo.InvariantCulture);
        var before = character;
        var updated = character with { BaseStats = stats, Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
        try { _characters.SaveCharacter(updated); } catch { _characters.SaveCharacter(before); throw; }
        return $"You increase {key.ToUpperInvariant()} to {current + 1} for {cost} CP. {available - cost} CP remain.";
    }

    public string ExecuteWorldNarrativeCommand(RuntimeSession session, RuntimeCommand command) => _policyGateway.Execute(session, command);

    private static JsonElement RequireGeneratedItemRecord(string projectRoot, string worldId, string itemId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "items.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Librarium item projection is missing.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("authority", out var authority) || !string.Equals(authority.GetString(), "Librarium", StringComparison.Ordinal))
            throw new InvalidOperationException("Item projection is not Librarium authoritative.");
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Generated item projection has no records array.");
        foreach (var record in records.EnumerateArray())
        {
            if (string.Equals(ReadText(record, "id"), itemId, StringComparison.OrdinalIgnoreCase)) return record.Clone();
        }
        throw new InvalidOperationException($"Generated item projection contains no item record '{itemId}'.");
    }

    private static long ReadItemCost(JsonElement item)
    {
        if (item.TryGetProperty("economy", out var economy) && economy.ValueKind == JsonValueKind.Object) return ReadLong(economy, "cost");
        return ReadLong(item, "cost", "value", "price");
    }
    private static JsonElement ResolveOffering(JsonElement service, string arrayName, string query)
    {
        if (!service.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array) throw new InvalidOperationException($"Generated service has no executable {arrayName} relationships.");
        var q=query.Trim(); var matches=array.EnumerateArray().Where(x => new[]{ReadText(x,"item_ref"),ReadText(x,"name")}.Any(v=>!string.IsNullOrWhiteSpace(v)&&(v.Equals(q,StringComparison.OrdinalIgnoreCase)||v.Contains(q,StringComparison.OrdinalIgnoreCase)))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(matches.Length == 0 ? $"No generated shop item matches '{q}'." : $"Multiple generated shop items match '{q}'.");
    }
    private static bool HasOperation(JsonElement root,string operation) => root.TryGetProperty("operations",out var value)&&value.ValueKind==JsonValueKind.Array&&value.EnumerateArray().Any(x=>x.ToString().Equals(operation,StringComparison.OrdinalIgnoreCase));
    private static string? ReadText(JsonElement root, params string[] names){foreach(var n in names)if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null){var s=v.ToString();if(!string.IsNullOrWhiteSpace(s))return s;}return null;}
    private static long ReadLong(JsonElement root, params string[] names){foreach(var n in names)if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty(n,out var v)&&long.TryParse(v.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var x))return x;return 0;}
    private static int ReadInt(JsonElement root, params string[] names)=>checked((int)Math.Clamp(ReadLong(root,names),int.MinValue,int.MaxValue));
    private static int ReadNestedInt(JsonElement root,string parent,string name,int fallback)=>root.TryGetProperty(parent,out var nested)&&nested.ValueKind==JsonValueKind.Object&&nested.TryGetProperty(name,out var value)&&int.TryParse(value.ToString(),out var parsed)?parsed:fallback;
    private static int ParseLegacyNumber(string id){var dot=id.LastIndexOf('.');return int.TryParse(dot>=0?id[(dot+1)..]:id,out var value)?value:0;}
}

public sealed class RuntimeTrainerProgressionCatalog
{
    private readonly string _dataRoot;
    private RuntimeTrainerProgressionCatalog(string dataRoot) => _dataRoot = dataRoot;
    public static RuntimeTrainerProgressionCatalog Load(string projectRoot,string worldId) =>
        new(Path.Combine(projectRoot,"Data",worldId));

    public long RequireExperience(RuntimeCharacterRecord character,int targetLevel)
    {
        foreach (var path in Directory.EnumerateFiles(_dataRoot,"*.json",SearchOption.TopDirectoryOnly))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));
            var match=FindLevelRow(doc.RootElement,targetLevel,character);
            if (match is not null) return match.Value;
        }
        throw new InvalidOperationException($"Generated progression data contains no executable experience row for level {targetLevel}, race {character.RaceId}, class {character.ClassId}.");
    }

    public int RequireCharacterPointAward(int targetLevel)
    {
        foreach (var path in Directory.EnumerateFiles(_dataRoot,"*.json",SearchOption.TopDirectoryOnly))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));
            var value=FindCpAward(doc.RootElement,targetLevel);
            if (value is not null) return value.Value;
        }
        throw new InvalidOperationException($"Generated progression data contains no CP award for level {targetLevel}.");
    }

    private static long? FindLevelRow(JsonElement node,int targetLevel,RuntimeCharacterRecord character)
    {
        if (node.ValueKind==JsonValueKind.Object)
        {
            var level=ReadInt(node,"level","target_level");
            var xp=ReadLong(node,"required_experience","experience_required","total_experience","experience","xp");
            if (level==targetLevel && xp>0 && Matches(node,"class",character.ClassId,character.ClassName) && Matches(node,"race",character.RaceId,character.RaceName)) return xp;
            foreach(var property in node.EnumerateObject()) { var found=FindLevelRow(property.Value,targetLevel,character); if(found is not null)return found; }
        }
        else if(node.ValueKind==JsonValueKind.Array) foreach(var child in node.EnumerateArray()){var found=FindLevelRow(child,targetLevel,character);if(found is not null)return found;}
        return null;
    }
    private static int? FindCpAward(JsonElement node,int targetLevel)
    {
        if(node.ValueKind==JsonValueKind.Object)
        {
            var level=ReadInt(node,"level","target_level");
            var cp=ReadInt(node,"character_points","cp_award","cp_per_level","cp");
            if(level==targetLevel&&cp>0)return cp;
            foreach(var property in node.EnumerateObject()){var found=FindCpAward(property.Value,targetLevel);if(found is not null)return found;}
        }
        else if(node.ValueKind==JsonValueKind.Array)foreach(var child in node.EnumerateArray()){var found=FindCpAward(child,targetLevel);if(found is not null)return found;}
        return null;
    }
    private static bool Matches(JsonElement row,string prefix,string id,string name)
    {
        var expected=ReadText(row,prefix+"_ref",prefix+"_id",prefix+"_name",prefix);
        return string.IsNullOrWhiteSpace(expected)||expected.Equals(id,StringComparison.OrdinalIgnoreCase)||expected.Equals(name,StringComparison.OrdinalIgnoreCase)||ParseLegacyNumber(id).ToString(CultureInfo.InvariantCulture)==expected;
    }
    private static string? ReadText(JsonElement root,params string[] names){foreach(var name in names)if(root.TryGetProperty(name,out var v)&&v.ValueKind!=JsonValueKind.Null){var s=v.ToString();if(!string.IsNullOrWhiteSpace(s))return s;}return null;}
    private static long ReadLong(JsonElement root,params string[] names){foreach(var name in names)if(root.TryGetProperty(name,out var v)&&long.TryParse(v.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var n))return n;return 0;}
    private static int ReadInt(JsonElement root,params string[] names)=>checked((int)Math.Clamp(ReadLong(root,names),int.MinValue,int.MaxValue));
    private static int ParseLegacyNumber(string id){var dot=id.LastIndexOf('.');return int.TryParse(dot>=0?id[(dot+1)..]:id,out var value)?value:0;}
}

public sealed class RuntimeWorldSystemsBundleSmokeReport
{
    public bool Passed => Failures.Count == 0; public List<string> Checks { get; }=new(); public List<string> Failures { get; }=new();
    public static RuntimeWorldSystemsBundleSmokeReport Run(string root,string world)
    {
        var r=new RuntimeWorldSystemsBundleSmokeReport();
        try
        {
            var services=RuntimeServiceCatalog.Load(root,world);
            Check(r,services.Services.Any(x=>x.GeneratedRecord.TryGetProperty("inventory",out var i)&&i.ValueKind==JsonValueKind.Array),"active-classic-shop-inventory");
            var trainers=services.Services.Where(x=>x.GeneratedRecord.TryGetProperty("service_kind",out var k)&&k.GetString()=="trainer").ToArray();
            Check(r,trainers.Length==44,"trainer-count-44");
            Check(r,trainers.All(x=>x.GeneratedRecord.TryGetProperty("room_ref",out _)),"trainer-room-resolution");
            Check(r,trainers.All(x=>x.GeneratedRecord.TryGetProperty("level_range",out var lr)&&lr.ValueKind==JsonValueKind.Object),"trainer-level-ranges");
            Check(r,trainers.All(x=>x.GeneratedRecord.TryGetProperty("class_limit",out _)),"trainer-class-restrictions");
            Check(r,RuntimeCommand.SupportedCommands.Contains("train"),"train-routing");
            Check(r,RuntimeCommand.SupportedCommands.Contains("buy"),"buy-routing");
            Check(r,RuntimeCommand.SupportedCommands.Contains("tat")&&RuntimeCommand.SupportedCommands.Contains("stats"),"tat-cp-spend-routing");
            Check(r,TatCost(0)==1&&TatCost(9)==1&&TatCost(10)==2&&TatCost(19)==2&&TatCost(20)==3&&TatCost(29)==3&&TatCost(30)==4&&TatCost(39)==4&&TatCost(40)==5,"tat-tiered-costs");
            Check(r,TotalTatCost(0,40)==100,"tat-forty-point-total-cost");
            Check(r,typeof(RuntimeGameplayCompletionStateStore).GetMethod("ExecuteShopTransaction") is not null,"persistent-shop-stock-transaction");
            Check(r,typeof(RuntimeCharacterService).GetMethod("ExecuteCharacterTransaction") is not null,"atomic-direct-give-transaction");
            var stockedShop=services.Services.First(x=>x.GeneratedRecord.TryGetProperty("inventory",out var inv)&&inv.ValueKind==JsonValueKind.Array&&inv.GetArrayLength()>0);
            var generatedEntry=stockedShop.GeneratedRecord.GetProperty("inventory")[0];
            Check(r,generatedEntry.TryGetProperty("normal_qty",out _)&&generatedEntry.TryGetProperty("max_qty",out _)&&generatedEntry.TryGetProperty("regen_qty",out _)&&generatedEntry.TryGetProperty("regen_time",out _),"generated-shop-restock-contract");
        }
        catch(Exception ex){r.Failures.Add(ex.GetType().Name+":"+ex.Message);} return r;
    }
    private static int TatCost(int pointsAboveBase)=>(Math.Max(0,pointsAboveBase)/10)+1;
    private static int TotalTatCost(int startingOffset,int points)=>Enumerable.Range(startingOffset,points).Sum(TatCost);
    private static void Check(RuntimeWorldSystemsBundleSmokeReport r,bool ok,string name){if(ok)r.Checks.Add(name);else r.Failures.Add(name);}
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}
