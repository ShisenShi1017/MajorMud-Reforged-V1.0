using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

/// <summary>
/// RealmGate-native inventory/equipment policy engine.
///
/// Decisions are derived only from RealmGate session snapshots, effective Librarium-backed
/// game data, saved RealmGate navigation metadata, and per-character RealmGate preferences.
/// The engine never mutates world data and never writes directly to a network connection;
/// it produces ordinary game commands for PlayViewModel to pass through the existing
/// command-isolation and Emergency Stop path.
/// </summary>
public sealed partial class RealmGateInventoryAutomationService
{
    private static readonly HashSet<string> AmbiguousMultiSlots = new(StringComparer.OrdinalIgnoreCase)
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
        if (!File.Exists(path))
            return CreateProfile(session, key);

        try
        {
            var loaded = JsonSerializer.Deserialize<RealmGateInventoryAutomationProfile>(File.ReadAllText(path), _json)
                         ?? CreateProfile(session, key);
            NormalizeProfile(loaded, session, key);
            return loaded;
        }
        catch (JsonException)
        {
            PreserveUnreadableProfile(path);
            return CreateProfile(session, key);
        }
        catch (IOException)
        {
            return CreateProfile(session, key);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateProfile(session, key);
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
        var destination = ProfilePath(key);
        var temporary = destination + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(profile, _json), new UTF8Encoding(false));
        File.Move(temporary, destination, true);
    }

    public RealmGateEquipmentSet CaptureEquipmentSet(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        string name)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);

        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var captured = new RealmGateEquipmentSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Captured Set" : name.Trim(),
            Enabled = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var order = 0;
        foreach (var displayText in session.EquippedItems)
        {
            var authoritative = catalog.FindByDisplayName(displayText);
            var itemName = authoritative?.Name ?? CleanObservedName(displayText);
            if (itemName.Length == 0)
                continue;

            captured.Items.Add(new RealmGateEquipmentSetEntry
            {
                ItemId = authoritative?.Id ?? string.Empty,
                ItemName = itemName,
                Slot = authoritative?.Slot ?? string.Empty,
                Order = order++
            });
        }

        profile.EquipmentSets.Add(captured);
        return captured;
    }

    public IReadOnlyList<RealmGateInventoryObservedItem> ObserveInventory(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var observed = new List<RealmGateInventoryObservedItem>(session.InventoryItems.Count + session.EquippedItems.Count);
        observed.AddRange(session.InventoryItems.Select(text => ObserveItem(text, false, catalog)));
        observed.AddRange(session.EquippedItems.Select(text => ObserveItem(text, true, catalog)));
        return observed;
    }

    public RealmGateInventoryAutomationPlan PlanEquipmentSet(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        RealmGateEquipmentSet set)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(set);

        var freshness = CheckStateFreshness(session, profile.Preferences, requireInventory: true, requireEquipment: true);
        if (freshness is not null)
        {
            return new RealmGateInventoryAutomationPlan
            {
                Action = $"Refresh state before applying '{set.Name}'",
                RequiresInventoryRefresh = freshness.Value.Inventory,
                RequiresEquipmentRefresh = freshness.Value.Equipment,
                Warnings = freshness.Value.Warnings,
                Fingerprint = $"refresh|{session.SessionId}|{freshness.Value.Inventory}|{freshness.Value.Equipment}"
            };
        }

        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var carried = session.InventoryItems.Select(text => ObserveItem(text, false, catalog)).ToArray();
        var worn = session.EquippedItems.Select(text => ObserveItem(text, true, catalog)).ToArray();
        var desired = set.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemName))
            .OrderBy(item => item.Order)
            .ToArray();

        var controlledSlots = desired
            .Select(item => item.Slot?.Trim() ?? string.Empty)
            .Where(slot => slot.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desiredNames = desired
            .Select(item => CanonicalName(item.ItemName))
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var commands = new List<string>();
        var missing = new List<string>();

        foreach (var current in worn)
        {
            var slot = current.Slot?.Trim() ?? string.Empty;
            if (slot.Length == 0 || !controlledSlots.Contains(slot) || AmbiguousMultiSlots.Contains(slot))
                continue;
            if (desiredNames.Contains(CanonicalName(current.Name)))
                continue;

            AddCommand(commands, ApplyTemplate(profile.Preferences.RemoveCommandTemplate, current.Name, 1, 0));
        }

        foreach (var requested in desired)
        {
            if (worn.Any(item => SameRequestedItem(item, requested)))
                continue;

            var available = carried.FirstOrDefault(item => SameRequestedItem(item, requested));
            if (available is null)
            {
                missing.Add(requested.ItemName);
                continue;
            }

            AddCommand(commands, ApplyTemplate(profile.Preferences.EquipCommandTemplate, requested.ItemName, 1, 0));
        }

        var limited = DistinctCommands(commands, profile.Preferences.MaximumCommandsPerPass);
        var distinctMissing = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new RealmGateInventoryAutomationPlan
        {
            Action = $"Apply equipment set '{set.Name}'",
            Commands = limited,
            MissingItems = distinctMissing,
            Fingerprint = BuildFingerprint("equip", set.Id, limited, distinctMissing)
        };
    }

    public RealmGateInventoryAutomationPlan PlanInventoryRules(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);

        var freshness = CheckStateFreshness(session, profile.Preferences, requireInventory: true, requireEquipment: false);
        if (freshness is not null)
        {
            return new RealmGateInventoryAutomationPlan
            {
                Action = "Refresh inventory before evaluating inventory rules",
                RequiresInventoryRefresh = true,
                Warnings = freshness.Value.Warnings,
                Fingerprint = $"refresh-inventory|{session.SessionId}"
            };
        }

        var catalog = RealmGateWorkshopDataCatalog.Load(session);
        var carried = session.InventoryItems.Select(text => ObserveItem(text, false, catalog)).ToArray();
        var worn = session.EquippedItems.Select(text => ObserveItem(text, true, catalog)).ToArray();
        var location = ReadLocationCapabilities(session);
        var commands = new List<string>();
        var warnings = new List<string>();

        foreach (var item in carried)
        {
            var rule = profile.Rules
                .Where(candidate => candidate.Enabled && candidate.Disposition != RealmGateInventoryDisposition.Buy)
                .OrderBy(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(candidate => RuleMatches(candidate, item));

            if (rule is null || rule.Disposition == RealmGateInventoryDisposition.Keep)
                continue;

            switch (rule.Disposition)
            {
                case RealmGateInventoryDisposition.Sell:
                    if (!profile.Preferences.AllowAutomaticSell)
                        break;
                    if (!location.Shop)
                    {
                        warnings.Add($"Sell rule for {item.Name} is waiting for a decoded shop.");
                        break;
                    }
                    AddCommand(commands, ApplyTemplate(profile.Preferences.SellCommandTemplate, item.Name, item.Quantity, 0));
                    break;

                case RealmGateInventoryDisposition.Stash:
                    if (!profile.Preferences.AllowAutomaticStash)
                        break;
                    if (!location.Stash)
                    {
                        warnings.Add($"Stash rule for {item.Name} is waiting for a saved stash room.");
                        break;
                    }
                    AddCommand(commands, ApplyTemplate(profile.Preferences.StashCommandTemplate, item.Name, item.Quantity, 0));
                    break;

                case RealmGateInventoryDisposition.Discard:
                    if (profile.Preferences.AllowAutomaticDiscard)
                        AddCommand(commands, ApplyTemplate(profile.Preferences.DiscardCommandTemplate, item.Name, item.Quantity, 0));
                    break;
            }
        }

        if (profile.Preferences.AllowAutomaticBuy && location.Shop)
            PlanPurchases(session, profile, catalog, carried, worn, commands, warnings);

        if (profile.Preferences.AutoDepositExcessCurrency && location.Bank)
            PlanCurrencyDeposit(session, profile.Preferences, commands, warnings);

        var limited = DistinctCommands(commands, profile.Preferences.MaximumCommandsPerPass);
        var distinctWarnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new RealmGateInventoryAutomationPlan
        {
            Action = "Process inventory/economy rules",
            Commands = limited,
            Warnings = distinctWarnings,
            Fingerprint = BuildFingerprint("rules", BuildProfileKey(session), limited, distinctWarnings)
        };
    }

    public RealmGateInventoryAutomationPlan PlanRecovery(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        RealmGateEquipmentSet? set)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);

        if (set is null)
        {
            return new RealmGateInventoryAutomationPlan
            {
                Action = "Recovery",
                Warnings = ["Select or configure an equipment set before starting recovery."],
                Fingerprint = "recovery|no-set"
            };
        }

        var commands = new List<string>();
        var warnings = new List<string>();
        var corpseFragment = profile.Preferences.RecoveryCorpseNameContains.Trim();
        var matchingCorpse = session.CurrentRoomItems.Any(item =>
            item.Contains("corpse", StringComparison.OrdinalIgnoreCase) &&
            (corpseFragment.Length == 0 || item.Contains(corpseFragment, StringComparison.OrdinalIgnoreCase)));

        if (corpseFragment.Length == 0)
        {
            warnings.Add("Automatic corpse identity is not guessed. Configure a corpse-name fragment before using recovery looting automatically.");
        }
        else if (matchingCorpse)
        {
            AddCommand(commands, profile.Preferences.RecoveryCorpseCommand);
        }
        else
        {
            warnings.Add("No matching corpse is visible in the latest room snapshot.");
        }

        var equipment = PlanEquipmentSet(session, profile, set);
        if (!equipment.RequiresInventoryRefresh && !equipment.RequiresEquipmentRefresh)
            commands.AddRange(equipment.Commands);
        warnings.AddRange(equipment.Warnings);

        var limited = DistinctCommands(commands, profile.Preferences.MaximumCommandsPerPass);
        var distinctWarnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new RealmGateInventoryAutomationPlan
        {
            Action = $"Recover and restore '{set.Name}'",
            Commands = limited,
            MissingItems = equipment.MissingItems,
            Warnings = distinctWarnings,
            RequiresInventoryRefresh = equipment.RequiresInventoryRefresh,
            RequiresEquipmentRefresh = equipment.RequiresEquipmentRefresh,
            Fingerprint = BuildFingerprint("recovery", set.Id, limited, distinctWarnings)
        };
    }

    public RealmGateEquipmentSet? ResolveAutomaticSet(RealmGateInventoryAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var configuredId = profile.Preferences.AutoEquipmentSetId.Trim();
        if (configuredId.Length > 0)
        {
            var configured = profile.EquipmentSets.FirstOrDefault(set =>
                set.Enabled && set.Id.Equals(configuredId, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
                return configured;
        }

        return profile.EquipmentSets.FirstOrDefault(set => set.Enabled && set.AutoApply)
               ?? profile.EquipmentSets.FirstOrDefault(set =>
                   set.Enabled && set.Name.Equals("Default", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAtDecodedShop(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.CurrentRoomServices.Any(service => service.Kind.Contains("shop", StringComparison.OrdinalIgnoreCase)))
            return true;

        var room = FindCurrentRoom(session);
        if (room is null)
            return false;

        var shopId = ReadInt(room.EffectiveRecord, "environment", "shop_id");
        return shopId > 0 && _gameData.LoadFamily(session, "Shops")
            .Any(shop => ReadInt(shop.EffectiveRecord, "legacy_id") == shopId);
    }

    public bool IsAtDecodedBank(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.CurrentRoomServices.Any(service =>
                service.Kind.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
                service.Kind.Contains("vault", StringComparison.OrdinalIgnoreCase)))
            return true;

        var room = FindCurrentRoom(session);
        if (room is null)
            return false;

        var shopId = ReadInt(room.EffectiveRecord, "environment", "shop_id");
        if (shopId > 0 && _gameData.LoadFamily(session, "Banks")
                .Any(bank => ReadInt(bank.EffectiveRecord, "legacy_id") == shopId))
            return true;

        var roomNumber = ReadInt(room.EffectiveRecord, "room_number");
        if (roomNumber <= 0)
            return false;

        return _gameData.LoadFamily(session, "Banks")
            .Where(bank => ReadInt(bank.EffectiveRecord, "room_number") == roomNumber)
            .Take(2)
            .Count() == 1;
    }

    public bool CurrentShopStocksItem(ClientSessionConnection session, string itemId)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        var room = FindCurrentRoom(session);
        if (room is null)
            return false;

        var shopId = ReadInt(room.EffectiveRecord, "environment", "shop_id");
        var roomId = room.Id;
        var shops = _gameData.LoadFamily(session, "Shops")
            .Where(shop =>
                (shopId > 0 && ReadInt(shop.EffectiveRecord, "legacy_id") == shopId) ||
                ReadText(shop.EffectiveRecord, "room_ref").Equals(roomId, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (shops.Length != 1 || shops[0].EffectiveRecord["inventory"] is not JsonArray inventory)
            return false;

        return inventory
            .OfType<JsonObject>()
            .Any(row => ReadText(row, "item_ref").Equals(itemId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAtSavedStash(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var roomKey = session.CurrentRoomKey.Trim();
        if (roomKey.Length == 0)
            return false;

        var profile = _navigation.LoadProfile(session);
        return profile.SavedDestinations.Any(destination =>
            destination.Kind == RealmGateSavedDestinationKind.Stash &&
            destination.RoomKey.Equals(roomKey, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildProfileKey(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var identity = string.Join("|",
            session.ServerProfileName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.WorldName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.HostName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.Port.ToString(CultureInfo.InvariantCulture),
            session.CharacterName?.Trim().ToLowerInvariant() ?? string.Empty);
        return ShortHash(identity);
    }

    public static string ApplyTemplate(string template, string item, int quantity, long amount)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        return template.Trim()
            .Replace("{item}", item?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{quantity}", Math.Max(1, quantity).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{amount}", Math.Max(0, amount).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private void PlanPurchases(
        ClientSessionConnection session,
        RealmGateInventoryAutomationProfile profile,
        RealmGateWorkshopDataCatalog catalog,
        IReadOnlyList<RealmGateInventoryObservedItem> carried,
        IReadOnlyList<RealmGateInventoryObservedItem> worn,
        ICollection<string> commands,
        ICollection<string> warnings)
    {
        foreach (var rule in profile.Rules
                     .Where(candidate => candidate.Enabled && candidate.Disposition == RealmGateInventoryDisposition.Buy)
                     .OrderBy(candidate => candidate.Priority)
                     .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase))
        {
            var target = ResolvePurchaseTarget(rule, catalog);
            if (target is null)
            {
                warnings.Add($"Buy rule '{rule.MatchValue}' does not resolve to one effective item record.");
                continue;
            }

            var owned = carried.Where(item => SameCatalogItem(item, target)).Sum(item => Math.Max(1, item.Quantity))
                        + worn.Where(item => SameCatalogItem(item, target)).Sum(item => Math.Max(1, item.Quantity));
            var needed = Math.Max(0, rule.TargetQuantity - owned);
            if (needed == 0)
                continue;

            if (!CurrentShopStocksItem(session, target.Id))
            {
                warnings.Add($"Buy rule for {target.Name} is waiting for a decoded current-shop stock reference; availability was not guessed.");
                continue;
            }

            AddCommand(commands, ApplyTemplate(profile.Preferences.BuyCommandTemplate, target.Name, needed, 0));
        }
    }

    private static void PlanCurrencyDeposit(
        ClientSessionConnection session,
        RealmGateInventoryAutomationPreferences preferences,
        ICollection<string> commands,
        ICollection<string> warnings)
    {
        if (session.CarriedCurrency <= preferences.KeepCurrency)
            return;

        var amount = session.CarriedCurrency - Math.Max(0, preferences.KeepCurrency);
        if (preferences.KeepCurrency == 0 && !string.IsNullOrWhiteSpace(preferences.DepositAllCommand))
        {
            AddCommand(commands, preferences.DepositAllCommand);
            return;
        }

        if (preferences.DepositExcessCommandTemplate.Contains("{amount}", StringComparison.OrdinalIgnoreCase))
        {
            AddCommand(commands, ApplyTemplate(preferences.DepositExcessCommandTemplate, string.Empty, 1, amount));
            return;
        }

        warnings.Add("Excess-currency deposit is waiting for a command template containing {amount}; no denomination was guessed.");
    }

    private (bool Shop, bool Bank, bool Stash) ReadLocationCapabilities(ClientSessionConnection session) =>
        (IsAtDecodedShop(session), IsAtDecodedBank(session), IsAtSavedStash(session));

    private static (bool Inventory, bool Equipment, IReadOnlyList<string> Warnings)? CheckStateFreshness(
        ClientSessionConnection session,
        RealmGateInventoryAutomationPreferences preferences,
        bool requireInventory,
        bool requireEquipment)
    {
        var now = DateTimeOffset.Now;
        var staleInventory = requireInventory && !IsFresh(session.InventoryUpdatedAt, now, preferences.StateMaximumAgeSeconds);
        var staleEquipment = requireEquipment && !IsFresh(session.EquipmentUpdatedAt, now, preferences.StateMaximumAgeSeconds);
        if (!staleInventory && !staleEquipment)
            return null;

        var warnings = new List<string>();
        if (staleInventory)
            warnings.Add("Inventory snapshot is stale or missing.");
        if (staleEquipment)
            warnings.Add("Equipment snapshot is stale or missing.");
        return (staleInventory, staleEquipment, warnings);
    }

    private static RealmGateInventoryObservedItem ObserveItem(
        string displayText,
        bool equipped,
        RealmGateWorkshopDataCatalog catalog)
    {
        var record = catalog.FindByDisplayName(displayText);
        return new RealmGateInventoryObservedItem(
            displayText,
            record?.Name ?? CleanObservedName(displayText),
            record?.Id ?? string.Empty,
            record?.Slot ?? string.Empty,
            record?.ItemType ?? string.Empty,
            ParseQuantity(displayText),
            equipped);
    }

    private static bool SameRequestedItem(RealmGateInventoryObservedItem observed, RealmGateEquipmentSetEntry requested)
    {
        if (!string.IsNullOrWhiteSpace(requested.ItemId) && !string.IsNullOrWhiteSpace(observed.ItemId))
            return observed.ItemId.Equals(requested.ItemId, StringComparison.OrdinalIgnoreCase);
        return CanonicalName(observed.Name).Equals(CanonicalName(requested.ItemName), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameCatalogItem(RealmGateInventoryObservedItem observed, RealmGateWorkshopItem target)
    {
        if (!string.IsNullOrWhiteSpace(observed.ItemId))
            return observed.ItemId.Equals(target.Id, StringComparison.OrdinalIgnoreCase);
        return CanonicalName(observed.Name).Equals(CanonicalName(target.Name), StringComparison.OrdinalIgnoreCase);
    }

    private static RealmGateWorkshopItem? ResolvePurchaseTarget(
        RealmGateInventoryRule rule,
        RealmGateWorkshopDataCatalog catalog)
    {
        var value = rule.MatchValue.Trim();
        if (value.Length == 0)
            return null;

        return rule.MatchMode switch
        {
            RealmGateInventoryRuleMatchMode.ItemId => catalog.Items.FirstOrDefault(item =>
                item.Id.Equals(value, StringComparison.OrdinalIgnoreCase)),
            RealmGateInventoryRuleMatchMode.ExactName => catalog.Items.FirstOrDefault(item =>
                item.Name.Equals(value, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private static bool RuleMatches(RealmGateInventoryRule rule, RealmGateInventoryObservedItem observed)
    {
        var value = rule.MatchValue.Trim();
        if (value.Length == 0)
            return false;

        return rule.MatchMode switch
        {
            RealmGateInventoryRuleMatchMode.ExactName => observed.Name.Equals(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.NameContains => observed.Name.Contains(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.ItemId => observed.ItemId.Equals(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.Slot => observed.Slot.Equals(value, StringComparison.OrdinalIgnoreCase),
            RealmGateInventoryRuleMatchMode.ItemType => observed.ItemType.Contains(value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private RealmGateGameDataRecord? FindCurrentRoom(ClientSessionConnection session)
    {
        if (!TryReadRoomKey(session.CurrentRoomKey, out var map, out var room))
            return null;

        return _gameData.LoadFamily(session, "Rooms").FirstOrDefault(record =>
            ReadInt(record.EffectiveRecord, "map_number") == map &&
            ReadInt(record.EffectiveRecord, "room_number") == room);
    }

    private static bool TryReadRoomKey(string? value, out int map, out int room)
    {
        map = 0;
        room = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var match = RoomKeyRegex().Match(value.Trim());
        if (!match.Success)
            return false;

        var mapText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
        var roomText = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
        return int.TryParse(mapText, NumberStyles.Integer, CultureInfo.InvariantCulture, out map)
               && int.TryParse(roomText, NumberStyles.Integer, CultureInfo.InvariantCulture, out room)
               && map > 0
               && room > 0;
    }

    private static int ReadInt(JsonObject root, params string[] path)
    {
        JsonNode? node = root;
        foreach (var segment in path)
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(segment, out node) || node is null)
                return 0;
        }

        if (node is not JsonValue value)
            return 0;
        if (value.TryGetValue<int>(out var integer))
            return integer;
        if (value.TryGetValue<long>(out var wide) && wide is >= int.MinValue and <= int.MaxValue)
            return (int)wide;
        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)
            ? integer
            : 0;
    }

    private static string ReadText(JsonObject root, params string[] path)
    {
        JsonNode? node = root;
        foreach (var segment in path)
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(segment, out node) || node is null)
                return string.Empty;
        }

        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text?.Trim() ?? string.Empty
            : node.ToString().Trim();
    }

    private RealmGateInventoryAutomationProfile CreateProfile(ClientSessionConnection session, string key) => new()
    {
        ProfileKey = key,
        CharacterName = session.CharacterName ?? string.Empty,
        ServerProfileName = session.ServerProfileName ?? string.Empty,
        WorldName = session.WorldName ?? string.Empty
    };

    private static void NormalizeProfile(
        RealmGateInventoryAutomationProfile profile,
        ClientSessionConnection session,
        string key)
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
            var index = 0;
            foreach (var item in set.Items)
            {
                item.ItemId ??= string.Empty;
                item.ItemName = item.ItemName?.Trim() ?? string.Empty;
                item.Slot = item.Slot?.Trim() ?? string.Empty;
                item.Order = index++;
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

    private static IReadOnlyList<string> DistinctCommands(IEnumerable<string> commands, int maximum) =>
        commands
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(command => command.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximum, 1, 100))
            .ToArray();

    private static void AddCommand(ICollection<string> commands, string? command)
    {
        if (!string.IsNullOrWhiteSpace(command))
            commands.Add(command.Trim());
    }

    private static string BuildFingerprint(
        string category,
        string identity,
        IEnumerable<string> commands,
        IEnumerable<string> details) =>
        ShortHash(category + "|" + identity + "|" + string.Join("|", commands) + "|" + string.Join("|", details));

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    private static bool IsFresh(DateTimeOffset? timestamp, DateTimeOffset now, int maximumAgeSeconds) =>
        timestamp is { } value && now - value <= TimeSpan.FromSeconds(Math.Clamp(maximumAgeSeconds, 5, 600));

    private static int ParseQuantity(string value)
    {
        var match = QuantityPrefixRegex().Match(value ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
            ? Math.Max(1, quantity)
            : 1;
    }

    private static string CleanObservedName(string value)
    {
        var text = (value ?? string.Empty).Trim();
        text = QuantityPrefixRegex().Replace(text, string.Empty);
        text = LeadingSlotRegex().Replace(text, string.Empty);
        text = TrailingAnnotationRegex().Replace(text, string.Empty);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string CanonicalName(string value) =>
        Regex.Replace(CleanObservedName(value).ToLowerInvariant(), @"^(a|an|the)\s+", string.Empty).Trim();

    private string ProfilePath(string key) => Path.Combine(_profileRoot, key + ".json");

    private static void PreserveUnreadableProfile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Move(path, path + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}", true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Regex RoomKeyRegex() => new(
        @"^(?:room\.)?(?:map)?(\d+)[\./:_-]+(?:room)?(\d+)$|^(\d+)/(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^\s*(\d+)\s*[xX]\s+")]
    private static partial Regex QuantityPrefixRegex();

    [GeneratedRegex(@"^\s*\[[^\]]+\]\s*")]
    private static partial Regex LeadingSlotRegex();

    [GeneratedRegex(@"\s+\([^\)]*\)\s*$")]
    private static partial Regex TrailingAnnotationRegex();
}