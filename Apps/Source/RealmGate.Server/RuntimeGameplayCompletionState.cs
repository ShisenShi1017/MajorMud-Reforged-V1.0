using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeShopStockEntry(string ItemId, int Quantity, DateTimeOffset NextRestockUtc);
public sealed record RuntimeShopState(string ShopId, IReadOnlyDictionary<string, RuntimeShopStockEntry> Stock, DateTimeOffset UpdatedAtUtc, IReadOnlyList<string>? AppliedRestockRequestIds = null);
public sealed record RuntimeTrainerOfferingEntry(string OfferingId, string OfferingKind, bool Enabled, DateTimeOffset NextAvailableUtc, string Authority);
public sealed record RuntimeTrainerState(string TrainerId, IReadOnlyDictionary<string, RuntimeTrainerOfferingEntry> Offerings, DateTimeOffset UpdatedAtUtc);

public sealed class RuntimeGameplayCompletionStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _root;
    private readonly object _gate = new();

    public RuntimeGameplayCompletionStateStore(string projectRoot, string worldId)
    {
        _root = Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, worldId), "GameplayCompletion");
        Directory.CreateDirectory(Path.Combine(_root, "shops"));
        Directory.CreateDirectory(Path.Combine(_root, "trainers"));
    }

    public T ExecuteShopTransaction<T>(RuntimeServiceDefinition shop, Func<Dictionary<string, RuntimeShopStockEntry>, T> action)
    {
        lock (_gate)
        {
            var before = LoadOrCreate(shop);
            var working = new Dictionary<string, RuntimeShopStockEntry>(before.Stock, StringComparer.OrdinalIgnoreCase);
            ApplyDueRestocks(shop, working, DateTimeOffset.UtcNow);
            var result = action(working);
            Save(new RuntimeShopState(shop.Id, working, DateTimeOffset.UtcNow));
            return result;
        }
    }

    public T ExecuteExactShopStateTransaction<T>(RuntimeServiceDefinition shop, Func<RuntimeShopState, (RuntimeShopState State, T Result)> action)
    {
        lock (_gate)
        {
            var before = LoadOrCreate(shop);
            var outcome = action(before);
            if (!outcome.State.ShopId.Equals(shop.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("shop-state-id-mismatch");
            Save(outcome.State);
            return outcome.Result;
        }
    }

    public RuntimeShopState Read(RuntimeServiceDefinition shop)
    {
        lock (_gate)
        {
            var state = LoadOrCreate(shop);
            var stock = new Dictionary<string, RuntimeShopStockEntry>(state.Stock, StringComparer.OrdinalIgnoreCase);
            if (ApplyDueRestocks(shop, stock, DateTimeOffset.UtcNow))
            {
                state = new RuntimeShopState(shop.Id, stock, DateTimeOffset.UtcNow);
                Save(state);
            }
            return state;
        }
    }


    public T ExecuteTrainerTransaction<T>(RuntimeServiceDefinition trainer, Func<Dictionary<string, RuntimeTrainerOfferingEntry>, T> action)
    {
        lock (_gate)
        {
            var before = LoadOrCreateTrainer(trainer);
            var working = new Dictionary<string, RuntimeTrainerOfferingEntry>(before.Offerings, StringComparer.OrdinalIgnoreCase);
            var result = action(working);
            SaveTrainer(new RuntimeTrainerState(trainer.Id, working, DateTimeOffset.UtcNow));
            return result;
        }
    }

    public RuntimeTrainerState ReadTrainer(RuntimeServiceDefinition trainer)
    {
        lock (_gate) return LoadOrCreateTrainer(trainer);
    }

    private RuntimeTrainerState LoadOrCreateTrainer(RuntimeServiceDefinition trainer)
    {
        var path = TrainerPathFor(trainer.Id);
        if (File.Exists(path))
        {
            var loaded = JsonSerializer.Deserialize<RuntimeTrainerState>(File.ReadAllText(path), JsonOptions);
            if (loaded is not null && loaded.TrainerId.Equals(trainer.Id, StringComparison.OrdinalIgnoreCase)) return loaded;
        }
        var created = new RuntimeTrainerState(trainer.Id, new Dictionary<string, RuntimeTrainerOfferingEntry>(StringComparer.OrdinalIgnoreCase), DateTimeOffset.UtcNow);
        SaveTrainer(created);
        return created;
    }

    private void SaveTrainer(RuntimeTrainerState state)
    {
        var path = TrainerPathFor(state.TrainerId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, path, true);
    }

    private RuntimeShopState LoadOrCreate(RuntimeServiceDefinition shop)
    {
        var path = PathFor(shop.Id);
        if (File.Exists(path))
        {
            var loaded = JsonSerializer.Deserialize<RuntimeShopState>(File.ReadAllText(path), JsonOptions);
            if (loaded is not null) return loaded;
        }
        var stock = new Dictionary<string, RuntimeShopStockEntry>(StringComparer.OrdinalIgnoreCase);
        if (shop.GeneratedRecord.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in inventory.EnumerateArray())
            {
                var itemId = ReadText(entry, "item_ref");
                if (string.IsNullOrWhiteSpace(itemId)) continue;
                var quantity = Math.Max(0, ReadInt(entry, "normal_qty"));
                stock[itemId] = new RuntimeShopStockEntry(itemId, quantity, NextRestock(entry, DateTimeOffset.UtcNow));
            }
        }
        var created = new RuntimeShopState(shop.Id, stock, DateTimeOffset.UtcNow);
        Save(created);
        return created;
    }

    private static bool ApplyDueRestocks(RuntimeServiceDefinition shop, Dictionary<string, RuntimeShopStockEntry> stock, DateTimeOffset now)
    {
        if (!shop.GeneratedRecord.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array) return false;
        var changed = false;
        foreach (var generated in inventory.EnumerateArray())
        {
            var itemId = ReadText(generated, "item_ref");
            if (string.IsNullOrWhiteSpace(itemId) || !stock.TryGetValue(itemId, out var current)) continue;
            var max = Math.Max(ReadInt(generated, "max_qty"), ReadInt(generated, "normal_qty"));
            var regen = Math.Max(0, ReadInt(generated, "regen_qty"));
            var percent = Math.Clamp(ReadInt(generated, "regen_percent"), 0, 100);
            var next = current.NextRestockUtc;
            while (regen > 0 && percent > 0 && current.Quantity < max && next <= now)
            {
                var quantity = StableRestockRoll(shop.Id,itemId,next)<=percent
                    ? Math.Min(max,checked(current.Quantity+regen))
                    : current.Quantity;
                current = current with { Quantity = quantity, NextRestockUtc = NextRestock(generated, next) };
                next = current.NextRestockUtc;
                changed = true;
            }
            stock[itemId] = current;
        }
        return changed;
    }

    private static DateTimeOffset NextRestock(JsonElement generated, DateTimeOffset from)
    {
        var rounds = ReadInt(generated, "regen_time");
        if(rounds<=0||ReadInt(generated,"regen_qty")<=0||ReadInt(generated,"regen_percent")<=0)
            return DateTimeOffset.MaxValue;
        return from.AddSeconds(checked(rounds * 6L));
    }

    internal static int StableRestockRoll(string shopId,string itemId,DateTimeOffset cycle)
    {
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes($"{shopId}|{itemId}|{cycle.UtcDateTime.Ticks}"));
        return (BitConverter.ToUInt32(bytes,0)%100)+1 is var roll ? checked((int)roll) : 1;
    }

    private void Save(RuntimeShopState state)
    {
        var path = PathFor(state.ShopId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, path, true);
    }

    private string PathFor(string shopId) => Path.Combine(_root, "shops", RuntimeIdentity.Normalize(shopId, "shop") + ".json");
    private string TrainerPathFor(string trainerId) => Path.Combine(_root, "trainers", RuntimeIdentity.Normalize(trainerId, "trainer") + ".json");
    private static string? ReadText(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.ToString() : null;
    private static int ReadInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
