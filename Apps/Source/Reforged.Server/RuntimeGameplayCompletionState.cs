using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeShopStockEntry(string ItemId, int Quantity, DateTimeOffset NextRestockUtc);
public sealed record RuntimeShopState(string ShopId, IReadOnlyDictionary<string, RuntimeShopStockEntry> Stock, DateTimeOffset UpdatedAtUtc);

public sealed class RuntimeGameplayCompletionStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _root;
    private readonly object _gate = new();

    public RuntimeGameplayCompletionStateStore(string projectRoot, string worldId)
    {
        _root = Path.Combine(projectRoot, "Saves", "Runtime0138", "GameplayCompletion", RuntimeIdentity.Normalize(worldId, "world"));
        Directory.CreateDirectory(Path.Combine(_root, "shops"));
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
            var next = current.NextRestockUtc;
            while (regen > 0 && current.Quantity < max && next <= now)
            {
                current = current with { Quantity = Math.Min(max, checked(current.Quantity + regen)), NextRestockUtc = NextRestock(generated, next) };
                next = current.NextRestockUtc;
                changed = true;
            }
            stock[itemId] = current;
        }
        return changed;
    }

    private static DateTimeOffset NextRestock(JsonElement generated, DateTimeOffset from)
    {
        var rounds = Math.Max(1, ReadInt(generated, "regen_time"));
        return from.AddSeconds(checked(rounds * 6L));
    }

    private void Save(RuntimeShopState state)
    {
        var path = PathFor(state.ShopId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, path, true);
    }

    private string PathFor(string shopId) => Path.Combine(_root, "shops", RuntimeIdentity.Normalize(shopId, "shop") + ".json");
    private static string? ReadText(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.ToString() : null;
    private static int ReadInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
