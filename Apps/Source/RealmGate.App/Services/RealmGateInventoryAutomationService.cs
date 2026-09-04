using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

/// <summary>
/// MudPlay-inspired inventory/equipment planner rebuilt around Reforged's authoritative
/// session snapshots and effective game-data projection. The service never writes world data.
/// It only persists per-character client preferences and produces ordinary MajorMUD commands
/// for PlayViewModel to send through the existing command-isolation layer.
/// </summary>
public sealed partial class RealmGateInventoryAutomationService
{
    private static readonly HashSet<string> MultiOccupancySlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "FINGER", "WRIST"
    };

    private readonly string _profileRoot;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly RealmGateNavigationService _navigation;
    private readonly RealmGateClientGameDataOverlayService _gameData = new();

    public RealmGateInventoryAutomationService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate");
        _profileRoot = Path.Combine(root, "Client", "InventoryAutomation");
        _navigation = new RealmGateNavigationService(root);
    }

    public RealmGateInventoryAutomationProfile LoadProfile(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var key = BuildProfileKey(session);
        var path = ProfilePath(key);
        if (!File.Exists(path)) return NewProfile(session, key);

        try
        {
            var profile = JsonSerializer.Deserialize<RealmGateInventoryAutomationProfile>(File.ReadAllText(path), _json)
                          ?? NewProfile(session, key);
            NormalizeProfile(profile, session, key);
            return profile;
        }
        catch (JsonException)
        {
            BackupCorrupt(path);
            return NewProfile(session, key);
        }
        catch (IOException)
        {
            return NewProfile(session, key);
        }
        catch (UnauthorizedAccessException)
        {
            return NewProfile(session, key);
        }
    }

    public void SaveProfile(ClientSessionConnection session, RealmGateInventoryAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var key = BuildProfileKey(session);
        NormalizeProfile(profile, session, key);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_profileRoot);
        var path = ProfilePath(key);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _json), new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    public RealmGateEquipmentSet CaptureEquipmentSet(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        string name)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var set = new RealmGateEquipmentSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Captured Set" : name.Trim(),
            Enabled = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var order = 0;
        foreach (var display in session.EquippedItems)
        {
            var item = catalog.FindByDisplayName(display);
            var observedName = item?.Name ?? NormalizeDisplayName(display);
            if (observedName.Length == 0) continue;
            set.Items.Add(new RealmGateEquipmentSetEntry
            {
                ItemId = item?.Id ?? string.Empty,
                ItemName = observedName,
                Slot = item?.Slot ?? string.Empty,
                Order = order++
            });
        }

        profile.EquipmentSets.Add(set);
        return set;
    }

    public IReadOnlyList<RealmGateInventoryObservedItem> ObserveInventory(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var result = new List<RealmGateInventoryObservedItem>();
        foreach (var display in session.InventoryItems)
            result.Add(Observe(display, equipped: false, catalog));
        foreach (var display in session.EquippedItems)
            result.Add(Observe(display, equipped: true, catalog));
        return result;
    }

    public RealmGateInventoryAutomationPlan PlanEquipmentSet(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        RealmGateEquipmentSet set)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(set);

        var preferences = profile.Preferences;
        var now = DateTimeOffset.Now;
        var warnings = new List<string>();
        var refreshInventory = !IsFresh(session.InventoryUpdatedAt, now, preferences.StateMaximumAgeSeconds);
        var refreshEquipment = !IsFresh(session.EquipmentUpdatedAt, now, preferences.StateMaximumAgeSeconds);
        if (refreshInventory || refreshEquipment)
        {
            if (refreshInventory) warnings.Add("Inventory snapshot is stale or missing.");
            if (refreshEquipment) warnings.Add("Equipment snapshot is stale or missing.");
            return new RealmGateInventoryAutomationPlan
            {
                Action = $"Refresh state before applying '{set.Name}'",
                RequiresInventoryRefresh = refreshInventory,
                RequiresEquipmentRefresh = refreshEquipment,
                Warnings = warnings,
                Fingerprint = $"refresh|{session.SessionId}|{refreshInventory}|{refreshEquipment}"
            };
        }

        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var inventory = session.InventoryItems.Select(text => Observe(text, false, catalog)).ToArray();
        var equipped = session.EquippedItems.Select(text => Observe(text, true, catalog)).ToArray();
        var desired = set.Items
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ItemName))
            .OrderBy(entry => entry.Order)
            .ToArray();
        var desiredNames = desired.Select(entry => NormalizeName(entry.ItemName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var controlledSlots = desired.Select(entry => entry.Slot).Where(slot => !string.IsNullOrWhiteSpace(slot))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var commands = new List<string>();
        var missing = new List<string>();

        // Only remove a currently worn item when the target set clearly controls a
        // single-occupancy slot. Paired ring/wrist slots are intentionally left alone
        // rather than guessing which physical index the server assigned.
        foreach (var current in equipped)
        {
            if (string.IsNullOrWhiteSpace(current.Slot) || !controlledSlots.Contains(current.Slot)) continue;
            if (MultiOccupancySlots.Contains(current.Slot)) continue;
            if (desiredNames.Contains(NormalizeName(current.Name))) continue;
            var remove = ApplyTemplate(preferences.RemoveCommandTemplate, current.Name, 1, 0);
            if (remove.Length > 0) commands.Add(remove);
        }

        foreach (var entry in desired)
        {
            if (equipped.Any(item => ItemMatches(item, entry))) continue;
            var carried = inventory.FirstOrDefault(item => ItemMatches(item, entry));
            if (carried is null)
            {
                missing.Add(entry.ItemName);
                continue;
            }
            var equip = ApplyTemplate(preferences.EquipCommandTemplate, entry.ItemName, 1, 0);
            if (equip.Length > 0) commands.Add(equip);
        }

        commands = commands.Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(preferences.MaximumCommandsPerPass)
            .ToList();
        return new RealmGateInventoryAutomationPlan
        {
            Action = $"Apply equipment set '{set.Name}'",
            Commands = commands,
            MissingItems = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Warnings = warnings,
            Fingerprint = Fingerprint("equip", set.Id, commands, missing)
        };
    }

    public RealmGateInventoryAutomationPlan PlanInventoryRules(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var preferences = profile.Preferences;
        var now = DateTimeOffset.Now;
        var warnings = new List<string>();
        if (!IsFresh(session.InventoryUpdatedAt, now, preferences.StateMaximumAgeSeconds))
        {
            return new RealmGateInventoryAutomationPlan
            {
                Action = "Refresh inventory before evaluating inventory rules",
                RequiresInventoryRefresh = true,
                Warnings = ["Inventory snapshot is stale or missing."],
                Fingerprint = $"refresh-inventory|{session.SessionId}"
            };
        }

        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var inventory = session.InventoryItems.Select(text => Observe(text, false, catalog)).ToArray();
        var atShop = IsAtDecodedShop(session);
        var atBank = IsAtDecodedBank(session);
        var atStash = IsAtSavedStash(session);
        var commands = new List<string>();

        foreach (var item in inventory)
        {
            var rule = profile.Rules
                .Where(candidate => candidate.Enabled && candidate.Disposition != RealmGateInventoryDisposition.Buy)
                .OrderBy(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(candidate => Matches(candidate, item));
            if (rule is null || rule.Disposition == RealmGateInventoryDisposition.Keep) continue;

            switch (rule.Disposition)
            {
                case RealmGateInventoryDisposition.Sell:
                    if (!preferences.AllowAutomaticSell) continue;
                    if (!atShop) { warnings.Add($"Sell rule for {item.Name} is waiting for a decoded shop."); continue; }
                    commands.Add(ApplyTemplate(preferences.SellCommandTemplate, item.Name, item.Quantity, 0));
                    break;
                case RealmGateInventoryDisposition.Stash:
                    if (!preferences.AllowAutomaticStash) continue;
                    if (!atStash) { warnings.Add($"Stash rule for {item.Name} is waiting for a saved stash room."); continue; }
                    commands.Add(ApplyTemplate(preferences.StashCommandTemplate, item.Name, item.Quantity, 0));
                    break;
                case RealmGateInventoryDisposition.Discard:
                    if (!preferences.AllowAutomaticDiscard) continue;
                    commands.Add(ApplyTemplate(preferences.DiscardCommandTemplate, item.Name, item.Quantity, 0));
                    break;
            }
        }

        if (preferences.AllowAutomaticBuy && atShop)
        {
            foreach (var rule in profile.Rules
                         .Where(candidate => candidate.Enabled && candidate.Disposition == RealmGateInventoryDisposition.Buy)
                         .OrderBy(candidate => candidate.Priority)
                         .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase))
            {
                var target = ResolveRuleTarget(rule, catalog);
                if (target is null)
                {
                    warnings.Add($"Buy rule '{rule.MatchValue}' does not resolve to one effective item record.");
                    continue;
                }
                var owned = inventory.Where(item => SameItem(item, target)).Sum(item => Math.Max(1, item.Quantity)) +
                            session.EquippedItems.Select(text => Observe(text, true, catalog)).Where(item => SameItem(item, target)).Sum(item => Math.Max(1, item.Quantity));
                var need = Math.Max(0, rule.TargetQuantity - owned);
                if (need == 0) continue;
                if (!CurrentShopStocksItem(session, target.Id))
                {
                    warnings.Add($"Buy rule for {target.Name} is waiting for a decoded current-shop stock reference; availability was not guessed.");
                    continue;
                }
                commands.Add(ApplyTemplate(preferences.BuyCommandTemplate, target.Name, need, 0));
            }
        }

        if (preferences.AutoDepositExcessCurrency && atBank && session.CarriedCurrency > preferences.KeepCurrency)
        {
            var amount = session.CarriedCurrency - Math.Max(0, preferences.KeepCurrency);
            if (preferences.KeepCurrency == 0 && !string.IsNullOrWhiteSpace(preferences.DepositAllCommand))
                commands.Add(preferences.DepositAllCommand.Trim());
            else if (preferences.DepositExcessCommandTemplate.Contains("{amount}", StringComparison.OrdinalIgnoreCase))
                commands.Add(ApplyTemplate(preferences.DepositExcessCommandTemplate, string.Empty, 1, amount));
            else
                warnings.Add("Excess-currency deposit is waiting for a command template containing {amount}; no denomination was guessed.");
        }

        commands = commands.Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(preferences.MaximumCommandsPerPass)
            .ToList();
        return new RealmGateInventoryAutomationPlan
        {
            Action = "Process inventory/economy rules",
            Commands = commands,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Fingerprint = Fingerprint("rules", BuildProfileKey(session), commands, warnings)
        };
    }

    public RealmGateInventoryAutomationPlan PlanRecovery(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        RealmGateEquipmentSet? set)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var warnings = new List<string>();
        var commands = new List<string>();
        if (set is null)
        {
            return new RealmGateInventoryAutomationPlan
            {
                Action = "Recovery",
                Warnings = ["Select or configure an equipment set before starting recovery."],
                Fingerprint = "recovery|no-set"
            };
        }

        var corpseNeedle = profile.Preferences.RecoveryCorpseNameContains.Trim();
        var corpseVisible = session.CurrentRoomItems.Any(item => item.Contains("corpse", StringComparison.OrdinalIgnoreCase) &&
            (corpseNeedle.Length == 0 || item.Contains(corpseNeedle, StringComparison.OrdinalIgnoreCase)));
        if (corpseNeedle.Length == 0)
            warnings.Add("Automatic corpse identity is not guessed. Configure a corpse-name fragment before using recovery looting automatically.");
        if (corpseVisible && corpseNeedle.Length > 0 && !string.IsNullOrWhiteSpace(profile.Preferences.RecoveryCorpseCommand))
            commands.Add(profile.Preferences.RecoveryCorpseCommand.Trim());
        else if (!corpseVisible)
            warnings.Add("No matching corpse is visible in the latest room snapshot.");

        var equipPlan = PlanEquipmentSet(session, profile, set);
        if (!equipPlan.RequiresInventoryRefresh && !equipPlan.RequiresEquipmentRefresh)
            commands.AddRange(equipPlan.Commands);
        warnings.AddRange(equipPlan.Warnings);

        return new RealmGateInventoryAutomationPlan
        {
            Action = $"Recover and restore '{set.Name}'",
            Commands = commands.Distinct(StringComparer.OrdinalIgnoreCase).Take(profile.Preferences.MaximumCommandsPerPass).ToArray(),
            MissingItems = equipPlan.MissingItems,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RequiresInventoryRefresh = equipPlan.RequiresInventoryRefresh,
            RequiresEquipmentRefresh = equipPlan.RequiresEquipmentRefresh,
            Fingerprint = Fingerprint("recovery", set.Id, commands, warnings)
        };
    }

    public RealmGateEquipmentSet? ResolveAutomaticSet(RealmGateInventoryAutomationProfile profile)
    {
        var id = profile.Preferences.AutoEquipmentSetId.Trim();
        if (id.Length > 0)
        {
            var explicitSet = profile.EquipmentSets.FirstOrDefault(set => set.Enabled && set.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (explicitSet is not null) return explicitSet;
        }
        return profile.EquipmentSets.FirstOrDefault(set => set.Enabled && set.AutoApply)
               ?? profile.EquipmentSets.FirstOrDefault(set => set.Enabled && set.Name.Equals("Default", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAtDecodedShop(ClientSessionConnection session)
    {
        if (session.CurrentRoomServices.Any(service => service.Kind.Contains("shop", StringComparison.OrdinalIgnoreCase)))
            return true;
        var room = ResolveCurrentRoomRecord(session);
        if (room is null) return false;
        var shopId = ReadInt(room.EffectiveRecord, "environment", "shop_id");
        if (shopId <= 0) return false;
        return _gameData.LoadFamily(session, "Shops").Any(shop => ReadInt(shop.EffectiveRecord, "legacy_id") == shopId);
    }

    public bool IsAtDecodedBank(ClientSessionConnection session)
    {
        if (session.CurrentRoomServices.Any(service =>
                service.Kind.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
                service.Kind.Contains("vault", StringComparison.OrdinalIgnoreCase)))
            return true;
        var room = ResolveCurrentRoomRecord(session);
        if (room is null) return false;
        var shopId = ReadInt(room.EffectiveRecord, "environment", "shop_id");
        if (shopId > 0 && _gameData.LoadFamily(session, "Banks").Any(bank => ReadInt(bank.EffectiveRecord, "legacy_id") == shopId))
            return true;

        var roomNumber = ReadInt(room.EffectiveRecord, "room_number");
        if (roomNumber <= 0) return false;
        var matches = _gameData.LoadFamily(session, "Banks")
            .Where(bank => ReadInt(bank.EffectiveRecord, "room_number") == roomNumber)
            .Take(2)
            .Count();
        return matches == 1;
    }

    public bool CurrentShopStocksItem(ClientSessionConnection session, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        var room = ResolveCurrentRoomRecord(session);
        if (room is null) return false;
        var shopId = ReadInt(room.EffectiveRecord, "environment", "shop_id");
        var roomId = room.Id;
        var shops = _gameData.LoadFamily(session, "Shops")
            .Where(shop =>
                (shopId > 0 && ReadInt(shop.EffectiveRecord, "legacy_id") == shopId) ||
                ReadText(shop.EffectiveRecord, "room_ref").Equals(roomId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (shops.Length != 1) return false;
        if (shops[0].EffectiveRecord["inventory"] is not System.Text.Json.Nodes.JsonArray inventory) return false;
        foreach (var node in inventory.OfType<System.Text.Json.Nodes.JsonObject>())
        {
            if (ReadText(node, "item_ref").Equals(itemId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool IsAtSavedStash(ClientSessionConnection session)
    {
        var room = session.CurrentRoomKey.Trim();
        if (room.Length == 0) return false;
        var navigationProfile = _navigation.LoadProfile(session);
        return navigationProfile.SavedDestinations.Any(destination =>
            destination.Kind == RealmGateSavedDestinationKind.Stash &&
            destination.RoomKey.Equals(room, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildProfileKey(ClientSessionConnection session)
    {
        var raw = string.Join("|",
            session.ServerProfileName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.WorldName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.HostName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.Port.ToString(CultureInfo.InvariantCulture),
            session.CharacterName?.Trim().ToLowerInvariant() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..24];
    }

    private static RealmGateInventoryObservedItem Observe(string display, bool equipped, RealmGateWorkshopDataCatalog catalog)
    {
        var item = catalog.FindByDisplayName(display);
        return new RealmGateInventoryObservedItem(
            display,
            item?.Name ?? NormalizeDisplayName(display),
            item?.Id ?? string.Empty,
            item?.Slot ?? string.Empty,
            item?.ItemType ?? string.Empty,
            ParseQuantity(display),
            equipped);
    }

    private static bool ItemMatches(RealmGateInventoryObservedItem item, RealmGateEquipmentSetEntry desired)
    {
        if (!string.IsNullOrWhiteSpace(desired.ItemId) && !string.IsNullOrWhiteSpace(item.ItemId))
            return item.ItemId.Equals(desired.ItemId, StringComparison.OrdinalIgnoreCase);
        return NormalizeName(item.Name).Equals(NormalizeName(desired.ItemName), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameItem(RealmGateInventoryObservedItem item, RealmGateWorkshopItem target)
    {
        if (!string.IsNullOrWhiteSpace(item.ItemId))
            return item.ItemId.Equals(target.Id, StringComparison.OrdinalIgnoreCase);
        return NormalizeName(item.Name).Equals(NormalizeName(target.Name), StringComparison.OrdinalIgnoreCase);
    }

    private static RealmGateWorkshopItem? ResolveRuleTarget(RealmGateInventoryRule rule, RealmGateWorkshopDataCatalog catalog)
    {
        var value = rule.MatchValue.Trim();
        if (value.Length == 0) return null;
        return rule.MatchMode switch
        {
            RealmGateInventoryRuleMatchMode.ItemId => catalog.Items.FirstOrDefault(item => item.Id.Equals(value, StringComparison.OrdinalIgnoreCase)),
            RealmGateInventoryRuleMatchMode.ExactName => catalog.Items.FirstOrDefault(item => item.Name.Equals(value, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private static bool Matches(RealmGateInventoryRule rule, RealmGateInventoryObservedItem item)
    {
        var value = rule.MatchValue.Trim();
        if (value.Length == 0) return false;
        return rule.MatchMode switch
        {
            RealmGateInventoryRuleMatchMode.ExactName => item.Name.Equals(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.NameContains => item.Name.Contains(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.ItemId => item.ItemId.Equals(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.Slot => item.Slot.Equals(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.ItemType => item.ItemType.Contains(value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static string ApplyTemplate(string template, string item, int quantity, long amount)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        return template.Trim()
            .Replace("{item}", item.Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{quantity}", Math.Max(1, quantity).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{amount}", Math.Max(0, amount).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string Fingerprint(string kind, string key, IEnumerable<string> commands, IEnumerable<string> details)
    {
        var raw = kind + "|" + key + "|" + string.Join("|", commands) + "|" + string.Join("|", details);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..24];
    }

    private static bool IsFresh(DateTimeOffset? timestamp, DateTimeOffset now, int maximumAgeSeconds) =>
        timestamp is { } value && now - value <= TimeSpan.FromSeconds(Math.Clamp(maximumAgeSeconds, 5, 600));

    private static int ParseQuantity(string value)
    {
        var match = QuantityPrefixRegex().Match(value ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
            ? Math.Max(1, quantity)
            : 1;
    }

    private static string NormalizeDisplayName(string value)
    {
        var text = (value ?? string.Empty).Trim();
        text = QuantityPrefixRegex().Replace(text, string.Empty);
        text = LeadingSlotRegex().Replace(text, string.Empty);
        text = TrailingAnnotationRegex().Replace(text, string.Empty);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string NormalizeName(string value) => Regex.Replace(NormalizeDisplayName(value).ToLowerInvariant(), @"^(a|an|the)\s+", string.Empty).Trim();

    private RealmGateGameDataRecord? ResolveCurrentRoomRecord(ClientSessionConnection session)
    {
        if (!TryParseRoomKey(session.CurrentRoomKey, out var map, out var room)) return null;
        return _gameData.LoadFamily(session, "Rooms")
            .FirstOrDefault(record =>
                ReadInt(record.EffectiveRecord, "map_number") == map &&
                ReadInt(record.EffectiveRecord, "room_number") == room);
    }

    private static bool TryParseRoomKey(string? value, out int map, out int room)
    {
        map = room = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var match = RoomKeyRegex().Match(value.Trim());
        if (!match.Success) return false;
        var mapText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
        var roomText = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
        return int.TryParse(mapText, NumberStyles.Integer, CultureInfo.InvariantCulture, out map) &&
               int.TryParse(roomText, NumberStyles.Integer, CultureInfo.InvariantCulture, out room) &&
               map > 0 && room > 0;
    }

    private static int ReadInt(System.Text.Json.Nodes.JsonObject root, params string[] path)
    {
        System.Text.Json.Nodes.JsonNode? node = root;
        foreach (var part in path)
        {
            if (node is not System.Text.Json.Nodes.JsonObject obj || !obj.TryGetPropertyValue(part, out node) || node is null)
                return 0;
        }
        if (node is System.Text.Json.Nodes.JsonValue value)
        {
            if (value.TryGetValue<int>(out var integer)) return integer;
            if (value.TryGetValue<long>(out var longInteger) && longInteger is >= int.MinValue and <= int.MaxValue) return (int)longInteger;
            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) return integer;
        }
        return 0;
    }

    private static string ReadText(System.Text.Json.Nodes.JsonObject root, params string[] path)
    {
        System.Text.Json.Nodes.JsonNode? node = root;
        foreach (var part in path)
        {
            if (node is not System.Text.Json.Nodes.JsonObject obj || !obj.TryGetPropertyValue(part, out node) || node is null)
                return string.Empty;
        }
        return node is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text)
            ? text?.Trim() ?? string.Empty
            : node.ToString().Trim();
    }

    private RealmGateInventoryAutomationProfile NewProfile(ClientSessionConnection session, string key) => new()
    {
        ProfileKey = key,
        CharacterName = session.CharacterName,
        ServerProfileName = session.ServerProfileName,
        WorldName = session.WorldName
    };

    private static void NormalizeProfile(RealmGateInventoryAutomationProfile profile, ClientSessionConnection session, string key)
    {
        profile.Version = 1;
        profile.ProfileKey = key;
        profile.CharacterName = session.CharacterName ?? string.Empty;
        profile.ServerProfileName = session.ServerProfileName ?? string.Empty;
        profile.WorldName = session.WorldName ?? string.Empty;
        profile.Preferences ??= new RealmGateInventoryAutomationPreferences();
        profile.EquipmentSets ??= [];
        profile.Rules ??= [];
        profile.Preferences.KeepCurrency = Math.Max(0, profile.Preferences.KeepCurrency);
        profile.Preferences.StateMaximumAgeSeconds = Math.Clamp(profile.Preferences.StateMaximumAgeSeconds, 5, 600);
        profile.Preferences.CommandCooldownSeconds = Math.Clamp(profile.Preferences.CommandCooldownSeconds, 1, 60);
        profile.Preferences.MaximumCommandsPerPass = Math.Clamp(profile.Preferences.MaximumCommandsPerPass, 1, 100);
        foreach (var set in profile.EquipmentSets)
        {
            set.Id = string.IsNullOrWhiteSpace(set.Id) ? Guid.NewGuid().ToString("N") : set.Id;
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Equipment Set" : set.Name.Trim();
            set.Items ??= [];
            var order = 0;
            foreach (var item in set.Items)
            {
                item.ItemId ??= string.Empty;
                item.ItemName = item.ItemName?.Trim() ?? string.Empty;
                item.Slot = item.Slot?.Trim() ?? string.Empty;
                item.Order = order++;
            }
        }
        foreach (var rule in profile.Rules)
        {
            rule.Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id;
            rule.MatchValue = rule.MatchValue?.Trim() ?? string.Empty;
            rule.TargetQuantity = Math.Clamp(rule.TargetQuantity, 1, 9999);
            rule.Priority = Math.Clamp(rule.Priority, -100000, 100000);
            rule.Notes ??= string.Empty;
        }
    }

    private string ProfilePath(string key) => Path.Combine(_profileRoot, key + ".json");

    private static void BackupCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            File.Move(path, path + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}", true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static Regex RoomKeyRegex() => new(@"^(?:room\.)?(?:map)?(\d+)[\./:_-]+(?:room)?(\d+)$|^(\d+)/(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^\s*(\d+)\s*[xX]\s+")]
    private static partial Regex QuantityPrefixRegex();

    [GeneratedRegex(@"^\s*\[[^\]]+\]\s*")]
    private static partial Regex LeadingSlotRegex();

    [GeneratedRegex(@"\s+\([^\)]*\)\s*$")]
    private static partial Regex TrailingAnnotationRegex();
}
