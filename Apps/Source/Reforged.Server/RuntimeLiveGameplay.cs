namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeSessionManager _sessions;
    private readonly Dictionary<string, RuntimeGameplayCompletionStateStore> _gameplayStores = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gameplayStoreGate = new();

    public RuntimeLiveGameplayFacade(RuntimeCharacterService characters, RuntimeSessionManager sessions)
    {
        _characters = characters;
        _sessions = sessions;
    }

    private RuntimeGameplayCompletionStateStore GameplayStore(string worldId)
    {
        lock (_gameplayStoreGate)
        {
            if (!_gameplayStores.TryGetValue(worldId, out var store))
            {
                store = new RuntimeGameplayCompletionStateStore(_characters.ProjectRoot, worldId);
                _gameplayStores[worldId] = store;
            }
            return store;
        }
    }

    public string RenderScore(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return session.Actor.DisplayName;
        }

        var character = _characters.LoadCharacter(actor.Id);
        if (character is null)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"Name: {actor.DisplayName}",
                $"Race: {actor.Race}",
                $"Class: {actor.CharacterClass}  Level: {actor.Level}"
            });
        }

        const string green = "\u001b[1;32m";
        const string cyan = "\u001b[1;36m";
        const string reset = "\u001b[0m";
        const int rightColumn = 55;

        static int Read(IReadOnlyDictionary<string, int> values, params string[] keys)
        {
            foreach (var key in keys)
                if (values.TryGetValue(key, out var value)) return value;
            return 0;
        }

        static string ColoredField(string label, object value) =>
            $"{green}{label}{cyan}{value}{reset}";

        static string Compose(string leftPlain, string leftColored, string rightColored, int column)
        {
            var spaces = Math.Max(2, column - leftPlain.Length);
            return leftColored + new string(' ', spaces) + rightColored;
        }

        static (string Plain, string Colored) TwoFields(
            string label1, object value1, int width1,
            string label2, object value2)
        {
            var firstPlain = label1 + value1;
            var pad = Math.Max(1, width1 - firstPlain.Length);
            var plain = firstPlain + new string(' ', pad) + label2 + value2;
            var colored = ColoredField(label1, value1) + new string(' ', pad) + ColoredField(label2, value2);
            return (plain, colored);
        }

        var stats = character.BaseStats;
        var skills = character.Skills;
        var semantic = character.SemanticState;
        var derived = semantic?.DerivedStats ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var resistances = semantic?.Resistances ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var armorPrimary = Read(derived, "armor_class", "armour_class", "armor_class_bonus");
        var armorSecondary = Read(derived, "damage_resistance", "damage_resistance_bonus", "defense_value");
        var lives = ReadCondition(character.Conditions, "lives", "lives_remaining");
        var creationPoints = ReadCondition(character.Conditions, "cp", "creation_points");

        var raceExp = TwoFields("Race: ", character.RaceName, 24, "Exp: ", character.Experience);
        var classLevel = TwoFields("Class: ", character.ClassName, 30, "Level: ", character.Level);
        var hitsArmor = TwoFields("Hits: ", $"{character.HitPoints}/{character.MaximumHitPoints}", 24, "Armour Class: ", $"{armorPrimary}/{armorSecondary}");
        var strAgi = TwoFields("Strength: ", Read(stats, "str", "strength"), 25, "Agility: ", Read(stats, "agl", "agi", "agility"));
        var intHealth = TwoFields("Intellect: ", Read(stats, "int", "intellect"), 25, "Health: ", Read(stats, "hea", "health"));
        var willCharm = TwoFields("Willpower: ", Read(stats, "wil", "willpower"), 25, "Charm: ", Read(stats, "cha", "charm"));

        var lines = new List<string>
        {
            Compose("Name: " + character.DisplayName, ColoredField("Name: ", character.DisplayName), ColoredField("Lives/CP: ", $"{lives}/{creationPoints}"), rightColumn),
            Compose(raceExp.Plain, raceExp.Colored, ColoredField("Perception: ", Read(skills, "perception", "per")), rightColumn),
            Compose(classLevel.Plain, classLevel.Colored, ColoredField("Stealth: ", Read(skills, "stealth", "sneak")), rightColumn),
            Compose(hitsArmor.Plain, hitsArmor.Colored, ColoredField("Thievery: ", Read(skills, "thievery", "thief")), rightColumn),
            new string(' ', rightColumn) + ColoredField("Traps: ", Read(skills, "traps", "trap")),
            new string(' ', rightColumn) + ColoredField("Picklocks: ", Read(skills, "picklocks", "picklock")),
            string.Empty,
            Compose(strAgi.Plain, strAgi.Colored, ColoredField("Tracking: ", Read(skills, "tracking", "track")), rightColumn),
            Compose(intHealth.Plain, intHealth.Colored, ColoredField("Martial Arts: ", Read(skills, "martial_arts", "martialarts", "ma")), rightColumn),
            Compose(willCharm.Plain, willCharm.Colored, ColoredField("MagicRes: ", Read(resistances, "magic", "magicres", "magic_resistance")), rightColumn)
        };
        return string.Join(Environment.NewLine, lines);
    }


    public string RenderExperience(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "Exp: 0 Level: 1 Exp needed for next level: unavailable";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return "Exp: 0 Level: 1 Exp needed for next level: unavailable";

        var nextLevel = checked(character.Level + 1);
        try
        {
            var schedule = RuntimeCharacterProgressionSchedule.Load(Path.Combine(_characters.ProjectRoot, "Data", character.WorldId));
            var row = schedule.Require(character, nextLevel);
            var remaining = Math.Max(0L, row.RequiredExperience - character.Experience);
            var percent = row.RequiredExperience <= 0
                ? 0L
                : (character.Experience * 100L) / row.RequiredExperience;
            return $"Exp: {character.Experience} Level: {character.Level} Exp needed for next level: {remaining} ({row.RequiredExperience}) [{percent}%]";
        }
        catch (Exception ex)
        {
            return $"Exp: {character.Experience} Level: {character.Level} Exp needed for next level: unavailable ({ex.Message})";
        }
    }

    private static int ReadCondition(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var raw) && int.TryParse(raw, out var value)) return value;
        return 0;
    }

    private static string FormatCharacterSex(string? sex) =>
        string.Equals(sex, "Male", StringComparison.OrdinalIgnoreCase) ? "Male" :
        string.Equals(sex, "Female", StringComparison.OrdinalIgnoreCase) ? "Female" :
        "Unspecified";

    public string RenderInventory(RuntimeSession session)
    {
        var state = LoadItemState(session);
        if (state is null)
        {
            return "You are carrying nothing.";
        }

        var inventory = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                && item.EquippedSlot is null)
            .OrderBy(item => item.CanonicalItemId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (inventory.Length == 0)
        {
            return $"You are carrying nothing.\nGold: {state.Gold}";
        }

        return "Inventory:" + Environment.NewLine
            + string.Join(Environment.NewLine, inventory.Select(RenderItem))
            + Environment.NewLine + $"Gold: {state.Gold}";
    }

    public string RenderEquipment(RuntimeSession session)
    {
        var state = LoadItemState(session);
        if (state is null)
        {
            return "You are carrying nothing.\nWealth: 0 copper farthings\nEncumbrance: 0/0 - None [0%]";
        }

        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var owned = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.LocationKind, RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.EquippedSlot is null ? 1 : 0)
            .ThenBy(item => catalog.Require(item.CanonicalItemId).Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var carriedText = owned.Length == 0
            ? "You are carrying nothing."
            : "You are carrying " + string.Join(", ", owned.Select(item =>
            {
                var definition = catalog.Require(item.CanonicalItemId);
                var quantity = item.StackQuantity > 1 ? $"{item.StackQuantity} {definition.Name}" : definition.Name;
                return item.EquippedSlot is null
                    ? quantity
                    : $"{quantity} ({ClassicDisplaySlot(catalog, item)})";
            }));

        var keyNames = owned
            .Where(item => catalog.Require(item.CanonicalItemId).ItemType.Contains("key", StringComparison.OrdinalIgnoreCase)
                || catalog.Require(item.CanonicalItemId).Name.Contains(" key", StringComparison.OrdinalIgnoreCase))
            .Select(item => catalog.Require(item.CanonicalItemId).Name)
            .ToArray();

        var lines = new List<string> { carriedText };
        if (keyNames.Length > 0) lines.Add("You have the following keys:  " + string.Join(", ", keyNames) + ".");
        lines.Add($"Wealth: {state.Gold} copper farthings");

        var character = _characters.LoadCharacter(session.Actor.Id);
        var projection = character is null
            ? null
            : RuntimeBundleCInventoryAuthority.Project(character, catalog);
        if (projection is not null)
        {
            lines[^1] = $"Wealth: {RuntimeBundleCInventoryAuthority.FormatCurrency(projection.Currency)}";
            lines.Add($"Carried weight: {projection.TotalCarriedWeight} ({projection.ItemWeight} items + {projection.CurrencyWeight} currency)");
            lines.Add("Encumbrance capacity and penalty bands: unresolved native relationship; no guessed cap is enforced.");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string ClassicDisplaySlot(RuntimeItemCatalog catalog, RuntimeItemInstance item)
    {
        var relation = catalog.Equipment(item.CanonicalItemId);
        if (relation?.TwoHanded == true) return "Two handed";
        return (item.EquippedSlot ?? relation?.Slot ?? "equipped").ToUpperInvariant() switch
        {
            "MAIN_HAND" or "PRIMARY_HAND" or "WEAPON" or "HAND" or "WIELD" => "One handed",
            "OFF_HAND" or "SECONDARY_HAND" => "Off hand",
            "ARMS" => "Arms",
            "TORSO" or "BODY" or "CHEST" => "Torso",
            "BACK" => "Back",
            "WRIST" or "WRISTS" => "Wrist",
            "FINGER" or "RING" => "Finger",
            "WAIST" or "BELT" => "Waist",
            "FEET" or "BOOTS" => "Feet",
            "HANDS" or "GLOVES" => "Hands",
            "HEAD" or "HELM" => "Head",
            "LEGS" => "Legs",
            "NECK" => "Neck",
            "SHIELD" => "Shield",
            var slot => slot.Replace('_', ' ').ToLowerInvariant()
        };
    }

    public string Save(RuntimeSession session)
    {
        if (!session.Actor.IsPersistentCharacter)
        {
            return "This session does not have a persistent character to save.";
        }

        _characters.SaveSession(session);
        return $"{session.Actor.DisplayName} saved at {session.CurrentRoom.Name}.";
    }

    public string RenderWho() => _sessions.RenderStatus();

    public string RenderIntegrationStatus(RuntimeSession session)
    {
        var persistent = session.Actor.IsPersistentCharacter ? "persistent" : "transient";
        var itemState = LoadItemState(session) is null ? "unavailable" : "attached";
        return string.Join(Environment.NewLine, new[]
        {
            $"Session: {session.Id}",
            $"Actor: {session.Actor.DisplayName} ({persistent})",
            $"World: {session.World.Definition.Id}",
            $"Room: {session.CurrentRoom.Id}",
            $"Item state: {itemState}",
            "Live domains: room, movement, presence, communication, inventory, equipment, banking, save/reconnect",
            "Live combat domains: generated NPC targeting, encounter admission, NPC attack-profile loading, status, flee, movement guard",
            "Policy-gated domains: player AV/DV derivation, combat damage rounds, spells, abilities, quests, shop/trainer transactions, death/respawn"
        });
    }

    public string ExecuteAuditCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (command.Arguments.Count == 0)
        {
            return ClassicRuntimeAudit.RenderRuntimeAudit(_characters.ProjectRoot, session.World, Living(session));
        }

        if (command.Arguments[0].Equals("room", StringComparison.OrdinalIgnoreCase))
        {
            var query = string.Join(" ", command.Arguments.Skip(1)).Trim();
            if (string.IsNullOrWhiteSpace(query)) query = session.CurrentRoom.Id;
            return ClassicRuntimeAudit.RenderRoomInspection(_characters.ProjectRoot, session.World, Living(session), query, RenderRoom(session));
        }

        if (command.Arguments[0].Equals("spawns", StringComparison.OrdinalIgnoreCase))
        {
            return ClassicRuntimeAudit.RenderSpawnVerification(session.World, Living(session));
        }

        return "Use: audit, audit room <room>, or audit spawns.";
    }

    private RuntimeItemState? LoadItemState(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return null;
        }

        return _characters.LoadCharacter(actor.Id)?.ItemState;
    }

    private static string RenderItem(RuntimeItemInstance item)
    {
        var quantity = item.StackQuantity > 1 ? $" x{item.StackQuantity}" : string.Empty;
        var charges = item.Charges > 0 ? $" charges={item.Charges}" : string.Empty;
        return $"- {item.CanonicalItemId}{quantity}{charges} [{item.InstanceId}]";
    }
}

public sealed class RuntimeLiveGameplaySmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveGameplaySmokeReport Run()
    {
        var report = new RuntimeLiveGameplaySmokeReport();
        try
        {
            var look = RuntimeCommand.Parse("look");
            Require(look.Name == "look" && look.Arguments.Count == 0, "parse-look");
            report.Checks.Add("parse-look");

            Require(RuntimeCommand.Parse("l").Name == "look", "parse-look-short");
            Require(RuntimeCommand.Parse("lo").Name == "look", "parse-look-prefix");
            report.Checks.Add("parse-look-abbreviations");

            var examine = RuntimeCommand.Parse("examine ancient sword");
            Require(examine.Name == "examine" && examine.ArgumentText == "ancient sword", "parse-arguments");
            report.Checks.Add("parse-arguments");

            var north = RuntimeCommand.Parse("north");
            Require(north.IsMovement, "movement-alias");
            report.Checks.Add("movement-alias");

            Require(RuntimeCommand.Parse("n").Name == "north", "movement-north-short");
            report.Checks.Add("movement-abbreviations");

            var inventory = RuntimeCommand.Parse("i");
            Require(inventory.Name == "inventory", "command-alias");
            Require(RuntimeCommand.Parse("inv").Name == "inventory", "inventory-inv-alias");
            report.Checks.Add("command-alias");

            Require(RuntimeCommand.Parse("eq").Name == "equipment", "equipment-alias");
            report.Checks.Add("equipment-alias");

            Require(RuntimeCommand.Parse("stat").Name == "score", "stat-alias");
            Require(RuntimeCommand.Parse("stats").Name == "score", "stats-alias");
            Require(RuntimeCommand.Parse("status").Name == "score", "status-alias");
            report.Checks.Add("stat-command-aliases");

            Require(RuntimeCommand.Parse("op n").Name == "open", "open-prefix");
            Require(RuntimeCommand.Parse("ba north").Name == "bash", "bash-prefix");
            report.Checks.Add("interaction-command-prefixes");

            Require(RuntimeCommand.Parse("s").Name.Equals("south", StringComparison.Ordinal), "movement-south-s");
            Require(RuntimeCommand.Parse("se").Name.Equals("southeast", StringComparison.Ordinal), "movement-southeast-se");
            Require(RuntimeCommand.Parse("st").ResolutionError.StartsWith("Ambiguous command", StringComparison.Ordinal), "ambiguous-st");
            report.Checks.Add("ambiguous-prefix-rejection");

            var save = RuntimeCommand.Parse("save");
            Require(save.Name == "save", "save-command");
            report.Checks.Add("save-command");

            var audit = RuntimeCommand.Parse("audit room Village Entrance");
            Require(audit.Name == "audit" && audit.ArgumentText == "room Village Entrance", "audit-command");
            report.Checks.Add("audit-command");

            Require(RuntimeCommand.SupportedCommands.Contains("score"), "supported-command-catalog");
            report.Checks.Add("supported-command-catalog");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
