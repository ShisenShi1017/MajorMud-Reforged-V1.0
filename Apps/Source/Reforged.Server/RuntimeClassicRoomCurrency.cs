using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicRoomCurrencyBalance(
    long RunicCoins,
    long PlatinumPieces,
    long GoldCrowns,
    long SilverNobles,
    long CopperFarthings)
{
    public static readonly RuntimeClassicRoomCurrencyBalance Empty = new(0, 0, 0, 0, 0);
    public bool IsEmpty => RunicCoins <= 0 && PlatinumPieces <= 0 && GoldCrowns <= 0 && SilverNobles <= 0 && CopperFarthings <= 0;
    public long CopperValue => checked(RunicCoins * 1_000_000L + PlatinumPieces * 10_000L + GoldCrowns * 100L + SilverNobles * 10L + CopperFarthings);
}

public sealed record RuntimeClassicRoomCurrencyTakeResult(bool Matched, long CopperValue, string DisplayText, RuntimeClassicRoomCurrencyBalance Balance);

public static class RuntimeClassicRoomCurrencyStore
{
    private sealed class StoreDocument
    {
        public Dictionary<string, RuntimeClassicRoomCurrencyBalance> Rooms { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MigratedLegacyInstanceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static RuntimeClassicRoomCurrencyBalance Get(string projectRoot, string worldId, string roomId)
    {
        lock (Gate)
        {
            var document = Load(projectRoot);
            return document.Rooms.TryGetValue(Key(worldId, roomId), out var balance) ? balance : RuntimeClassicRoomCurrencyBalance.Empty;
        }
    }

    public static string AddExact(string projectRoot, string worldId, string roomId, long runic, long platinum, long gold, long silver, long copper)
    {
        if (runic < 0 || platinum < 0 || gold < 0 || silver < 0 || copper < 0)
            throw new InvalidOperationException("Classic room currency amounts cannot be negative.");
        var addition = new RuntimeClassicRoomCurrencyBalance(runic, platinum, gold, silver, copper);
        if (addition.IsEmpty) return string.Empty;
        lock (Gate)
        {
            var document = Load(projectRoot);
            var key = Key(worldId, roomId);
            var current = document.Rooms.TryGetValue(key, out var existing) ? existing : RuntimeClassicRoomCurrencyBalance.Empty;
            document.Rooms[key] = Add(current, addition);
            Save(projectRoot, document);
        }
        return Format(addition);
    }

    public static string AddCopper(string projectRoot, string worldId, string roomId, long totalCopper)
    {
        if (totalCopper <= 0) return string.Empty;
        var addition = FromCopper(totalCopper);
        lock (Gate)
        {
            var document = Load(projectRoot);
            var key = Key(worldId, roomId);
            var current = document.Rooms.TryGetValue(key, out var existing) ? existing : RuntimeClassicRoomCurrencyBalance.Empty;
            document.Rooms[key] = Add(current, addition);
            Save(projectRoot, document);
        }
        return Format(addition);
    }

    public static RuntimeCharacterRecord MigrateLegacyCurrency(string projectRoot, RuntimeCharacterRecord character)
    {
        var state = character.ItemState;
        if (state is null || state.Items.Count == 0) return character;

        var legacy = state.Items.Where(IsLegacyCurrency).ToArray();
        if (legacy.Length == 0) return character;

        lock (Gate)
        {
            var document = Load(projectRoot);
            foreach (var item in legacy)
            {
                if (!document.MigratedLegacyInstanceIds.Add(item.InstanceId)) continue;
                if (!item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)) continue;
                var kind = LegacyKind(item);
                if (kind is null) continue;
                var key = Key(character.WorldId, item.LocationId);
                var current = document.Rooms.TryGetValue(key, out var existing) ? existing : RuntimeClassicRoomCurrencyBalance.Empty;
                document.Rooms[key] = AddDenomination(current, kind, Math.Max(0, item.StackQuantity));
            }
            Save(projectRoot, document);
        }

        var cleaned = state with { Items = state.Items.Where(item => !IsLegacyCurrency(item)).ToArray() };
        return character with { ItemState = cleaned, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    public static RuntimeClassicRoomCurrencyTakeResult Take(string projectRoot, string worldId, string roomId, string query)
    {
        query = query.Trim().ToLowerInvariant();
        lock (Gate)
        {
            var document = Load(projectRoot);
            var key = Key(worldId, roomId);
            var current = document.Rooms.TryGetValue(key, out var existing) ? existing : RuntimeClassicRoomCurrencyBalance.Empty;
            if (current.IsEmpty) return new(false, 0, string.Empty, RuntimeClassicRoomCurrencyBalance.Empty);

            var takeAll = query.Length == 0 || query is "all" or "coin" or "coins" or "money" or "currency";
            var takeRunic = takeAll || Matches(query, "runic", "runic coin", "runic coins", "rune", "runes");
            var takePlatinum = takeAll || Matches(query, "platinum", "platinum piece", "platinum pieces", "plat", "piece", "pieces");
            var takeGold = takeAll || Matches(query, "gold", "gold crown", "gold crowns", "crown", "crowns");
            var takeSilver = takeAll || Matches(query, "silver", "silver noble", "silver nobles", "noble", "nobles");
            var takeCopper = takeAll || Matches(query, "copper", "copper farthing", "copper farthings", "farthing", "farthings");
            if (!takeRunic && !takePlatinum && !takeGold && !takeSilver && !takeCopper) return new(false, 0, string.Empty, RuntimeClassicRoomCurrencyBalance.Empty);

            var taken = new RuntimeClassicRoomCurrencyBalance(
                takeRunic ? current.RunicCoins : 0,
                takePlatinum ? current.PlatinumPieces : 0,
                takeGold ? current.GoldCrowns : 0,
                takeSilver ? current.SilverNobles : 0,
                takeCopper ? current.CopperFarthings : 0);
            if (taken.IsEmpty) return new(false, 0, string.Empty, RuntimeClassicRoomCurrencyBalance.Empty);

            var remaining = new RuntimeClassicRoomCurrencyBalance(
                current.RunicCoins - taken.RunicCoins,
                current.PlatinumPieces - taken.PlatinumPieces,
                current.GoldCrowns - taken.GoldCrowns,
                current.SilverNobles - taken.SilverNobles,
                current.CopperFarthings - taken.CopperFarthings);
            if (remaining.IsEmpty) document.Rooms.Remove(key); else document.Rooms[key] = remaining;
            Save(projectRoot, document);
            return new(true, taken.CopperValue, Format(taken), taken);
        }
    }

    public static bool IsLegacyCurrency(RuntimeItemInstance item) =>
        item.CanonicalItemId.StartsWith("__classic_", StringComparison.OrdinalIgnoreCase)
        || item.Metadata?.ContainsKey("currency_kind") == true;

    public static string Format(RuntimeClassicRoomCurrencyBalance balance)
    {
        var parts = new List<string>();
        AddPart(parts, balance.RunicCoins, "runic coin", "runic coins");
        AddPart(parts, balance.PlatinumPieces, "platinum piece", "platinum pieces");
        AddPart(parts, balance.GoldCrowns, "gold crown", "gold crowns");
        AddPart(parts, balance.SilverNobles, "silver noble", "silver nobles");
        AddPart(parts, balance.CopperFarthings, "copper farthing", "copper farthings");
        return string.Join(", ", parts);
    }

    private static RuntimeClassicRoomCurrencyBalance FromCopper(long value)
    {
        var runic = value / 1_000_000L;
        value %= 1_000_000L;
        var platinum = value / 10_000L;
        value %= 10_000L;
        var gold = value / 100L;
        value %= 100L;
        var silver = value / 10L;
        var copper = value % 10L;
        return new(runic, platinum, gold, silver, copper);
    }

    private static RuntimeClassicRoomCurrencyBalance Add(RuntimeClassicRoomCurrencyBalance left, RuntimeClassicRoomCurrencyBalance right) =>
        new(checked(left.RunicCoins + right.RunicCoins), checked(left.PlatinumPieces + right.PlatinumPieces), checked(left.GoldCrowns + right.GoldCrowns), checked(left.SilverNobles + right.SilverNobles), checked(left.CopperFarthings + right.CopperFarthings));

    private static RuntimeClassicRoomCurrencyBalance AddDenomination(RuntimeClassicRoomCurrencyBalance value, string kind, long quantity) => kind switch
    {
        "runic_coins" => value with { RunicCoins = checked(value.RunicCoins + quantity) },
        "platinum_pieces" => value with { PlatinumPieces = checked(value.PlatinumPieces + quantity) },
        "gold_crowns" => value with { GoldCrowns = checked(value.GoldCrowns + quantity) },
        "silver_nobles" => value with { SilverNobles = checked(value.SilverNobles + quantity) },
        "copper_farthings" => value with { CopperFarthings = checked(value.CopperFarthings + quantity) },
        _ => value
    };

    private static string? LegacyKind(RuntimeItemInstance item)
    {
        if (item.Metadata?.TryGetValue("currency_kind", out var kind) == true) return kind.ToLowerInvariant();
        var id = item.CanonicalItemId.ToLowerInvariant();
        if (id.Contains("runic")) return "runic_coins";
        if (id.Contains("platinum")) return "platinum_pieces";
        if (id.Contains("gold")) return "gold_crowns";
        if (id.Contains("silver")) return "silver_nobles";
        if (id.Contains("copper")) return "copper_farthings";
        return null;
    }

    private static bool Matches(string query, params string[] names) => names.Any(name => name.StartsWith(query, StringComparison.OrdinalIgnoreCase));
    private static void AddPart(List<string> parts, long quantity, string singular, string plural) { if (quantity > 0) parts.Add($"{quantity} {(quantity == 1 ? singular : plural)}"); }
    private static string Key(string worldId, string roomId) => worldId + "\u001f" + roomId;
    private static string StorePath(string projectRoot) => Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, "classic"), "classic-room-currency.json");

    private static StoreDocument Load(string projectRoot)
    {
        var path = StorePath(projectRoot);
        if (!File.Exists(path)) return new StoreDocument();
        try { return JsonSerializer.Deserialize<StoreDocument>(File.ReadAllText(path), JsonOptions) ?? new StoreDocument(); }
        catch { return new StoreDocument(); }
    }

    private static void Save(string projectRoot, StoreDocument document)
    {
        var path = StorePath(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temp, path, true);
    }
}
