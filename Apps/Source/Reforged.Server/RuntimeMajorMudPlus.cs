using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeMajorMudPlusDescription(
    string CharacterId,
    string DisplayName,
    IReadOnlyList<string> Lines,
    bool Approved,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeMajorMudPlusRegistry(
    string CharacterId,
    string DisplayName,
    IReadOnlyDictionary<string, string> Values,
    DateTimeOffset UpdatedAtUtc,
    long PlayMinutes = 0);

public sealed record RuntimeMajorMudPlusAction(
    string Name,
    bool BoardSpecific,
    IReadOnlyDictionary<string, string> Messages,
    bool WccOrigin,
    bool Deleted,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeMajorMudPlusContest(string State, string Mode, DateTimeOffset UpdatedAtUtc);
public sealed record RuntimeMajorMudPlusContestRange(int Map, int MinimumRoom, int MaximumRoom, int Quantity);
public sealed record RuntimeMajorMudPlusContestContract(
    string CanonicalItemId, int LegacyItemNumber, int MaximumAttemptsPerItem,
    int ExcludedRoomAttributeMask, int VisibleRoomCapacity, int HiddenRoomCapacity,
    IReadOnlyList<RuntimeMajorMudPlusContestRange> PlacementRanges);

public sealed record RuntimeMajorMudPlusState(
    IReadOnlyDictionary<string, RuntimeMajorMudPlusDescription> Descriptions,
    IReadOnlyDictionary<string, RuntimeMajorMudPlusRegistry> Registries,
    IReadOnlyDictionary<string, RuntimeMajorMudPlusAction> Actions,
    RuntimeMajorMudPlusContest Contest,
    long Revision)
{
    public static RuntimeMajorMudPlusState Empty { get; } = new(
        new Dictionary<string, RuntimeMajorMudPlusDescription>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, RuntimeMajorMudPlusRegistry>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, RuntimeMajorMudPlusAction>(StringComparer.OrdinalIgnoreCase),
        new("inactive", "visible", DateTimeOffset.UnixEpoch),
        0);
}

public sealed record RuntimeMajorMudPlusSettings(
    int DescriptionCreditCost,
    long DescriptionCopperCost,
    int DescriptionMembershipDayCost,
    int LifeCreditCost,
    long LifeCopperCost,
    int LifeMembershipDayCost,
    bool LifePurchaseEnabled,
    int MaximumLives,
    int MinutesPerAgeYear,
    int StartingAge,
    int MaximumDescriptionLines,
    bool DescriptionApprovalRequired,
    string ApprovalNotificationUser,
    string RegistryValueAnsi,
    string GlobalRegistryCommand);

public sealed class RuntimeMajorMudPlusCatalog
{
    private RuntimeMajorMudPlusCatalog(
        string productName,
        string persistenceAuthority,
        RuntimeMajorMudPlusSettings settings,
        IReadOnlySet<string> playerFeatures,
        IReadOnlySet<string> sysopFeatures,
        IReadOnlySet<string> actionEditorFields,
        IReadOnlySet<string> actionVariables,
        IReadOnlySet<string> contestModes,
        RuntimeMajorMudPlusContestContract contestContract,
        IReadOnlyDictionary<string, RuntimeMajorMudPlusAction> defaultActions)
    {
        ProductName = productName;
        PersistenceAuthority = persistenceAuthority;
        Settings = settings;
        PlayerFeatures = playerFeatures;
        SysopFeatures = sysopFeatures;
        ActionEditorFields = actionEditorFields;
        ActionVariables = actionVariables;
        ContestModes = contestModes;
        ContestContract = contestContract;
        DefaultActions = defaultActions;
    }
    public string ProductName { get; }
    public string PersistenceAuthority { get; }
    public RuntimeMajorMudPlusSettings Settings { get; }
    public IReadOnlySet<string> PlayerFeatures { get; }
    public IReadOnlySet<string> SysopFeatures { get; }
    public IReadOnlySet<string> ActionEditorFields { get; }
    public IReadOnlySet<string> ActionVariables { get; }
    public IReadOnlySet<string> ContestModes { get; }
    public RuntimeMajorMudPlusContestContract ContestContract { get; }
    public IReadOnlyDictionary<string, RuntimeMajorMudPlusAction> DefaultActions { get; }

    public static RuntimeMajorMudPlusCatalog Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "majormud_plus.json");
        if (!File.Exists(path)) throw new InvalidDataException($"Generated MajorMUD Plus data was not found: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array || records.GetArrayLength() != 1)
            throw new InvalidDataException("Generated MajorMUD Plus data must contain exactly one contract.");
        var record = records[0];
        var settings = record.GetProperty("settings");
        return new RuntimeMajorMudPlusCatalog(
            record.GetProperty("product_name").GetString() ?? throw new InvalidDataException("MajorMUD Plus product name is missing."),
            record.GetProperty("persistence_authority").GetString() ?? throw new InvalidDataException("MajorMUD Plus persistence authority is missing."),
            new(
            ReadInt(settings, "description_credit_cost"),
            ReadLong(settings, "description_copper_cost"),
            ReadInt(settings, "description_membership_day_cost"),
            ReadInt(settings, "life_credit_cost"),
            ReadLong(settings, "life_copper_cost"),
            ReadInt(settings, "life_membership_day_cost"),
            settings.GetProperty("life_purchase_enabled").GetBoolean(),
            ReadInt(settings, "maximum_lives"),
            ReadInt(settings, "minutes_per_age_year"),
            ReadInt(settings, "starting_age"),
            ReadInt(settings, "maximum_description_lines"),
            settings.GetProperty("description_approval_required").GetBoolean(),
            settings.GetProperty("approval_notification_user").GetString() ?? "Sysop",
            settings.GetProperty("registry_value_ansi").GetString() ?? string.Empty,
            settings.GetProperty("global_registry_command").GetString() ?? "/mudreg"),
            ReadStrings(record, "player_features"),
            ReadStrings(record, "sysop_features"),
            ReadStrings(record, "action_editor_fields"),
            ReadStrings(record, "action_variables"),
            ReadStrings(record, "contest_modes"),
            ReadContestContract(record),
            ReadDefaultActions(record));
    }

    private static IReadOnlyDictionary<string, RuntimeMajorMudPlusAction> ReadDefaultActions(JsonElement record)
    {
        if (!record.TryGetProperty("default_actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("MajorMUD Plus default WCC actions are missing.");
        var result = new Dictionary<string, RuntimeMajorMudPlusAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in actions.EnumerateArray())
        {
            var name = value.GetProperty("name").GetString() ?? throw new InvalidDataException("A WCC action name is missing.");
            var messages = value.GetProperty("messages").EnumerateObject().ToDictionary(
                property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            if (!result.TryAdd(name, new(name, value.GetProperty("board_specific").GetBoolean(), messages, true, false, DateTimeOffset.UnixEpoch)))
                throw new InvalidDataException($"Duplicate WCC action '{name}'.");
        }
        if (result.Count != 64) throw new InvalidDataException($"Expected 64 native WCC actions; found {result.Count}.");
        return result;
    }

    private static RuntimeMajorMudPlusContestContract ReadContestContract(JsonElement record)
    {
        var contract = record.GetProperty("contest_contract");
        var ranges = contract.GetProperty("placement_ranges").EnumerateArray().Select(range => new RuntimeMajorMudPlusContestRange(
            ReadInt(range, "map"), ReadInt(range, "minimum_room"), ReadInt(range, "maximum_room"), ReadInt(range, "quantity"))).ToArray();
        var parsed = new RuntimeMajorMudPlusContestContract(
            contract.GetProperty("canonical_item_id").GetString() ?? throw new InvalidDataException("Contest item is missing."),
            ReadInt(contract, "legacy_item_number"), ReadInt(contract, "maximum_attempts_per_item"),
            ReadInt(contract, "excluded_room_attribute_mask"), ReadInt(contract, "visible_room_capacity"),
            ReadInt(contract, "hidden_room_capacity"), ranges);
        if (parsed.LegacyItemNumber != 965 || parsed.CanonicalItemId != "item.965" || ranges.Sum(value => value.Quantity) != 40)
            throw new InvalidDataException("MajorMUD Plus native contest contract is incomplete.");
        return parsed;
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : throw new InvalidDataException($"MajorMUD Plus setting '{name}' is missing or invalid.");

    private static long ReadLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var parsed)
            ? parsed
            : throw new InvalidDataException($"MajorMUD Plus setting '{name}' is missing or invalid.");

    private static IReadOnlySet<string> ReadStrings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"MajorMUD Plus contract '{name}' is missing.");
        return values.EnumerateArray().Select(value => value.GetString() ?? string.Empty)
            .Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class RuntimeMajorMudPlusService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> RegistryFields = new(
        ["birthplace", "associates", "enemies", "favorite_hangout", "favorite_weapon", "demeanor",
         "dislikes", "comments_one", "comments_two", "last_words", "age"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ActionMessageFields = new(
        ["single_actor_message", "single_room_message", "user_actor_message", "user_target_message", "user_room_message",
         "monster_actor_message", "monster_room_message", "inventory_item_actor_message", "inventory_item_room_message",
         "floor_item_actor_message", "floor_item_room_message"],
        StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeAccountSecurityService _security;
    private readonly RuntimeSessionManager? _sessions;
    private readonly object _gate = new();

    public RuntimeMajorMudPlusService(RuntimeCharacterService characters, RuntimeSessionManager? sessions = null)
    {
        _characters = characters;
        _security = new RuntimeAccountSecurityService(characters.ProjectRoot);
        _sessions = sessions;
    }

    public bool TryExecuteAction(RuntimeSession session, RuntimeCommand command, out string result, RuntimeLivingWorldService? livingWorld = null)
    {
        result = string.Empty;
        if (session.Actor is not RuntimeCharacterActor actor) return false;
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return false;
        var state = Load(character.WorldId);
        if (!state.Actions.TryGetValue(command.Name, out var action) || action.Deleted) return false;
        var actorName = session.Actor.DisplayName;
        RuntimeSession? target = null;
        RuntimeLivingNpcState? targetNpc = null;
        RuntimeItemInstance? targetItem = null;
        string? targetName = null;
        string actorKey;
        string roomKey;
        string? targetKey = null;
        if (command.Arguments.Count > 0)
        {
            target = _sessions?.FindUniqueInRoom(session, command.ArgumentText);
            if (target is not null)
            {
                targetName = target.Actor.DisplayName;
                actorKey = "user_actor_message";
                targetKey = "user_target_message";
                roomKey = "user_room_message";
            }
            else
            {
                var query = command.ArgumentText.Trim();
                var npcs = livingWorld?.NpcsInRoom(session.CurrentRoom.Id).Where(npc => npc.Alive
                    && (npc.InstanceId.Equals(query, StringComparison.OrdinalIgnoreCase) || npc.Name.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray()
                    ?? Array.Empty<RuntimeLivingNpcState>();
                if (npcs.Length > 1) return false;
                targetNpc = npcs.SingleOrDefault();
                if (targetNpc is not null)
                {
                    targetName = targetNpc.Name;
                    actorKey = "monster_actor_message";
                    roomKey = "monster_room_message";
                }
                else
                {
                    var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
                    var carried = character.ItemState?.Items.Where(item => item.OwnerId?.Equals(actor.Id, StringComparison.OrdinalIgnoreCase) == true
                        && item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                        && (item.InstanceId.Equals(query, StringComparison.OrdinalIgnoreCase) || catalog.Matches(item.CanonicalItemId, query))).ToArray()
                        ?? Array.Empty<RuntimeItemInstance>();
                    if (carried.Length > 1) return false;
                    targetItem = carried.SingleOrDefault();
                    if (targetItem is not null)
                    {
                        targetName = catalog.Require(targetItem.CanonicalItemId).Name;
                        actorKey = "inventory_item_actor_message";
                        roomKey = "inventory_item_room_message";
                    }
                    else
                    {
                        var floor = livingWorld?.GroundItems(session.CurrentRoom.Id)
                            .Where(item => RuntimeBundleDSearchRuntime.CanCharacterSeeGroundItem(actor.Id, item)
                                && (item.InstanceId.Equals(query, StringComparison.OrdinalIgnoreCase) || catalog.Matches(item.CanonicalItemId, query))).ToArray()
                            ?? Array.Empty<RuntimeItemInstance>();
                        if (floor.Length != 1) return false;
                        targetItem = floor[0];
                        targetName = catalog.Require(targetItem.CanonicalItemId).Name;
                        actorKey = "floor_item_actor_message";
                        roomKey = "floor_item_room_message";
                    }
                }
            }
        }
        else
        {
            actorKey = "single_actor_message";
            roomKey = "single_room_message";
        }
        result = action.Messages.TryGetValue(actorKey, out var actorMessage)
            ? RenderAction(actorMessage, session, targetName)
            : string.Empty;
        if (target is not null && targetKey is not null && action.Messages.TryGetValue(targetKey, out var targetMessage))
            target.EnqueueMessage(RenderAction(targetMessage, session, targetName));
        if (_sessions is not null && action.Messages.TryGetValue(roomKey, out var roomMessage))
        {
            foreach (var observer in _sessions.InRoom(character.WorldId, session.CurrentRoom.Id)
                         .Where(candidate => candidate.Id != session.Id && candidate.Id != target?.Id))
                observer.EnqueueMessage(RenderAction(roomMessage, session, targetName));
        }
        return true;
    }

    private static string RenderAction(string template, RuntimeSession actor, string? targetName)
    {
        var sex = actor.Actor is RuntimeCharacterActor character ? character.Sex : "Unspecified";
        var possessive = sex.Equals("Female", StringComparison.OrdinalIgnoreCase) ? "her" :
            sex.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "his" : "their";
        var objective = sex.Equals("Female", StringComparison.OrdinalIgnoreCase) ? "her" :
            sex.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "him" : "them";
        var rendered = template
            .Replace("%i", actor.Actor.DisplayName, StringComparison.Ordinal)
            .Replace("%p", possessive, StringComparison.Ordinal)
            .Replace("%P", objective, StringComparison.Ordinal)
            .Replace("%t", targetName ?? string.Empty, StringComparison.Ordinal)
            .Replace("%B", "\u001b[1m", StringComparison.Ordinal)
            .Replace("%N", "\u001b[0m", StringComparison.Ordinal)
            .Replace("%U", "\u001b[4m", StringComparison.Ordinal)
            .Replace("%L", "\u001b[5m", StringComparison.Ordinal)
            .Replace("%R", "\u001b[7m", StringComparison.Ordinal);
        for (var color = 0; color <= 7; color++)
        {
            rendered = rendered
                .Replace($"%f{color}", $"\u001b[3{color}m", StringComparison.Ordinal)
                .Replace($"%b{color}", $"\u001b[4{color}m", StringComparison.Ordinal);
        }
        return rendered;
    }

    public string Execute(RuntimeSession session, RuntimeCommand command, RuntimeLivingWorldService? livingWorld = null)
    {
        if (session.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("A persistent character is required.");
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var catalog = RuntimeMajorMudPlusCatalog.Load(_characters.ProjectRoot, character.WorldId);
        return command.Name switch
        {
            "mudplus" => RenderMenu(character, catalog),
            "description" => ExecuteDescription(character, command.ArgumentText, catalog),
            "mudreg" => ExecuteRegistry(character, command.ArgumentText, catalog),
            "buylife" => BuyLife(character, catalog),
            "plusadmin" => ExecuteAdmin(character, command.Arguments, catalog, livingWorld),
            _ => throw new InvalidOperationException("Unsupported MajorMUD Plus command.")
        };
    }

    public bool TryGetApprovedDescription(string worldId, string characterId, out IReadOnlyList<string> lines)
    {
        var state = Load(worldId);
        if (state.Descriptions.TryGetValue(characterId, out var description) && description.Approved)
        {
            lines = description.Lines;
            return true;
        }
        lines = Array.Empty<string>();
        return false;
    }

    private string RenderMenu(RuntimeCharacterRecord character, RuntimeMajorMudPlusCatalog catalog)
    {
        var state = Load(character.WorldId);
        var description = state.Descriptions.TryGetValue(character.CharacterId, out var value)
            ? value.Approved ? "approved" : "awaiting approval"
            : "not set";
        return "MajorMUD Plus - Entertainment Edition" + Environment.NewLine +
               $"Description: {description}" + Environment.NewLine +
               "Commands: DESCRIPTION, DESCRIPTION SET <line1>|<line2>, MUDREG, MUDREG SET <field> <value>, " +
               "MUDREG SEARCH <text>, BUYLIFE.";
    }

    private string ExecuteDescription(RuntimeCharacterRecord character, string argument, RuntimeMajorMudPlusCatalog catalog)
    {
        lock (_gate)
        {
            var state = Load(character.WorldId);
            if (string.IsNullOrWhiteSpace(argument))
            {
                if (!state.Descriptions.TryGetValue(character.CharacterId, out var current)) return "You have no custom character description.";
                var visibility = current.Approved ? "approved" : "awaiting sysop approval";
                return $"{current.DisplayName}'s custom description ({visibility}):" + Environment.NewLine + string.Join(Environment.NewLine, current.Lines);
            }
            if (!argument.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Usage: DESCRIPTION SET <line1>|<line2>.");
            var lines = argument[4..].Split('|', StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0).ToArray();
            if (lines.Length == 0 || lines.Length > catalog.Settings.MaximumDescriptionLines)
                throw new InvalidOperationException($"Descriptions require 1-{catalog.Settings.MaximumDescriptionLines} non-empty lines separated by '|'.");
            if (lines.Any(line => line.Length > 240))
                throw new InvalidOperationException("Description lines cannot exceed 240 characters.");

            var charged = DebitHostAndGameCosts(
                character,
                catalog.Settings.DescriptionCreditCost,
                catalog.Settings.DescriptionCopperCost,
                catalog.Settings.DescriptionMembershipDayCost);
            var descriptions = Copy(state.Descriptions);
            var approved = !catalog.Settings.DescriptionApprovalRequired;
            descriptions[character.CharacterId] = new(character.CharacterId, character.DisplayName, lines, approved, DateTimeOffset.UtcNow);
            Save(character.WorldId, state with { Descriptions = descriptions, Revision = checked(state.Revision + 1) });
            try
            {
                _characters.SaveCharacter(charged, "majormud-plus-description");
            }
            catch
            {
                Save(character.WorldId, state);
                throw;
            }
            return approved ? "Your custom description has been saved." : "Your custom description has been saved and is awaiting sysop approval.";
        }
    }

    private string ExecuteRegistry(RuntimeCharacterRecord character, string argument, RuntimeMajorMudPlusCatalog catalog)
    {
        lock (_gate)
        {
            var state = Load(character.WorldId);
            if (argument.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = argument[4..].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !RegistryFields.Contains(parts[0]))
                    throw new InvalidOperationException($"Usage: MUDREG SET <{string.Join('|', RegistryFields.OrderBy(x => x))}> <value>.");
                if (parts[1].Length > 240) throw new InvalidOperationException("Registry values cannot exceed 240 characters.");
                var registries = Copy(state.Registries);
                var values = registries.TryGetValue(character.CharacterId, out var current)
                    ? new Dictionary<string, string>(current.Values, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                values[parts[0]] = parts[1];
                var playMinutes = ReadConditionInt(character.Conditions, "playtime_minutes", 0);
                registries[character.CharacterId] = new(character.CharacterId, character.DisplayName, values, DateTimeOffset.UtcNow, playMinutes);
                Save(character.WorldId, state with { Registries = registries, Revision = checked(state.Revision + 1) });
                return $"Registry field '{parts[0]}' saved.";
            }
            if (argument.StartsWith("search ", StringComparison.OrdinalIgnoreCase))
            {
                var query = argument[7..].Trim();
                if (query.Length == 0) throw new InvalidOperationException("Usage: MUDREG SEARCH <text>.");
                var matches = state.Registries.Values.Where(entry =>
                        entry.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        entry.Values.Values.Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
                return matches.Length == 0 ? "No registry entries found." : $"{matches.Length} registry entries found: {string.Join(", ", matches.Select(x => x.DisplayName))}";
            }
            var target = string.IsNullOrWhiteSpace(argument) ? character.DisplayName : argument.Trim();
            var found = state.Registries.Values.Where(entry =>
                entry.CharacterId.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                entry.DisplayName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                entry.DisplayName.Contains(target, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (found.Length != 1) return found.Length == 0 ? "No registry entries found." : "Registry lookup is ambiguous.";
            return RenderRegistry(found[0], character, catalog);
        }
    }

    private static string RenderRegistry(RuntimeMajorMudPlusRegistry registry, RuntimeCharacterRecord viewer, RuntimeMajorMudPlusCatalog catalog)
    {
        var lines = new List<string> { "MajorMUD Registry", $"Name: {registry.DisplayName}" };
        lines.AddRange(registry.Values.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key.Replace('_', ' ')}: {catalog.Settings.RegistryValueAnsi}{pair.Value}\u001b[0m"));
        if (!registry.Values.ContainsKey("age"))
        {
            var currentMinutes = registry.CharacterId.Equals(viewer.CharacterId, StringComparison.OrdinalIgnoreCase)
                ? ReadConditionInt(viewer.Conditions, "playtime_minutes", checked((int)Math.Min(int.MaxValue, registry.PlayMinutes)))
                : registry.PlayMinutes;
            var age = checked(catalog.Settings.StartingAge + (int)(currentMinutes / catalog.Settings.MinutesPerAgeYear));
            lines.Add($"age: {age}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private string BuyLife(RuntimeCharacterRecord character, RuntimeMajorMudPlusCatalog catalog)
    {
        if (!catalog.Settings.LifePurchaseEnabled || catalog.Settings.LifeCreditCost < 0)
            throw new InvalidOperationException("Life purchase is disabled.");
        var lives = ReadConditionInt(character.Conditions, "lives", 0);
        if (lives >= catalog.Settings.MaximumLives) throw new InvalidOperationException("You may not purchase any more lives.");
        var charged = DebitHostAndGameCosts(character, catalog.Settings.LifeCreditCost, catalog.Settings.LifeCopperCost, catalog.Settings.LifeMembershipDayCost);
        var conditions = new Dictionary<string, string>(charged.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            ["lives"] = checked(lives + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        _characters.SaveCharacter(charged with { Conditions = conditions }, "majormud-plus-life-purchase");
        return "You have purchased an additional life for your character.";
    }

    private string ExecuteAdmin(RuntimeCharacterRecord actor, IReadOnlyList<string> arguments, RuntimeMajorMudPlusCatalog catalog, RuntimeLivingWorldService? livingWorld)
    {
        RequireSysop(actor);
        if (arguments.Count == 0)
            return "PLUSADMIN PENDING|APPROVE|VIEW|DELETE DESCRIPTION|DELETE REGISTRY|ACTION|CONTEST|CLEAR CONFIRM";
        lock (_gate)
        {
            var state = Load(actor.WorldId);
            var operation = arguments[0].ToLowerInvariant();
            if (operation == "pending")
            {
                var pending = state.Descriptions.Values.Where(x => !x.Approved).OrderBy(x => x.DisplayName).Select(x => x.DisplayName).ToArray();
                return pending.Length == 0 ? "No descriptions await approval." : string.Join(Environment.NewLine, pending);
            }
            if (operation is "approve" or "view")
            {
                var target = ResolveDescription(state, string.Join(' ', arguments.Skip(1)));
                if (operation == "view") return string.Join(Environment.NewLine, target.Lines);
                var descriptions = Copy(state.Descriptions);
                descriptions[target.CharacterId] = target with { Approved = true, UpdatedAtUtc = DateTimeOffset.UtcNow };
                Save(actor.WorldId, state with { Descriptions = descriptions, Revision = checked(state.Revision + 1) });
                return $"{target.DisplayName}'s description is approved.";
            }
            if (operation == "delete" && arguments.Count >= 3)
            {
                var kind = arguments[1].ToLowerInvariant();
                var query = string.Join(' ', arguments.Skip(2));
                if (kind == "description")
                {
                    var target = ResolveDescription(state, query);
                    var descriptions = Copy(state.Descriptions);
                    descriptions.Remove(target.CharacterId);
                    Save(actor.WorldId, state with { Descriptions = descriptions, Revision = checked(state.Revision + 1) });
                    return $"{target.DisplayName}'s description was deleted.";
                }
                if (kind == "registry")
                {
                    var target = ResolveRegistry(state, query);
                    var registries = Copy(state.Registries);
                    registries.Remove(target.CharacterId);
                    Save(actor.WorldId, state with { Registries = registries, Revision = checked(state.Revision + 1) });
                    return $"{target.DisplayName}'s registry was deleted.";
                }
            }
            if (operation == "action") return ExecuteActionAdmin(actor.WorldId, state, arguments.Skip(1).ToArray(), catalog);
            if (operation == "contest") return ExecuteContestAdmin(actor.WorldId, state, arguments.Skip(1).ToArray(), catalog, livingWorld);
            if (operation == "clear" && arguments.Count == 2 && arguments[1].Equals("confirm", StringComparison.OrdinalIgnoreCase))
            {
                Save(actor.WorldId, state with
                {
                    Descriptions = new Dictionary<string, RuntimeMajorMudPlusDescription>(StringComparer.OrdinalIgnoreCase),
                    Registries = new Dictionary<string, RuntimeMajorMudPlusRegistry>(StringComparer.OrdinalIgnoreCase),
                    Revision = checked(state.Revision + 1)
                });
                return "All MajorMUD Plus descriptions and registries were cleared.";
            }
            throw new InvalidOperationException("Invalid PLUSADMIN operation or missing explicit confirmation.");
        }
    }

    private string ExecuteActionAdmin(string worldId, RuntimeMajorMudPlusState state, IReadOnlyList<string> arguments, RuntimeMajorMudPlusCatalog catalog)
    {
        if (arguments.Count == 0 || arguments[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            return state.Actions.Count == 0 ? "No Plus actions are defined." : string.Join(", ", state.Actions.Values.Where(x => !x.Deleted).OrderBy(x => x.Name).Select(x => x.Name));
        var operation = arguments[0].ToLowerInvariant();
        if (operation == "view" && arguments.Count >= 2)
        {
            if (!state.Actions.TryGetValue(arguments[1], out var action)) throw new InvalidOperationException("Plus action was not found.");
            return JsonSerializer.Serialize(action, JsonOptions);
        }
        if (operation is "new" or "change")
        {
            if (arguments.Count < 4) throw new InvalidOperationException("Usage: PLUSADMIN ACTION NEW|CHANGE <name> <message-key> <message>.");
            var name = arguments[1].Trim().ToLowerInvariant();
            var key = arguments[2].Trim().ToLowerInvariant();
            var text = string.Join(' ', arguments.Skip(3));
            if (name.Length == 0 || text.Length == 0) throw new InvalidOperationException("Action name and value are required.");
            if (!catalog.ActionEditorFields.Contains(key))
                throw new InvalidOperationException("Unknown Plus action message field.");
            var actions = Copy(state.Actions);
            var existing = actions.TryGetValue(name, out var current) ? current : new RuntimeMajorMudPlusAction(name, true, new Dictionary<string, string>(), false, false, DateTimeOffset.UtcNow);
            if (operation == "new" && actions.ContainsKey(name) && !existing.Deleted) throw new InvalidOperationException("Plus action already exists.");
            if (key.Equals("action_name", StringComparison.OrdinalIgnoreCase))
            {
                if (operation == "new") throw new InvalidOperationException("NEW already supplies the action name.");
                var replacementName = text.Trim().ToLowerInvariant();
                if (actions.ContainsKey(replacementName) && !replacementName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The replacement action name already exists.");
                actions.Remove(name);
                actions[replacementName] = existing with { Name = replacementName, Deleted = false, UpdatedAtUtc = DateTimeOffset.UtcNow };
                Save(worldId, state with { Actions = actions, Revision = checked(state.Revision + 1) });
                return $"Plus action '{name}' renamed to '{replacementName}'.";
            }
            if (key.Equals("board_specific", StringComparison.OrdinalIgnoreCase))
            {
                var boardSpecific = text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : text.Equals("no", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase)
                        ? false
                        : throw new InvalidOperationException("Board-specific must be YES or NO.");
                actions[name] = existing with { BoardSpecific = boardSpecific, Deleted = false, UpdatedAtUtc = DateTimeOffset.UtcNow };
                Save(worldId, state with { Actions = actions, Revision = checked(state.Revision + 1) });
                return $"Plus action '{name}' saved.";
            }
            if (!ActionMessageFields.Contains(key)) throw new InvalidOperationException("Unknown Plus action message field.");
            var messages = new Dictionary<string, string>(existing.Messages, StringComparer.OrdinalIgnoreCase) { [key] = text };
            actions[name] = existing with { Messages = messages, Deleted = false, UpdatedAtUtc = DateTimeOffset.UtcNow };
            Save(worldId, state with { Actions = actions, Revision = checked(state.Revision + 1) });
            return $"Plus action '{name}' saved.";
        }
        if (operation == "delete" && arguments.Count == 2)
        {
            var actions = Copy(state.Actions);
            if (!actions.TryGetValue(arguments[1], out var action)) throw new InvalidOperationException("Plus action was not found.");
            if (action.WccOrigin) actions[action.Name] = action with { Deleted = true, UpdatedAtUtc = DateTimeOffset.UtcNow };
            else actions.Remove(action.Name);
            Save(worldId, state with { Actions = actions, Revision = checked(state.Revision + 1) });
            return $"Plus action '{action.Name}' deleted.";
        }
        throw new InvalidOperationException("Invalid Plus action editor operation.");
    }

    private string ExecuteContestAdmin(string worldId, RuntimeMajorMudPlusState state, IReadOnlyList<string> arguments, RuntimeMajorMudPlusCatalog catalog, RuntimeLivingWorldService? livingWorld)
    {
        if (arguments.Count == 0 || arguments[0].Equals("status", StringComparison.OrdinalIgnoreCase))
            return RenderContestStatus(worldId, state, catalog, livingWorld);
        if (arguments[0].Equals("start", StringComparison.OrdinalIgnoreCase) && arguments.Count == 2)
        {
            var mode = arguments[1].ToLowerInvariant();
            if (!catalog.ContestModes.Contains(mode)) throw new InvalidOperationException("Contest mode must be visible or hidden.");
            if (state.Contest.State == "active") throw new InvalidOperationException("A Plus contest is already active.");
            if (livingWorld is null) throw new InvalidOperationException("The authoritative living world is required to start a contest.");
            var started = DateTimeOffset.UtcNow;
            var contestId = started.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var hidden = mode == "hidden";
            var contract = catalog.ContestContract;
            var placed = new List<string>();
            foreach (var range in contract.PlacementRanges)
            {
                var candidates = livingWorld.ContestRooms(range.Map, range.MinimumRoom, range.MaximumRoom, contract.ExcludedRoomAttributeMask);
                for (var quantity = 0; quantity < range.Quantity; quantity++)
                {
                    var success = false;
                    for (var attempt = 0; attempt < contract.MaximumAttemptsPerItem && candidates.Count > 0; attempt++)
                    {
                        var room = candidates[Random.Shared.Next(candidates.Count)];
                        if (!livingWorld.TryPlaceContestItem(contract.CanonicalItemId, room.Id, hidden,
                            hidden ? contract.HiddenRoomCapacity : contract.VisibleRoomCapacity, contestId)) continue;
                        placed.Add($"Placed a {mode} rune in map {room.MapId} room {room.RoomNumber}.");
                        success = true;
                        break;
                    }
                    if (!success) placed.Add($"Unable to place rune in map {range.Map} rooms {range.MinimumRoom}-{range.MaximumRoom} after {contract.MaximumAttemptsPerItem} attempts.");
                }
            }
            Save(worldId, state with { Contest = new("active", mode, started), Revision = checked(state.Revision + 1) });
            return $"MajorMUD Plus contest started in {mode} mode; {placed.Count(line => line.StartsWith("Placed", StringComparison.Ordinal))}/40 runes placed." + Environment.NewLine + string.Join(Environment.NewLine, placed);
        }
        if (arguments[0].Equals("finish", StringComparison.OrdinalIgnoreCase) && arguments.Count == 1)
        {
            if (state.Contest.State != "active") throw new InvalidOperationException("No Plus contest is active.");
            if (livingWorld is null) throw new InvalidOperationException("The authoritative living world is required to finish a contest.");
            var finished = state with { Contest = new("finished", state.Contest.Mode, DateTimeOffset.UtcNow), Revision = checked(state.Revision + 1) };
            var result = "MajorMUD Plus contest finished." + Environment.NewLine + RenderContestStatus(worldId, finished, catalog, livingWorld);
            livingWorld.RemoveGroundItems(catalog.ContestContract.CanonicalItemId);
            foreach (var character in _characters.ListAllCharacters().Where(value => value.WorldId.Equals(worldId, StringComparison.OrdinalIgnoreCase)))
            {
                var itemState = character.ItemState;
                if (itemState is null || !itemState.Items.Any(item => item.CanonicalItemId.Equals(catalog.ContestContract.CanonicalItemId, StringComparison.OrdinalIgnoreCase))) continue;
                _characters.SaveCharacter(character with
                {
                    ItemState = itemState with { Items = itemState.Items.Where(item => !item.CanonicalItemId.Equals(catalog.ContestContract.CanonicalItemId, StringComparison.OrdinalIgnoreCase)).ToArray() },
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, "majormud-plus-contest-finish");
            }
            Save(worldId, finished);
            return result;
        }
        throw new InvalidOperationException("Usage: PLUSADMIN CONTEST [START visible|hidden|STATUS|FINISH].");
    }

    private string RenderContestStatus(string worldId, RuntimeMajorMudPlusState state, RuntimeMajorMudPlusCatalog catalog, RuntimeLivingWorldService? livingWorld)
    {
        var lines = new List<string> { $"Contest: {state.Contest.State}; mode={state.Contest.Mode}." };
        if (livingWorld is not null)
        {
            foreach (var room in catalog.ContestContract.PlacementRanges.Select(range => range.Map).Distinct().OrderBy(value => value))
            foreach (var item in livingWorld.ContestRooms(room, 1, int.MaxValue, 0).SelectMany(value => livingWorld.GroundItems(value.Id))
                         .Where(item => item.CanonicalItemId.Equals(catalog.ContestContract.CanonicalItemId, StringComparison.OrdinalIgnoreCase)))
                lines.Add($"Found {(RuntimeClassicHiddenSearchService.IsHidden(item) ? "hidden" : "visible")} in {item.LocationId}.");
        }
        foreach (var character in _characters.ListAllCharacters().Where(value => value.WorldId.Equals(worldId, StringComparison.OrdinalIgnoreCase)))
        {
            var quantity = character.ItemState?.Items.Where(item => item.CanonicalItemId.Equals(catalog.ContestContract.CanonicalItemId, StringComparison.OrdinalIgnoreCase)).Sum(item => item.StackQuantity) ?? 0;
            if (quantity > 0) lines.Add($"Found {quantity} on user {character.DisplayName} (local account: {character.AccountId}).");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private RuntimeCharacterRecord DebitHostAndGameCosts(RuntimeCharacterRecord character, int credits, long copper, int days)
    {
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        var availableCredits = ReadConditionInt(conditions, "credits", 0);
        var availableDays = ReadConditionInt(conditions, "membership_days", 0);
        if (credits > availableCredits) throw new InvalidOperationException("You do not have enough credits.");
        if (days > availableDays) throw new InvalidOperationException("You do not have enough membership days.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var settled = copper > 0 ? RuntimeBundleCEconomyAuthority.DebitCopperValue(state, copper) : state;
        conditions["credits"] = checked(availableCredits - credits).ToString(System.Globalization.CultureInfo.InvariantCulture);
        conditions["membership_days"] = checked(availableDays - days).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return character with { Conditions = conditions, ItemState = settled, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    private void RequireSysop(RuntimeCharacterRecord character)
    {
        var credential = _security.TryLoad(character.AccountId);
        if (credential is null || !credential.Enabled || credential.Role is not (RuntimeAccountRole.Sysop or RuntimeAccountRole.Administrator))
            throw new InvalidOperationException("MajorMUD Plus sysop authority is required.");
    }

    private RuntimeMajorMudPlusState Load(string worldId)
    {
        var path = StatePath(worldId);
        var state = !File.Exists(path)
            ? RuntimeMajorMudPlusState.Empty
            : JsonSerializer.Deserialize<RuntimeMajorMudPlusState>(File.ReadAllText(path), JsonOptions)
              ?? throw new InvalidDataException("MajorMUD Plus state could not be loaded.");
        var actions = Copy(state.Actions);
        foreach (var pair in RuntimeMajorMudPlusCatalog.Load(_characters.ProjectRoot, worldId).DefaultActions)
            actions.TryAdd(pair.Key, pair.Value);
        return state with { Actions = actions };
    }

    private void Save(string worldId, RuntimeMajorMudPlusState state)
    {
        var path = StatePath(worldId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions) + Environment.NewLine);
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
        else File.Move(temp, path);
    }

    private string StatePath(string worldId) =>
        Path.Combine(RuntimeSavePaths.RealmRoot(_characters.ProjectRoot, worldId), "majormud_plus", RuntimeIdentity.Normalize(worldId, "world") + ".json");

    private static RuntimeMajorMudPlusDescription ResolveDescription(RuntimeMajorMudPlusState state, string query)
    {
        var matches = state.Descriptions.Values.Where(x => x.CharacterId.Equals(query, StringComparison.OrdinalIgnoreCase) || x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(matches.Length == 0 ? "Description was not found." : "Description lookup is ambiguous.");
    }

    private static RuntimeMajorMudPlusRegistry ResolveRegistry(RuntimeMajorMudPlusState state, string query)
    {
        var matches = state.Registries.Values.Where(x => x.CharacterId.Equals(query, StringComparison.OrdinalIgnoreCase) || x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(matches.Length == 0 ? "Registry was not found." : "Registry lookup is ambiguous.");
    }

    private static int ReadConditionInt(IReadOnlyDictionary<string, string> conditions, string key, int fallback) =>
        conditions.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    private static Dictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T> source) =>
        new(source, StringComparer.OrdinalIgnoreCase);
}
